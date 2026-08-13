# ③ 夜勤・複数患者の優先順位判断 — Claude Code 入力用プロンプト

以下をそのまま Claude Code に貼る。

---

あなたは Unity 6 に習熟したシニアゲームエンジニアです。
夜勤看護の「複数患者・優先順位判断」を訓練するシミュレーションを、
**単体で完結する 1 フォルダ**として実装してください。

## 事前に読むもの

- `codemed-x/standalone/README.md`（作り方の約束事）
- `codemed-x/standalone/01-welfare-interview/` の 3 ファイル（**必ず読む**）
- `codemed-x/docs/scenarios/03-nightshift-triage.md`（設計の詳細）
- `codemed-x/schema/objectives.csv` の `nightshift_multi_v1` の行

## 作るもの

`codemed-x/standalone/03-nightshift-triage/` に 3 ファイル。

| ファイル | 役割 |
|---|---|
| `NightShiftSim.cs` | 時間進行・タスク管理・画面（`OnGUI`） |
| `NightShiftScenarioData.cs` | 時間台本・タスク定義・文言・配点 |
| `NightShiftTrainingLog.cs` | 学習履歴の記録 |

`scenario_id` は `nightshift_multi_v1`、名前空間は `CodemedX.NightShift`。

### 守ること（①と揃える）

- **空の GameObject にスクリプトを 1 つ付けて Play するだけで動くこと。**
  Canvas、Prefab、XR パッケージ、`.asmdef` は使わない。画面は IMGUI。
  **XRIT や Trigger Collider は使わない。** タスクへの着手はボタンで表現する。
  VR 化は内容が固まってからで間に合う。
- **`TrainingEvent` のフィールドは①と 1 文字も変えない。**
  `TrainingEvent` / `Payload` / ログクラスは①からコピーして名前空間だけ変える。
- `objective_id` は `NGT-*` のみ使う。
- 時間台本と配点は `NightShiftScenarioData` に持たせ、コードに直接書かない。
  演習ごとに秒数を変えて難易度を調整するため。

## 内容

このシナリオだけは**実時間で進む**。`Update()` で経過秒を見る。

### 時間台本（180 秒）

| 時刻 | 出来事 | 緊急度 | 放置の許容 |
|---|---|---|---|
| 10 秒 | 患者A の SpO2 が急激に低下（モニタのアラーム） | 10 | 15 秒 |
| 30 秒 | 患者B（認知症）がベッドサイドで立ち上がる（抜管・転倒リスク） | 8 | 12 秒 |
| 60 秒 | ナースステーションの電話（事務連絡） | 2 | — |
| 90 秒 | 複数病室からナースコールが同時点灯 | 5 | 30 秒 |

発生したタスクは画面に一覧で出し、それぞれ「対応する」ボタンを置く。
対応中は他のタスクの経過時間が進み続けることが分かるように表示する
（残り時間や経過秒を各タスクの横に出す）。

### 判定するのは 2 つだけ

**1. 優先順位の逆転** — より緊急なタスクが発生中なのに、緊急度が 1 以上低いタスクに着手した。
`TriageAction` を `PriorityInversion` として記録する。
payload に `chosen` / `chosen_priority` / `deferred` / `deferred_priority` /
`deferred_unattended_sec` を含める。

**2. 放置による重大化** — 許容時間を超えて未対応。
`TriageAction` を `CriticalTaskNeglected` として記録し、結果を画面に出す。

- 患者A を 15 秒以上放置 → 状態悪化
- 患者B を 12 秒以上放置 → 自己抜管が発生

重大化は一度だけ発火させる（毎フレーム記録しない）。

### エスカレーション

いつでも「医師に電話する」を選べるようにする。SBAR の 4 要素
（Situation / Background / Assessment / Recommendation）を選択で構成させ、
`EscalationPerformed` を `objective_id = "NGT-ESC-01"` で記録する。

payload に **急変発生からエスカレーションまでの秒数**（`time_to_decision_sec`）を必ず含める。
**これがこのシナリオでもっとも重要な指標。**
新人看護師の教育で問題になるのは知識ではなく「呼ぶのが遅れる」ことなので、
単独の数値として取り出せるようにする。

### 終了

180 秒経過、または全タスク解決で振り返りへ。
重大化したタスクが残ったまま終了した場合は失敗として記録する。

### 振り返りで見せるもの

- 各タスクの発生時刻・着手時刻・解決時刻（時系列で並べる）
- 優先順位逆転の回数と、そのときに後回しにしたもの
- 急変からエスカレーションまでの秒数
- 重大化したタスクとその結果

## 成果物

1. 上記 3 ファイル（そのままコンパイルが通るもの）
2. `codemed-x/standalone/README.md` の③の行を、①と同じ書式で埋める
3. 題材が監修前の仮版であることを、振り返り画面と README に明記する
