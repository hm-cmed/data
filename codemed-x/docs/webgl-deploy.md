# ブラウザ・タブレットに配る（WebGL ビルド）

①②④⑤ はすでに別々のシーンとして作ってある前提で、それを **1 つの WebGL ビルド**に
まとめて、PC のブラウザと iPad の Safari の両方から同じ URL で使ってもらう手順。

③（夜勤）はタブレットでも操作できるように作り直した
（`TouchControls.cs`。画面をドラッグする仮想パッド）。この章の手順でそのまま WebGL に入る。

初めて WebGL ビルドを作る前提で、省略せずに書く。すでに知っている手順は読み飛ばして構わない。

## 全体の流れ

```
1. 5 つのシーンの名前をそろえる
2. ランチャー（メニュー）シーンを作る
3. WebGL モジュールを入れる
4. Build Settings に 6 つのシーンを登録する
5. Player Settings を WebGL 向けに直す
6. ビルドする
7. 手元でざっと確認する
8. どこかに置く（ホスティング）
9. PC と iPad から開いて確認する
```

## 1. 5 つのシーンの名前をそろえる

すでに①②④⑤（と③）を別シーンとして作ってあるなら、シーン名を次の表と
**1 文字も違わず**そろえる（大文字・小文字も区別される）。名前が違うと、
あとで作るメニューのボタンを押しても反応しない。

| シーン名 | 付けるスクリプト |
|---|---|
| `01-WelfareInterview` | `WelfareInterviewSim` |
| `02-AcpDialogue` | `AcpDialogueSim` |
| `03-NightShiftTriage` | `NightShiftSim`（③のみ 3D 版なら病棟一式） |
| `04-PharmacistInquiry` | `PharmacistInquirySim` |
| `05-Gatekeeper` | `GatekeeperSim` |

名前を変えるには、Project ウィンドウでシーンのアセットを選んで **F2**（名前変更）。

**任意:** シナリオ側から一覧に戻れるようにしたい場合、各シーンに空の GameObject を作り
`standalone/presentation/ReturnToLauncherButton.cs` を付ける。画面右上に
「≡ メニュー」ボタンが出る。付けなくても動作に支障はない。

## 2. ランチャー（メニュー）シーンを作る

1. `File > New Scene`（Basic (Built-in) など空のテンプレートでよい）
2. `File > Save As` で **`00-Launcher`** という名前で保存する
3. シーンに空の GameObject を作る（**GameObject > Create Empty**）
4. `standalone/00-launcher/ScenarioLauncher.cs` をその GameObject にドラッグして付ける
5. Play して確認する。**Items** が空でも、既定の 5 本（① 〜 ⑤、上の表と同じシーン名）で
   自動的に埋まる。ボタンを押すと該当のシーンに切り替わる
   （この時点では Build Settings に未登録なので Console にエラーが出るのが正常。次の章で直す）

シーン名を上の表と変えた場合は、Inspector の **Items** を開き、
各行の **Scene Name** を実際のシーン名に書き換える。

日本語が □ になる場合は、Inspector の **Ui Font** に日本語を含むフォントを割り当てる
（詳しくは本ファイルの「日本語が表示されない」を参照）。

## 3. WebGL モジュールを入れる

Unity Hub でこのプロジェクトのバージョンに **WebGL Build Support** を追加する。

1. Unity Hub を開く → 左メニュー **Installs**
2. 使っているバージョン（Unity 6 / 6000.0 LTS）の **︙**（歯車の隣） → **Add Modules**
3. **WebGL Build Support** にチェック → **Install**

インストール済みなら、この章は不要。

## 4. Build Settings に 6 つのシーンを登録する

1. **File > Build Settings**
2. **Scenes In Build** に、Project ウィンドウから 6 つのシーンをドラッグして登録する。
   **`00-Launcher` を一番上（先頭）にする**（WebGL は一番上のシーンから起動するため）
3. 左下の **Platform** リストで **WebGL** を選び、**Switch Platform**
   （初回はしばらく時間がかかる）

登録し終えたら、いったんランチャーシーンで Play し、各ボタンでエラーが出なくなっているか確認する。

## 5. Player Settings を WebGL 向けに直す

**Build Settings** の左下 **Player Settings...** を開く。左のタブから WebGL のアイコン（📱のような形）を選ぶ。

### Resolution and Presentation

- **WebGL Template**: `Default` のままでよい（見た目にこだわるなら `Minimal` を自作してもよいが、まずは既定でよい）
- **Run In Background**: チェックを入れる（ブラウザのタブを切り替えても進行が止まらないようにする）

### Publishing Settings — ここがいちばん重要

- **Compression Format** を **`Disabled`** にする。

  **理由**: 既定の圧縮（Brotli）は、配置先のサーバが正しい HTTP ヘッダーを返さないと
  ブラウザが解凍できず、**白い画面のまま止まる**。これが WebGL 公開でいちばん多い失敗。
  `Disabled` にするとビルドのファイルサイズは大きくなるが、どんな静的ホスティングでも
  確実に動く。慣れてから `Gzip`（サーバ側で `.gz` に対応させれば安全に縮められる）を試す。

- **Decompression Fallback**: `Disabled` にした場合は関係ないので触らなくてよい

### Other Settings — ここも必須設定が 1 つある

- **Active Input Handling** を **`Both`** にする。**これは省略できない。**

  `SimpleWalker.cs` / `TouchControls.cs` は新 Input System と旧 Input Manager の
  両方に対応するコードを書いてあり、さらに「タッチ対応端末か」の判定
  （`Input.touchSupported`）を `#if` で囲まずに直接呼んでいる。
  ここが `Input System Package (New)` だけになっていると、この呼び出しが
  **実行時に例外を投げて止まる。** 既定では `Input Manager (Old)` のことが多いので、
  ここを確認して `Both` に変更する。

- **Auto Graphics API** のチェックを外し、**`WebGL2`** だけを残す
  （複数の Graphics API を自動選択させると、ブラウザによって挙動が揺れることがあるため）

- **Color Space**: 既定（Linear）のままでよい
- **Memory Size**: 今回の 5 本は IMGUI 中心で軽いので、既定値のままで足りるはず。
  ③の 3D 病棟を大きくした場合だけ、動作が重ければ増やす

**このプロジェクトに WebXR（ブラウザで VR ヘッドセット表示）は不要。**
①〜⑤は VR 前提ではなく PC ブラウザ + iPad のタッチ操作を想定しているうえ、
**iPad の Safari はそもそも WebXR に対応していない**（Apple 未対応）。
WebXR Exporter 等のパッケージを見かけても、今回の目的には使わないので入れなくてよい。

### WebXR のチュートリアルを先に試した場合、戻す設定

WebXR（ブラウザで VR ヘッドセット表示）のチュートリアルを先に試したプロジェクトを
そのまま使う場合、次を確認する。入れたままでも動くことはあるが、
**WebGL Template だけは必ず確認する**（表示が崩れる原因になりやすい）。

| 場所 | 戻す内容 |
|---|---|
| Player Settings > Resolution and Presentation > **WebGL Template** | VR 用テンプレート（例: `WebXR2020`）になっていたら **`Default`** に戻す |
| Project Settings > **XR Plug-in Management** > WebGL タブ | **Plug-in Providers** のチェックを外す（起動時に XR を初期化させない） |
| Build Settings > **Scenes In Build** | WebXR チュートリアルのサンプルシーン（例: `Desert.unity`）が残っていれば外し、`00-Launcher` + ①〜⑤の 6 シーンだけにする |
| Package Manager > My Registries > OpenUPM | 使わないなら `WebXR Export` / `WebXR Interactions` を Remove |
| Project ウィンドウ | `Assets/Samples/WebXR Interactions` 等のサンプルフォルダが残っていれば削除 |
| Project Settings > Package Manager > Scoped Registries | 上記パッケージ削除後なら、OpenUPM の登録は削除してよい（残しても実害は無い） |

**Active Input Handling = Both / Auto Graphics API OFF・WebGL2 のみ / Compression Format = Disabled は戻さない。**
これらは WebXR とは関係なく、このプロジェクトのコードに必要な設定として上でそのまま説明している。

## 6. ビルドする

1. **Build Settings** に戻り、**Build**（または **Build And Run**）
2. 保存先フォルダを選ぶ（例: プロジェクトの外に `webgl-build` フォルダを新規作成）
3. しばらく待つ（数分〜。プロジェクトの規模による）

できあがったフォルダの中身はそのまま**静的ファイル一式**（`index.html` を含む）。
これをどこかの Web サーバに置けば、そのままアクセスできる。

## 7. 手元でざっと確認する

**`index.html` をブラウザで直接ダブルクリックして開いても動かない。**
ブラウザのセキュリティ制限で、`file://` からは WebGL ビルドを読み込めない。
必ず簡易サーバを立てて確認する。

ビルドしたフォルダで、ターミナルから:

```
python3 -m http.server 8000
```

ブラウザで `http://localhost:8000` を開く。これで PC 上での動作確認ができる
（iPad からの確認は次の「ホスティング」まで進めてから行う）。

## 8. ホスティング

まったく初めてなら **itch.io** がいちばん簡単（アカウント登録だけで、サーバの知識が要らない）。

### itch.io を使う場合

1. ビルドしたフォルダの中身を **zip 圧縮**する（フォルダごとではなく、
   `index.html` が zip の直下に来るように圧縮する）
2. [itch.io](https://itch.io) でアカウントを作り、**Upload new project**
3. **Kind of project** を **HTML** にする
4. 作った zip をアップロードし、**This file will be played in the browser** にチェック
5. 保存して公開すると URL が発行される。PC でも iPad でも、その URL を開くだけでよい

### 自前のサーバ（GitHub Pages 等）に置く場合

- ビルドフォルダの中身をそのままアップロードする
- 圧縮を `Disabled` にしてあれば、サーバ側の追加設定は不要
- `Gzip` にした場合は、`.unityweb` / `.gz` 拡張子に対して
  `Content-Encoding: gzip` を返すようサーバ側で設定する必要がある
  （ここでつまずくことが多いので、まずは `Disabled` を勧める）

## 9. PC と iPad から開いて確認する

- **PC**: Chrome / Edge / Safari で URL を開く。マウス + キーボードでそのまま操作できる
- **iPad**: Safari（iOS/iPadOS 15 以降）で URL を開く。
  ①②④⑤ は画面のボタンをタップ、③はドラッグで移動・視点、近づいたボタンはタップで対応する

**iPad で「ホーム画面に追加」しておくと使いやすい。**
Safari の共有ボタン → **ホーム画面に追加**。アイコンから起動すると
アドレスバー等が消え、アプリのような全画面表示になる。

## 日本語が表示されない（□ になる）

WebGL は OS のフォントを読みに行けないため、Unity 側にフォントを埋め込む必要がある。

各シナリオのスクリプト（`WelfareInterviewSim` など）とランチャー（`ScenarioLauncher`）の
Inspector に **Ui Font** の欄がある。日本語を含む TrueType フォント
（例: [Noto Sans JP](https://fonts.google.com/noto/specimen/Noto+Sans+JP) など、
再配布条件を確認した上で使えるもの）を `Assets/` に取り込み、そこへ割り当てる。
**割り当てたら再ビルドが必要。**

## よくある不具合

| 症状 | 原因と対処 |
|---|---|
| 読み込みバーが最後まで進まない／白い画面のまま | 圧縮設定が原因のことが多い。Player Settings の **Compression Format** を **Disabled** にして再ビルド |
| 文字が □ になる | 上の「日本語が表示されない」を参照。**Ui Font** 未設定のまま WebGL に持っていくと必ず起きる |
| iPad でボタンが反応しない／視点が回らない | ③ は `TouchControls` が付いているか確認（`NightShiftHospitalBuilder` で生成すれば自動）。①②④⑤ は通常のボタンなのでタップで反応するはず |
| iPad で長時間プレイすると落ちる | iPad Safari はメモリに厳しい。他の Safari タブを閉じる、360度画像のような重いテクスチャを避ける（[docs/visuals.md](visuals.md) 参照） |
| ランチャーのボタンを押しても切り替わらない | シーン名の不一致が最多。Build Settings に登録した名前と、`ScenarioLauncher` の **Scene Name** が 1 文字も違わず一致しているか確認 |
| ③ を開いた瞬間に固まる／Console に `InvalidOperationException`（Input 関連） | **Active Input Handling** が `Input System Package (New)` だけになっている。Player Settings > Other Settings で `Both` に変更して再ビルド |
| 学習履歴の送信でエラーになる（Console に CORS 関連の表示） | ログ送信先（GAS 等）がブラウザからのアクセスに CORS ヘッダーを返していない。送信先の設定を見直す。ローカルでの動作確認だけならログ送信を無効にしてもよい |

## 5 本を別々に配る場合との違い

このガイドはランチャー経由で 1 つの URL にまとめる方法。
演習ごとに 1 本だけ渡したい（他のシナリオを見せたくない）場合は、
ランチャーを使わず、シナリオ 1 本だけを含む WebGL ビルドを 5 回作ってもよい。
その場合は Build Settings にそのシナリオのシーン 1 つだけを登録する。
コード側の変更は不要（`standalone/README.md` の「1 シーンで動かせるのは 1 つだけ」を参照）。
