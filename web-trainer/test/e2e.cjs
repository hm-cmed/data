/*
 * End-to-end test for dissection-trainer.html, opened from file:// exactly as a user would.
 * CDN requests are served from local copies of the same package versions (npm), so the test
 * needs no network. A fake camera device (Chromium flag) exercises the MediaPipe pipeline,
 * and MediaPipe's own sample hand photos check the gesture classifier on real landmarks.
 *
 * Usage: VENDOR_DIR=/path/to/vendor OUT_DIR=/path/for/screenshots node test/e2e.cjs
 * VENDOR_DIR must contain: three-0.128.0/package, mediapipe-tasks-vision-0.10.14/package,
 * hand_landmarker.task and sample images (fist.jpg, pointing_up.jpg, victory.jpg, thumb_up.jpg).
 */
const { chromium } = require("playwright");
const fs = require("fs");
const path = require("path");

const VENDOR = process.env.VENDOR_DIR;
const OUT = process.env.OUT_DIR || path.join(__dirname, "out");
const PAGE = "file://" + path.resolve(__dirname, "../dissection-trainer.html");
const TEMPLATE = path.resolve(__dirname, "../template/dissection-template.glb");
fs.mkdirSync(OUT, { recursive: true });
fs.mkdirSync(path.dirname(TEMPLATE), { recursive: true });

const results = [];
function check(name, ok, info) { results.push({ name, ok: !!ok, info }); console.log((ok ? "PASS " : "FAIL ") + name + (info !== undefined ? "  " + JSON.stringify(info) : "")); }

const TYPES = { ".js": "text/javascript", ".mjs": "text/javascript", ".wasm": "application/wasm", ".task": "application/octet-stream", ".map": "application/json" };
async function serveVendor(ctx) {
  const map = [
    [/^https:\/\/cdnjs\.cloudflare\.com\/ajax\/libs\/three\.js\/r128\/three\.min\.js$/, () => "three-0.128.0/package/build/three.min.js"],
    [/^https:\/\/cdn\.jsdelivr\.net\/npm\/three@0\.128\.0\/(.+)$/, (m) => "three-0.128.0/package/" + m[1]],
    [/^https:\/\/cdn\.jsdelivr\.net\/npm\/@mediapipe\/tasks-vision@0\.10\.14\/(.+)$/, (m) => "mediapipe-tasks-vision-0.10.14/package/" + m[1]],
    [/^https:\/\/storage\.googleapis\.com\/mediapipe-models\/hand_landmarker\/hand_landmarker\/float16\/1\/hand_landmarker\.task$/, () => "hand_landmarker.task"],
  ];
  await ctx.route(/^https?:\/\//, async (route) => {
    const url = route.request().url();
    if (/fonts\.(googleapis|gstatic)\.com/.test(url)) return route.fulfill({ status: 200, contentType: "text/css", body: "", headers: { "access-control-allow-origin": "*" } });
    for (const [re, f] of map) {
      const m = url.match(re);
      if (m) {
        const file = path.join(VENDOR, f(m));
        if (!fs.existsSync(file)) return route.fulfill({ status: 404, body: "missing " + file });
        return route.fulfill({ status: 200, body: fs.readFileSync(file), headers: { "content-type": TYPES[path.extname(file)] || "application/octet-stream", "access-control-allow-origin": "*" } });
      }
    }
    console.log("  (blocked unexpected request) " + url);
    return route.abort();
  });
}

async function newPage(browser, opts) {
  const ctx = await browser.newContext(Object.assign({ viewport: { width: 1280, height: 900 }, deviceScaleFactor: 1 }, opts || {}));
  await serveVendor(ctx);
  const page = await ctx.newPage();
  const errors = [];
  page.on("pageerror", (e) => errors.push("pageerror: " + e.message));
  page.on("console", (m) => { if (m.type() === "error") errors.push("console: " + m.text()); });
  await page.goto(PAGE);
  await page.waitForFunction(() => window.__dbg && window.__dbg.state().phase === "pre", null, { timeout: 20000 });
  return { ctx, page, errors };
}

const st = (page) => page.evaluate(() => window.__dbg.state());
const scr = (page, p) => page.evaluate((q) => window.__dbg.screenOf(q), p);
async function fieldCenter(page) { const b = await page.locator("#field").boundingBox(); return { x: b.x + b.width / 2, y: b.y + b.height / 2, b }; }
async function waitStep(page, key, ms) {
  try { await page.waitForFunction((k) => window.__dbg.state().stepKey === k || window.__dbg.state().phase !== "run", key, { timeout: ms || 4000 }); return true; } catch (e) { return false; }
}
async function grabTo(page, from, to) {         // right-drag: grab the tissue at `from`, release at `to`
  await page.mouse.move(from.x, from.y);
  await page.mouse.down({ button: "right" });
  await page.mouse.move(to.x, to.y, { steps: 14 });
  await page.mouse.up({ button: "right" });
}
async function centerOn(page, getPoint) {
  for (let i = 0; i < 4; i++) {
    const c = await fieldCenter(page), p = await scr(page, await getPoint());
    if (Math.hypot(p.x - c.x, p.y - c.y) < 8) break;
    await grabTo(page, p, c);
    await page.waitForTimeout(60);
  }
}
// wait for rendered frames so every pointer position is seen by the 60 Hz tick (software GL is slow)
const frames = (page, n) => page.evaluate((k) => new Promise((r) => { let i = 0; const f = () => (++i >= k ? r() : requestAnimationFrame(f)); requestAnimationFrame(f); }), n || 1);
async function cutAlong(page, s0, s1, ds) {
  let p = await scr(page, await page.evaluate((s) => window.__dbg.pathAt(s), s0));
  await page.mouse.move(p.x, p.y); await frames(page, 2);
  await page.mouse.down({ button: "left" });
  for (let s = s0; s <= s1 + 1e-6; s += ds) {
    p = await scr(page, await page.evaluate((v) => window.__dbg.pathAt(v), s));
    await page.mouse.move(p.x, p.y); await frames(page, 1);
  }
  await frames(page, 3);
  await page.mouse.up({ button: "left" });
}

async function runMouseSession(page, label, shots) {
  await page.click("#src-mouse");
  await page.click("#start");
  await page.waitForFunction(() => window.__dbg.state().phase === "run");
  // 1 centre
  await centerOn(page, () => page.evaluate(() => window.__dbg.start()));
  check(label + ": step 1 (centre) completes", await waitStep(page, "zoom", 4000));
  // 2 zoom with the wheel
  const c = await fieldCenter(page);
  await page.mouse.move(c.x, c.y);
  const d = (await st(page)).d;
  await page.mouse.wheel(0, Math.log(11 / d) / 0.0015);
  await page.waitForTimeout(100);
  await centerOn(page, () => page.evaluate(() => window.__dbg.start()));
  check(label + ": step 2 (zoom) completes", await waitStep(page, "approach", 4000), { d: (await st(page)).d });
  // 3 approach
  const sp = await scr(page, await page.evaluate(() => window.__dbg.start()));
  await page.mouse.move(sp.x, sp.y);
  check(label + ": step 3 (approach) completes", await waitStep(page, "cut1", 3000));
  // 4 cut along the plane to marker A
  const s0 = await st(page);
  await cutAlong(page, 0, s0.sA + 0.05, 0.07);
  if (shots) await page.screenshot({ path: path.join(OUT, label + "-run-cut1.png") });
  check(label + ": step 4 (cut1) completes", await waitStep(page, "recenter", 3000), { front: (await st(page)).front, sA: s0.sA });
  // 5 recentre on marker A
  await centerOn(page, () => page.evaluate(() => window.__dbg.markerA()));
  check(label + ": step 5 (recenter) completes", await waitStep(page, "cut2", 4000));
  // 6 cut to the end, re-centring on the front before each chunk
  for (let k = 0; k < 12; k++) {
    const s = await st(page);
    if (s.phase !== "run") break;
    await centerOn(page, () => page.evaluate(() => window.__dbg.pathAt(window.__dbg.state().front)));
    await cutAlong(page, s.front, Math.min(s.L, s.front + 2.4), 0.07);
  }
  await page.waitForFunction(() => window.__dbg.state().phase === "debrief", null, { timeout: 5000 }).catch(() => {});
  const fin = await st(page);
  check(label + ": session reaches debrief", fin.phase === "debrief");
  return fin;
}

(async () => {
  const browser = await chromium.launch({
    args: ["--use-fake-ui-for-media-stream", "--use-fake-device-for-media-stream", "--use-gl=angle", "--use-angle=swiftshader", "--enable-unsafe-swiftshader", "--ignore-gpu-blocklist"],
  });

  /* ---------- A. load + geometry sanity ---------- */
  let { ctx, page, errors } = await newPage(browser);
  await page.screenshot({ path: path.join(OUT, "pre-light.png"), fullPage: true });
  const vmin = await page.evaluate(() => window.__dbg.vesselMin());
  check("default vessel clears the ±3.5 mm plane + 5.5 mm injury radius (min distance 1.0–1.4 cm)", vmin > 1.0 && vmin < 1.4, { vesselMin_cm: +vmin.toFixed(3) });
  const L = (await st(page)).L;
  check("default dissection path length is plausible (10–16 cm)", L > 10 && L < 16, { L_cm: +L.toFixed(2) });

  /* ---------- B. full mouse session, guided ---------- */
  const fin = await runMouseSession(page, "guided-mouse", true);
  const sm = fin.summary || {};
  check("clean run: completed, no off-plane, no vessel injury", sm.completed === 1 && sm.off_plane_episodes === 0 && sm.vessel_injuries === 0, sm);
  check("clean run: mean path error under tolerance (<3.5 mm)", sm.path_error_mm < 3.5, { path_error_mm: sm.path_error_mm });
  await page.waitForTimeout(150);
  await page.screenshot({ path: path.join(OUT, "debrief-light.png"), fullPage: true });
  const csv = await page.evaluate(() => window.__dbg.csv());
  const lines = csv.split("\n");
  check("CSV has header, events, 10 Hz samples, summary and NASA-TLX rows",
    lines[0] === "t_ms,type,step,detail" && /,session_start,/.test(csv) && /,sample,/.test(csv) && /,summary,,completed=1/.test(csv) && /,nasa_tlx,,raw_tlx=/.test(csv),
    { rows: lines.length, samples: lines.filter((l) => l.includes(",sample,")).length });
  check("no page errors during the mouse session", errors.length === 0, errors.slice(0, 5));

  /* ---------- C. error detection: stray toward the vessel ---------- */
  await page.click("#restart");
  await page.click("#mode-faded");
  await page.click("#start");
  await page.waitForFunction(() => window.__dbg.state().phase === "run");
  await centerOn(page, () => page.evaluate(() => window.__dbg.start()));
  await waitStep(page, "zoom");
  let c = await fieldCenter(page); await page.mouse.move(c.x, c.y);
  await page.mouse.wheel(0, Math.log(11 / (await st(page)).d) / 0.0015);
  await centerOn(page, () => page.evaluate(() => window.__dbg.start()));
  await waitStep(page, "approach");
  const sp = await scr(page, await page.evaluate(() => window.__dbg.start()));
  await page.mouse.move(sp.x, sp.y);
  await waitStep(page, "cut1");
  // cut a little, then keep cutting straight across the plane onto the nearest vessel point
  await cutAlong(page, 0, 1.0, 0.07);
  const from = await page.evaluate(() => window.__dbg.pathAt(1.0));
  const ves = await page.evaluate((p) => window.__dbg.vesselNearest(p), from);
  await page.mouse.down({ button: "left" });
  for (let k = 0; k <= 40; k++) {
    const t = k / 40;
    const p = await scr(page, { x: from.x + (ves.x - from.x) * t, y: from.y + (ves.y - from.y) * t, z: from.z + (ves.z - from.z) * t });
    await page.mouse.move(p.x, p.y); await frames(page, 1);
  }
  await frames(page, 4);
  await page.screenshot({ path: path.join(OUT, "faded-error.png") });
  await page.mouse.up({ button: "left" });
  const se = await st(page);
  check("straying off the plane is counted", se.m.offPlane >= 1, { offPlane: se.m.offPlane });
  check("touching the vessel while cutting is counted as an injury", se.m.injuries >= 1, { injuries: se.m.injuries });
  await page.click("#abort");
  await page.waitForFunction(() => window.__dbg.state().phase === "debrief");
  const sa = (await st(page)).summary;
  check("abort produces a partial debrief", sa.completed === 0 && sa.progress_pct < 100, { progress: sa.progress_pct });
  const improve = await page.textContent("#d-improve");
  check("debrief gives one improvement point about the vessel", /血管/.test(improve), improve);
  await page.screenshot({ path: path.join(OUT, "debrief-error.png"), fullPage: true });
  check("no page errors during the error session", errors.length === 0, errors.slice(0, 5));
  await ctx.close();

  /* ---------- D. hand-tracking pipeline (fake camera) + real-photo gestures ---------- */
  ({ ctx, page, errors } = await newPage(browser));
  await page.click("#src-hand");
  let ready = false;
  try { await page.waitForFunction(() => window.__dbg.state().hand.ready, null, { timeout: 90000 }); ready = true; } catch (e) {}
  const hs = await st(page);
  check("MediaPipe Hand Landmarker loads from file:// with a camera stream", ready, { delegate: hs.hand.delegate, status: await page.textContent("#hand-status") });
  if (ready) {
    const f0 = (await st(page)).hand.frames; await page.waitForTimeout(1500); const f1 = (await st(page)).hand.frames;
    check("detection loop runs on live video frames", f1 > f0 + 3, { framesIn1_5s: f1 - f0 });
    check("start button enabled once the hand model is ready", await page.isEnabled("#start"));
    await page.screenshot({ path: path.join(OUT, "pre-hand.png"), fullPage: true });
    const want = { "fist.jpg": ["fist"], "pointing_up.jpg": ["open"], "victory.jpg": ["open"], "thumb_up.jpg": ["fist", "open"] };
    const realLms = {};
    for (const [img, ok] of Object.entries(want)) {
      const b64 = fs.readFileSync(path.join(VENDOR, img)).toString("base64");
      const r = await page.evaluate(async (src) => {
        const im = new Image(); im.src = src; await im.decode();
        return window.__dbg.handOnImage(im);
      }, "data:image/jpeg;base64," + b64);
      const pose = r && r.pose ? r.pose.pose : null;
      if (r && r.lm) realLms[img] = r.lm;
      check("real photo " + img + " → " + ok.join("/"), ok.includes(pose), { pose, ratio: r && r.pose ? +r.pose.ratio.toFixed(2) : null, curls: r && r.pose ? r.pose.curls : null });
    }
    // pinch: real open-hand landmarks with the thumb tip moved onto the index tip
    if (realLms["victory.jpg"]) {
      const res = await page.evaluate((lm) => {
        const a = lm.map((q) => Object.assign({}, q));
        a[4] = { x: a[8].x + 0.01, y: a[8].y + 0.01, z: a[8].z };
        const pinch = window.__dbg.classify(a, 342 / 362, false).pose;
        const b = lm.map((q) => Object.assign({}, q));
        b[4] = { x: (b[4].x + b[8].x) / 2, y: (b[4].y + b[8].y) / 2, z: b[8].z };  // half-closed: should stay open when not already pinching
        return { pinch, half: window.__dbg.classify(b, 342 / 362, false).pose, halfHeld: window.__dbg.classify(b, 342 / 362, true).pose,
                 open: window.__dbg.classify(lm, 342 / 362, false).pose };
      }, realLms["victory.jpg"]);
      check("pinch detected when thumb tip meets index tip", res.pinch === "pinch", res);
      check("open hand is not a pinch", res.open === "open", res);
    }
    // a short hand-mode session start: overlay + rail wiring
    await page.click("#start");
    await page.waitForFunction(() => window.__dbg.state().phase === "run");
    await page.waitForTimeout(600);
    check("hand-mode run shows the camera card in the rail", await page.isVisible("#cam-slot-run #camcard"));
    check("hand-mode shows a 'hand not visible' warning with no hand in view", /手が見えません/.test(await page.textContent("#warns")));
    await page.screenshot({ path: path.join(OUT, "run-hand.png") });
  }
  check("no page errors in hand mode", errors.length === 0, errors.slice(0, 5));
  await ctx.close();

  /* ---------- E. Blender round trip: export a template GLB in metres, import it back ---------- */
  ({ ctx, page, errors } = await newPage(browser));
  await page.addScriptTag({ path: path.join(VENDOR, "three-0.128.0/package/examples/js/exporters/GLTFExporter.js") });
  const glbB64 = await page.evaluate(() => new Promise((resolve, reject) => {
    const ex = window.__dbg.exportScene();
    const root = new THREE.Group(); root.name = "Template";
    const tissue = ex.tissue.clone(); tissue.name = "Tissue";
    tissue.material = new THREE.MeshStandardMaterial({ map: ex.tissue.material.map, roughness: 0.36 });
    root.add(tissue);
    ex.path.forEach((p, i) => { const o = new THREE.Object3D(); o.name = "Path_" + String(i).padStart(2, "0"); o.position.set(p[0], ex.h(p[0], p[1]), p[1]); root.add(o); });
    const vp = ex.vessel.map((p) => new THREE.Vector3(p[0], ex.h(p[0], p[1]) + 0.1, p[1]));
    ex.vessel.forEach((p, i) => { const o = new THREE.Object3D(); o.name = "Vessel_" + String(i).padStart(2, "0"); o.position.copy(vp[i]); root.add(o); });
    const tube = new THREE.Mesh(new THREE.TubeGeometry(new THREE.CatmullRomCurve3(vp, false, "centripetal"), 200, 0.28, 14, false), new THREE.MeshStandardMaterial({ color: 0x6a1d36, roughness: 0.3 }));
    tube.name = "VesselTube"; root.add(tube);
    root.scale.setScalar(0.01);                       // Blender default unit: metres
    new THREE.GLTFExporter().parse(root, (buf) => {
      const u8 = new Uint8Array(buf); let s = ""; for (let i = 0; i < u8.length; i += 0x8000) s += String.fromCharCode.apply(null, u8.subarray(i, i + 0x8000));
      resolve(btoa(s));
    }, { binary: true });
  }));
  fs.writeFileSync(TEMPLATE, Buffer.from(glbB64, "base64"));
  check("template GLB exported (metres, Path_## / Vessel_## empties)", fs.statSync(TEMPLATE).size > 10000, { bytes: fs.statSync(TEMPLATE).size });
  await ctx.close();
  ({ ctx, page, errors } = await newPage(browser));
  await page.click("details summary");
  await page.setInputFiles("#glb-input", TEMPLATE);
  await page.waitForFunction(() => window.__dbg.state().glb, null, { timeout: 15000 }).catch(() => {});
  const g = (await st(page)).glb;
  const nV = await page.evaluate(() => window.__dbg.exportScene().vessel.length), nP = await page.evaluate(() => window.__dbg.exportScene().path.length);
  check("GLB import finds path / vessel empties and converts metres → cm", g && g.pathPoints === nP && g.vesselPoints === nV && /m → cm/.test(g.unit) && Math.abs(g.pathCm - L) < 0.3, g);
  const finG = await runMouseSession(page, "glb-mouse", false);
  check("full session runs on the imported GLB", finG.summary && finG.summary.completed === 1 && finG.summary.anatomy === "glb", finG.summary);
  check("no page errors with the GLB", errors.length === 0, errors.slice(0, 5));
  await ctx.close();

  /* ---------- F. dark theme + phone width render ---------- */
  ({ ctx, page, errors } = await newPage(browser, { colorScheme: "dark" }));
  await page.screenshot({ path: path.join(OUT, "pre-dark.png"), fullPage: true });
  await ctx.close();
  ({ ctx, page, errors } = await newPage(browser, { viewport: { width: 400, height: 860 } }));
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
  check("no horizontal scroll at 400 px", overflow <= 0, { overflow });
  await page.screenshot({ path: path.join(OUT, "pre-phone.png"), fullPage: true });
  await ctx.close();

  await browser.close();
  const failed = results.filter((r) => !r.ok);
  console.log("\n" + (results.length - failed.length) + "/" + results.length + " checks passed");
  process.exit(failed.length ? 1 : 0);
})().catch((e) => { console.error(e); process.exit(2); });
