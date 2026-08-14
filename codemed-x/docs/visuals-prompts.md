# 360度背景を画像生成AIで作るためのプロンプト集

①②④⑤ の背景を、正距円筒（equirectangular）の 360度静止画として生成するためのプロンプト。

## 使う前に

### ツールの選択

**正距円筒を直接出力できるツールを使うこと。**
汎用の画像生成AI（Midjourney、SDXL、Imagen 等）にプロンプトで「equirectangular」と
指定しても、左右の端がつながらず、天頂・天底の歪みが図法どおりにならないことが多い。
Unity の `Skybox/Panoramic` に入れると継ぎ目が見える。

360度パノラマ専用のツール（Blockade Labs の Skybox AI など）なら、
出力が最初から正距円筒になるため、下記のプロンプトは**シーンの記述部分だけ**を使えばよい。
汎用モデルを使う場合は、冒頭の投影指定ごと入れる。

### 4 つの鉄則

1. **人物を入れない。** 登場人物は別レイヤーで重ねる。背景に焼き込むと、
   分岐ごとに背景を作り直すことになり、同じ人物の見た目も揃わない。
2. **文字を入れない。** 生成AIは日本語の文字を崩す。掲示物・薬袋・カルテの文字は
   崩れた文字列になり、それだけで教材の信頼性が落ちる。必要な文字は Unity 側で重ねる。
3. **視点の高さを指定する。** 面談シーンは学習者が座っているので、
   カメラは座位の目線（約 1.2m）。立って歩く場面は約 1.6m。
   ここを外すと、相手を見下ろす／見上げる不自然な絵になる。
4. **人物を置く場所を空ける。** 立ち絵やビデオを合成する位置に、
   空の椅子や余白を作っておく。埋まっていると合成できない。

### 共通の否定プロンプト（Negative prompt）

```
people, humans, person, faces, hands, crowd, patient, doctor, nurse, child,
text, letters, kanji, japanese writing, signage, posters with readable writing,
labels, watermark, logo, signature,
fisheye distortion, tilted horizon, warped ceiling, duplicated furniture,
western hospital, american office, european interior,
cluttered stock-photo look, oversaturated, HDR halo, cartoon, illustration, 3d render
```

### 共通の接頭辞（汎用モデルを使う場合のみ）

```
Equirectangular 360-degree panorama, 2:1 aspect ratio, seamless horizontal wrap,
horizon line at exact vertical center, no visible seam at the left and right edges,
photorealistic, natural color, even lighting,
```

---

## ① 相談援助面接（児童相談所の家庭訪問）— `welfare_abuse_v1`

3 枚必要。局面ごとに背景を切り替える。

### ①-A 児童相談所の執務室（事前確認・報告の場面）

視点: 座位 1.2m ／ 人物を置く位置: 正面やや右（上司）

```
Interior of a Japanese municipal child guidance center office during daytime.
Rows of grey steel desks pushed together facing each other, stacks of manila
folders and ring binders, beige linoleum floor, suspended fluorescent ceiling
lights, white metal filing cabinets along the walls, a blank whiteboard, a small
potted plant, aluminum-framed windows with white venetian blinds, a wall clock.
Functional and slightly worn public-sector interior, not modern or luxurious.
Camera at seated eye height, 1.2 meters above the floor.
Empty office chairs, completely unoccupied.
Overcast daylight through the windows mixed with neutral white fluorescent light.
```

### ①-B 集合住宅の外廊下（訪問時の到着）

視点: 立位 1.6m ／ 人物を置く位置: 正面（半分開いたドアの向こう）

```
Exterior walkway of an older Japanese low-rise apartment building, late afternoon.
Concrete open-air corridor with a painted steel handrail, weathered pale green
metal doors, electricity meters and gas meters on the wall, an overflowing mailbox,
a folded umbrella and a plastic bucket left beside one door, faded paint and rust
streaks on the concrete. A residential neighborhood of low houses visible beyond
the handrail. Camera at standing eye height, 1.6 meters above the floor,
positioned in front of one apartment door.
No people. Low warm sunlight, long shadows, slightly overcast sky.
```

### ①-C 生活が荒れた居間（観察・面談の場面）★ 中核

視点: 立位 1.6m（観察）／ 人物を置く位置: 正面

養育環境の所見が観察できることが要件。**扇情的にしない**。
散らかった部屋そのものが目的ではなく、「事実として記録できる手がかり」が
画面内にあることが目的。

```
Interior of a small, run-down Japanese apartment living room in the late afternoon,
curtains half closed so the room is dim. Worn tatami and a stained beige carpet,
a low wooden table with unwashed cups and convenience-store food containers,
several empty beer cans and a large liquor bottle standing on the floor beside the
table, laundry piled unfolded in a corner, a thin blanket and a small cushion on the
floor near the wall, an old CRT-style television on a low stand, a sliding paper
door with a small tear, a kitchen sink visible through the doorway with dishes
stacked in it, a nearly empty refrigerator with its door slightly ajar,
unopened envelopes scattered near the entrance.
Camera at standing eye height, 1.6 meters above the floor, in the middle of the room.
No people. Dim natural light through thin curtains, no bright colors, muted palette.
```

**生成後に確認すること**: 酒瓶・食品容器・洗濯物・薄い毛布・郵便物が
それぞれ判別できる位置にあるか。シナリオの観察対象（`WelfareScenarioData`）と
対応していないと、学習者が「見るべきもの」を探せない。

---

## ② 困難な対話（ACP / SPIKES）— `acp_dialogue_v1`

2 枚。SPIKES の S（環境設定）で「面談室へ移る」選択があるので、
**外来診察室と面談室の 2 枚を切り替えられると、選択の意味が体験できる**。

### ②-A 外来診察室（環境設定を怠った場合）

視点: 座位 1.2m ／ 人物を置く位置: 正面（回転椅子）と右斜め前（付き添い椅子）

```
Interior of a Japanese hospital outpatient consultation room, daytime.
A simple desk with a desktop computer monitor turned away from the camera, a
rolling stool, an examination bed with a light blue vinyl mattress and a paper
sheet, a folding privacy curtain partly drawn, a blood pressure monitor on a
small cart, a stainless steel wagon with a kidney dish, a wall-mounted light box,
a small round wall clock, pale grey vinyl flooring, plain white walls,
a closed sliding door to the corridor.
Camera at seated eye height, 1.2 meters above the floor, at the doctor's position.
Empty patient chair and empty companion chair in front of the camera.
No people. Bright even fluorescent lighting, slightly cool white balance.
```

### ②-B 面談室（環境設定を行った場合）

視点: 座位 1.2m ／ 人物を置く位置: 正面と正面やや左（テーブル越しに 2 席）

```
Interior of a small quiet consultation room in a Japanese hospital, used for
family meetings. A light wood rectangular table with four upholstered chairs,
soft indirect ceiling lighting, warm beige walls, a box of tissues placed on the
table, a small potted plant on a side cabinet, a framed landscape print on the
wall, a closed door with a frosted glass panel, warm-toned vinyl floor,
a window with sheer curtains letting in soft daylight.
Calm and private, noticeably softer than a clinical examination room.
Camera at seated eye height, 1.2 meters above the floor, at one side of the table.
Two empty chairs on the opposite side of the table, facing the camera.
No people. Soft warm lighting, gentle contrast.
```

**設計上の意図**: A と B の落差が、SPIKES の S（環境設定）の意味そのもの。
2 枚の雰囲気の差をはっきりつけること。

---

## ④ 薬剤師：服薬指導から疑義照会 — `pharmacist_inquiry_v1`

2 枚。

### ④-A 保険薬局の投薬カウンター（服薬指導の場面）

視点: 座位 1.2m（カウンター越しに着座）／ 人物を置く位置: 正面（カウンターの向こう）

```
Interior of a small Japanese community pharmacy, seen from behind the counseling
counter, daytime. A low wooden counter with a partition, a waiting area with four
linked chairs and a low table with magazines, pale wood-grain vinyl flooring,
white shelving units with rows of small drawers and labeled boxes behind the
camera, a water dispenser in the corner, a hand sanitizer bottle on the counter,
an automatic glass entrance door with the street visible outside, a ceiling air
conditioner, bright even lighting.
Camera at seated eye height, 1.2 meters above the floor, at the pharmacist's side
of the counter.
Empty chair on the patient's side of the counter, directly facing the camera.
No people. Clean, bright, neutral white lighting.
```

### ④-B 調剤室（検査値の確認・医師への電話）

視点: 立位 1.6m ／ 人物を置く位置: なし（学習者が一人で作業する場面）

```
Interior of the dispensing room of a small Japanese pharmacy, daytime.
A stainless steel work bench with a tablet counting tray and a mortar, a desktop
computer with two monitors angled away from the camera, a label printer, an
automatic tablet packaging machine against the wall, tall shelves of small
uniform white boxes and amber bottles, a refrigerator with a glass door,
a cordless telephone handset on a wall cradle, a wall-mounted clock,
pale grey floor, bright white lighting, no windows.
Camera at standing eye height, 1.6 meters above the floor.
No people. Even clinical white lighting, no shadows.
```

**注意**: 薬の箱や棚のラベルに文字が入ると崩れる。否定プロンプトの
`labels, text` を強めに効かせ、必要な文字情報（処方箋・検査値）は
Unity の UI 側で重ねる。

---

## ⑤ ゲートキーパー：自殺リスク評価と危機介入 — `gatekeeper_crisis_v1`

2 枚。

### ⑤-A 地域の相談窓口の個室（対話の場面）★ 中核

視点: 座位 1.2m ／ 人物を置く位置: 正面（テーブル越し）

```
Interior of a small private counseling room in a Japanese municipal health center,
daytime. A light wood round table with two simple upholstered chairs facing each
other, soft warm ceiling lighting, plain pale walls, a box of tissues on the table,
a small potted green plant on a low cabinet, a window with sheer white curtains
letting in diffused daylight, a closed plain door, warm grey carpet tile floor,
a small wall clock. Calm, plain and unintimidating, neither clinical nor corporate.
Camera at seated eye height, 1.2 meters above the floor, at one chair.
The chair opposite the camera is empty.
No people. Soft diffused daylight, warm neutral tone, low contrast.
```

**設計上の意図**: 威圧感を与えない部屋であること。
取調室のような硬い雰囲気にしない。窓と観葉植物が効く。

### ⑤-B 保健センターの待合（導入・終了時）

視点: 立位 1.6m ／ 人物を置く位置: なし

```
Waiting area of a Japanese municipal public health center, daytime.
Rows of linked blue fabric chairs, a low table with pamphlet stands (blank
pamphlets, no readable text), pale linoleum floor, a reception counter with a
lowered glass partition in the background, large windows with white blinds,
a few potted plants, a wall-mounted television turned off, bright even lighting.
Ordinary, slightly dated public facility, clean and calm.
Camera at standing eye height, 1.6 meters above the floor.
No people. Bright neutral daylight mixed with fluorescent light.
```

---

## 生成後のチェックリスト

技術面:

- [ ] 縦横比が **2:1** になっているか（例 8192×4096）
- [ ] 左右の端がつながるか（画像編集ソフトで横に 50% ずらして継ぎ目を確認）
- [ ] 地平線が画像の**縦方向のちょうど中央**にあるか
- [ ] 天頂（上端）と天底（下端）が破綻していないか
- [ ] Unity のインポート設定で **Max Size を 8192 に上げたか**（既定 2048 のままだとボケる）

内容面:

- [ ] **人物が写り込んでいないか**（1 人でも入っていたら作り直す）
- [ ] **崩れた文字が写っていないか**（掲示物・ラベル・看板）
- [ ] 人物を合成する位置が空いているか
- [ ] 日本の施設に見えるか。**ここは必ず実務者に見てもらう**
      （器材の配置、床材、椅子の並び、カーテンの色ひとつで「違う」と感じられる）
- [ ] ①-C が扇情的になっていないか。観察の手がかりがあればよく、
      悲惨さを強調する必要はない

権利・倫理:

- [ ] 生成物の商用利用・教材利用が、使ったツールの規約で許されているか
- [ ] 生成された人物（誤って入った場合）が実在の人物に似ていないか
- [ ] ⑤ の背景が、特定の属性や生活水準と結びつく描写になっていないか

## 反復のコツ

一度で決まらない。次の順で詰めると早い。

1. まず**構図と視点の高さ**だけを見る。人物を置く位置が空いているか
2. 次に**日本らしさ**を見る。違和感のある要素を否定プロンプトに追加していく
   （例: `hospital bed with wooden headboard, curtain track on the ceiling` など、
   出てしまった余計な物を名指しで打ち消す）
3. 最後に**明るさと色味**を揃える。同じシナリオ内の 2 枚は光の条件を合わせる
4. 採用した画像のプロンプトは、**この文書に追記して残す**。
   後で差し替えるときに同じ雰囲気で作り直せる
