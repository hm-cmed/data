# ① 相談援助面接シミュレーター — Claude Code 入力用プロンプト

以下をそのまま Claude Code に貼る。

---

あなたは Unity 6 (URP)、OpenXR、XR Interaction Toolkit 3.x に習熟したシニアゲームエンジニアです。
Codemed-x プロジェクトの共通基盤の上に、児童相談所の家庭訪問における
「児童虐待・ネグレクト環境アセスメント」シミュレータを実装してください。

## 事前に読むもの

- `codemed-x/CLAUDE.md`（コーディング規約）
- `codemed-x/docs/architecture.md`（共通基盤の設計）
- `codemed-x/docs/scenarios/01-welfare-interview.md`（本シナリオの詳細設計）
- `codemed-x/schema/objectives.csv` の `welfare_abuse_v1` の行（評価項目）

## 共通基盤は実装済みです。再実装せず利用してください

`codemed-x/unity/Assets/CodemedX/Runtime/` に以下があります。

- `CodemedX.Scenarios.ScenarioController<TState>` / `ScenarioStateMachine<TState>` / `ScenarioDefinition`
- `CodemedX.Logging.EventLogger`（唯一のログ送信口。バッチ化・再送・オフライン退避を内包）
- `CodemedX.Core.EventTypes` / `ErrorTypes` / `PayloadBuilder`
- `CodemedX.Affect.AffectActor` / `AffectState` / `AffectThreshold`
- `CodemedX.Dialogue.DialogueRunner` / `DialogueGraph`
- `CodemedX.Observation.ObservationTarget` / `GazeDwellTracker`
- `CodemedX.Assessment.ScoreCard` / `ObjectiveCatalog`

## 実装するもの

### 1. `WelfareScenarioManager.cs`

`ScenarioController<WelfareState>` を継承する。

```csharp
public enum WelfareState
{
    Preparation, Observation, Interview, Assessment, Escalation, Completed, Aborted
}
```

遷移表は `ConfigureTransitions` で宣言する。
`Preparation → Observation → Interview → Assessment → Escalation → Completed` の順のみ許可し、
`Aborted` は `AllowFromAny`。順序を飛ばす遷移は基底が拒否する。

### 2. 空間観察フェーズ（Observation）

- リスクサインは `ObservationTarget`（Tag: `AbuseRiskSign`）として配置する。
  `requiredDwellSeconds = 2`、重大なものは `isCritical = true`。
- `GazeDwellTracker` を XR Origin に 1 つ付ける。レイキャストの起点は
  未設定なら `Camera.main` になる。XRIT 3.x の Ray Interactor を使う場合は
  `SetRayOrigin()` にその Transform を渡す（Interactor の型には依存させない）。
- `Observation` を抜けるとき、未発見の `ObservationTarget` すべてに
  `ReportMissed()` を呼ぶ。**見落としは「イベントが無いこと」ではなく明示的に記録する。**

### 3. 面談フェーズ（Interview）

- `DialogueRunner` + `DialogueGraph`（ScriptableObject）で構成する。
- 保護者 `guardian` に `AffectActor` を付け、`Trust`（初期値 40）を使う。
  威圧的な選択で下がり、共感的な選択で上がる（増減は `DialogueChoice.effects` にデータで持たせる）。
- `AffectThreshold` を 2 つ設定する。
  - `Trust <= 30` → 状態名 `Shutdown`。`ThresholdCrossed` を購読して面談を強制終了し、
    `Assessment` へ遷移する（情報が欠けたまま判定させる）。
  - `Trust >= 70` → 状態名 `Disclosing`。追加情報ノードを解放する。
- 高圧的な選択肢には `errorType = ErrorTypes.CombativeSpeech` を設定する。
- **感情値スライダーは面談中には表示しない**。デブリーフィング画面でのみ開示する
  （面談中に見えると、相手の反応ではなく数値を見て操作する練習になる）。

### 4. 判定フェーズ（Assessment）

- 「一時保護が必要か」の判定と、「その根拠（複数選択）」を**別々の UI** で取る。
  判定だけ合っていて根拠が伴わない場合を区別するため。
- 提出時に `EventTypes.AssessmentSubmitted` を `objective_id = "WLF-LAW-01"` で送る。
  payload に `decision`、`grounds`、`correct` を含める。
- 要件を満たすのに保護不要と判定した（またはその逆の）場合は
  `ErrorTypes.UnjustifiedCustodyDecision` を付ける。

### 5. 報告フェーズ（Escalation）

- 上司（AI）への報告を UI の選択で構成し、`EventTypes.EscalationPerformed` を
  `objective_id = "WLF-ESC-01"` で送る。payload に含めた要素と所要時間を残す。
- 完了時は `FinishScenario(success)` を呼ぶ。基底が `ScoreCard` のサマリを
  `ScenarioCompleted` として送信する（発見数・感情値推移・最終判断が含まれる）。

## 守ること

- `[SerializeField] private` を使う。インスペクターで観察対象・UI・対話グラフ・
  感情値表示（デブリーフィング用）を設定可能にする。
- Unity 6 の最新 API に準拠する（`rigidbody.velocity` → `linearVelocity`、
  `FindObjectOfType` → `FindAnyObjectByType`）。
- `event_type` / `error_type` は `EventTypes` / `ErrorTypes` の定数を使う。
  必要な種別が無ければまずカタログに追加する。
- `objective_id` は `objectives.csv` の `WLF-*` のみを使う。追加したら
  `python3 codemed-x/tools/validate_codemedx.py` を通す。
- `UnityWebRequest` を直接呼ばない。ログ送信は基底の `LogEvent(...)` 経由のみ。
- 台詞・分岐・閾値は C# に書かず ScriptableObject に持たせる。
- アセット追加後に `python3 codemed-x/tools/generate_unity_meta.py` を実行する。

## 成果物

1. `WelfareScenarioManager.cs`（完全にコンパイル可能なもの）
2. 面談 UI と判定 UI のコンポーネント
3. サンプルの `DialogueGraph` アセット（保護者の防衛的な応答から始まる最小構成でよい）
4. EditMode テスト — 遷移の拒否、`Trust` 30 以下での強制終了、
   未発見対象の `ReportMissed` が記録されることをカバーする
5. シーン構成手順を `docs/scenarios/01-welfare-interview.md` の末尾に追記
