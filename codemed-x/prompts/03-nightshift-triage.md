# ③ 夜勤・複数患者の優先順位判断 — Claude Code 入力用プロンプト

以下をそのまま Claude Code に貼る。

---

あなたは Unity 6 (OpenXR / XR Interaction Toolkit 3.x) に習熟したシニアゲームエンジニアです。
Codemed-x プロジェクトの共通基盤の上に、夜勤看護シミュレータの
`NurseWorkloadScheduler` と評価システムを実装してください。

## 事前に読むもの

- `codemed-x/CLAUDE.md`
- `codemed-x/docs/architecture.md`
- `codemed-x/docs/scenarios/03-nightshift-triage.md`
- `codemed-x/schema/objectives.csv` の `nightshift_multi_v1` の行

## 共通基盤は実装済みです。再実装せず利用してください

- `CodemedX.Timing.ScenarioClock` — 指定秒数での自律イベント発火（`UnityEvent`）
- `CodemedX.Timing.PriorityTask` / `PriorityTaskBoard` — **優先順位逆転と放置の判定は実装済み**
- `CodemedX.Scenarios.ScenarioController<TState>` — 制限時間とタイムアウト処理を内包
- `CodemedX.Logging.EventLogger`、`CodemedX.Core.EventTypes` / `ErrorTypes` / `PayloadBuilder`

`PriorityTaskBoard` が既に以下を自動で行います。**自前で書かないでください。**

- `AttendTo(task)` を呼ぶと、より緊急なタスクを差し置いていた場合に
  `ErrorTypes.PriorityInversion` を記録する
- 許容時間を超えて未対応のタスクを `ErrorTypes.CriticalTaskNeglected` として
  記録し、`TaskEscalated` イベントを発火する
- `Resolve(task)` で解決を記録し、着手までの秒数を残す

## 実装するもの

### 1. `NurseWorkloadScheduler.cs`

`ScenarioController<NightShiftState>` を継承する。

```csharp
public enum NightShiftState { Briefing, Rounds, Escalation, Debrief, Aborted }
```

- 制限時間 180 秒は `ScenarioDefinition.timeLimitSeconds` に設定する
  （タイムアウト処理は基底が行う。`Update` で自前に測らないこと）。
- `Escalation` はどの状態からでも入れるようにする（`AllowFromAny`）。

### 2. 時間台本

`ScenarioClock` のトリガーとして **Inspector のデータで**定義する。
C# にハードコードしないこと（演習ごとに秒数を変えて難易度調整するため）。

| 時刻 | イベント | `PriorityTask.priority` | 放置許容 |
|---|---|---|---|
| 10 秒 | 患者A の SpO2 が急激に低下（生体モニタ点滅） | 10 | 15 秒 |
| 30 秒 | 患者B（認知症）がベッドサイドで立ち上がる（抜管・転倒リスク） | 8 | 12 秒 |
| 60 秒 | ナースステーションの電話が鳴る（事務連絡） | 2 | — |
| 90 秒 | 複数病室からナースコールが同時点灯 | 5 | 30 秒 |

各トリガーの `UnityEvent` から、対応する `PriorityTask.Activate()` と
演出（モニタ点滅・アラーム音・ナースコールランプ）を呼ぶ。

### 3. 「着手した」の検知

`PriorityTaskBoard.AttendTo(task)` を呼ぶトリガーをシナリオ側で実装する。

- ベッドサイドの Trigger Collider への XR Origin の侵入
- 対象オブジェクトの Grab（XRIT 3.x の Interactable の `selectEntered`）
- 器材 UI の操作

XRIT 3.x の名前空間は `UnityEngine.XR.Interaction.Toolkit.Interactables` /
`.Interactors` です（2.x から変わっています）。

### 4. エスカレーション

- 医師への SBAR 電話・RRS 要請を実装し、`EventTypes.EscalationPerformed` を
  `objective_id = "NGT-ESC-01"` で送る。
- payload に **急変発生からエスカレーションまでの秒数**（`time_to_decision_sec`）を必ず含める。
  これがこのシナリオでもっとも重要な指標。
- SBAR の各要素（Situation / Background / Assessment / Recommendation）を
  選択で構成させ、充足状況を payload に残す。

### 5. 重大アクシデント

`PriorityTaskBoard.TaskEscalated` を購読し、以下を実装する。

- 患者A の SpO2 低下を 15 秒以上放置 → 状態悪化の演出
- 患者B を 12 秒以上放置 → 自己抜管の発生
- いずれかが未回復のまま制限時間終了 → `FinishScenario(false)`

## 守ること

- 非同期処理は**コルーチン**で書く。XRIT 3.x のイベントと組み合わせる場合、
  `async/await` はシーン遷移時のキャンセル漏れを起こしやすい。
- `[SerializeField] private` を使う。
- `EventTypes` / `ErrorTypes` の定数を使う。`objective_id` は `NGT-*` のみ。
- `UnityWebRequest` を直接呼ばない。ログは基底の `LogEvent(...)` 経由。
- Unity 6 で非推奨の API を使わない（`rigidbody.velocity` → `linearVelocity`）。
- 移動方式は `continuous`。`ScenarioDefinition` の `defaultLocomotionMode` に設定する。
- アセット追加後に `python3 codemed-x/tools/generate_unity_meta.py` を実行する。

## 成果物

1. `NurseWorkloadScheduler.cs`（完全にコンパイル可能なもの）
2. 着手検知コンポーネント（Trigger 侵入 / Grab）
3. 生体モニタとナースコールの表示コンポーネント
4. EditMode テスト — 優先順位逆転の記録、放置による重大化、
   タイムアウト時の `ScenarioFailed` をカバーする
5. シーン構成手順を `docs/scenarios/03-nightshift-triage.md` の末尾に追記
