# カメラ剥離トレーナー(ブラウザ版・Unity不要)

認知負荷理論(Nishida & Masumori, *Int J Urol* 2026)に基づく、**内視鏡カメラ操作+剥離の part-task 教材**です。
webカメラの手の動き(MediaPipe Hand Landmarker)またはマウスで操作し、ブラウザだけで動作します。
3D表示は Three.js、術野モデルは Blender で作った GLB に差し替えられます。

## 使い方(いちばん簡単な方法)

1. `dissection-trainer.html` をダウンロードし、**Chrome または Edge** で開く(ダブルクリック)。
2. 「操作方法」で **webカメラ(手)** を選び、カメラの使用を「許可」する。
3. プレビューに手の骨格(線)が出ることを確かめてから「課題を開始」。

初回は手の認識モデル(約8 MB)をインターネットから読み込むため、10秒ほどかかります。
Safari では動作しない場合があります。

### 手の操作(片手で完結)

| 手の形 | 動作 |
|---|---|
| 手を開く | 器具を動かす(親指と人差し指の中点が器具の先端) |
| 親指と人差し指をつまむ | 剥離する |
| 握る(4本の指を曲げる) | 視野のクラッチ。握ったまま動かすと視野が動き、カメラに近づける/離すと拡大/縮小 |

カメラから40〜60 cm、手のひらをカメラに向け、プレビューの点線の枠の中で動かします。
マウスの場合: 左ボタン=剥離、右ドラッグ(または Shift+ドラッグ)=視野、ホイール=拡大。

## 教材の構成(CLT との対応)

| 論文の方略(Table 3) | この教材での実装 |
|---|---|
| Pre-training | 術前ブリーフで「剥離平面・血管・前線を中央に」の3つの手がかりを先に示す |
| Procedural segmentation | 6ステップ(中心化 → 拡大 → 器具を開始点へ → 剥離1 → 再中心化 → 剥離2) |
| Signaling | 剥離線・目標リング・中央の円・方向矢印・警告(血管に近い/平面外/前線が視野外) |
| Scaffolded instruction | Guided → Faded → Assessment で手がかりを段階的に除去 |
| Cognitive aids | 手順チェックリストを常に表示し「今どこか」を示す |
| Part-task practice | カメラ操作と剥離だけを切り出し、両者の切り替え(止めてから視野を直す)を練習 |
| Debriefing | 軌跡図・ステップ別時間・改善点を1つだけ提示、NASA-TLX(Raw)を記録 |

## 記録(CSV)

デブリーフ画面の「CSVを保存」で、1回の試行を1ファイルに保存します(UTF-8 BOM 付き、Excel で開けます)。

| 列 | 内容 |
|---|---|
| `t_ms` | 課題開始からの経過時間(ミリ秒、固定60 Hzのシミュレーション時間) |
| `type` | `session_start` / `step_enter` / `step_complete` / `activate_start` / `activate_end` / `off_plane` / `vessel_injury` / `camera_move_start` / `front_out_of_view` / `hand_lost` などのイベント、10 Hz の軌跡 `sample`、集計 `summary`、`nasa_tlx` |
| `step` | その時点のステップ(`center` `zoom` `approach` `cut1` `recenter` `cut2`) |
| `detail` | `key=value;…` 形式の詳細(器具先端の座標、剥離線・血管までの距離 mm、カメラ位置と距離、手の形 など) |

主な集計値: 総所要時間、視野の準備時間、平均経路誤差(mm)、平面逸脱回数、血管損傷回数、前線が視野外だった時間、剥離中の視野の動かし直し回数、平均カメラ距離、推定出血量。

## Blender で術野を作る

1. `template/dissection-template.glb` を Blender で開く(File › Import › glTF 2.0)。
   - `Tissue`: 術野のメッシュ(名前が Tissue で始まるメッシュを器具が当たる面として使います。無ければ Vessel 以外の全メッシュ)
   - `Path_00` 〜 `Path_07`: 剥離線のエンプティ。番号順に結んだ線が剥離平面になります
   - `Vessel_00` 〜: 血管の中心線のエンプティ(または名前が Vessel で始まるメッシュ)。近づくと警告、触れると損傷
2. 形や質感を自由に作り直し、エンプティを動かして剥離線・血管を配置する。術野は上向き(Blender の +Z)に置きます。
3. File › Export › glTF 2.0 で **glTF Binary (.glb)** を選んで書き出す(Include › Custom Properties は不要)。
4. トレーナーの「Blenderで作った術野を使う」から .glb を読み込む。

単位はメートル(Blender の既定)のままで構いません。最大寸法が3未満のモデルはメートルとみなし、自動でセンチメートルに換算します。
許容幅は剥離線から ±3.5 mm、血管損傷は中心線から 5.5 mm 以内です。血管は剥離線から 1 cm 以上離して置くと、平面内で正しく剥離した場合に損傷判定が出ません。

## 研修医に配布する場合

ファイルを渡して各自のPCで開いてもらう方法のほか、HTTPS の静的サイト(GitHub Pages、Netlify など)に `dissection-trainer.html` を置けば URL を開くだけで使えます(webカメラは HTTPS か localhost でのみ動きます)。

## 開発者向け

- `src/page.html` が本体です。`./build.sh` で単体の `dissection-trainer.html` を生成します。
- `test/e2e.cjs` は、ファイルを `file://` で開く実際の使い方のまま、マウス操作の通しセッション・誤操作の検出・偽カメラでの MediaPipe 起動・MediaPipe 公式サンプル写真(握りこぶし・指差し・ピース・サムズアップ)でのジェスチャー判定・GLB の書き出しと読み込み・ダークテーマとスマホ幅の表示を確認します。
  CDN の代わりに同じ版のファイルを `VENDOR_DIR` から配信します(`npm pack @mediapipe/tasks-vision@0.10.14 three@0.128.0` と手のモデル・サンプル画像)。
  ```
  VENDOR_DIR=/path/to/vendor OUT_DIR=/tmp/shots node test/e2e.cjs
  ```
- 使用ライブラリ: three.js r128、@mediapipe/tasks-vision 0.10.14(いずれも CDN から読み込み)。

## 制約

- webカメラの手の認識は「開く・つまむ・握る」といった粗い動作に向きます。縫合のような微細運動や力加減は再現しません。
- 3D表示は教材用の簡易表現です。数値(許容幅、血管との距離、出血量とバイタルの関係)は開発用の値で、教育研究に使う前に臨床側のレビューで確定してください。
