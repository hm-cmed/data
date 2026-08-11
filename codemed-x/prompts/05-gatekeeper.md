# ⑤ ゲートキーパー：自殺リスク評価と危機介入 — Claude Code 入力用プロンプト

以下をそのまま Claude Code に貼る。

---

あなたは Unity 6 (URP) に習熟したシニアゲームエンジニアです。
Codemed-x プロジェクトの共通基盤の上に、自殺防止ゲートキーパー研修用の
「希死念慮アセスメントおよび危機介入対話システム」を実装してください。

## 事前に読むもの

- `codemed-x/CLAUDE.md`
- `codemed-x/docs/architecture.md`
- `codemed-x/docs/scenarios/05-gatekeeper.md`（**取り扱い上の注意を必ず読むこと**）
- `codemed-x/schema/objectives.csv` の `gatekeeper_crisis_v1` の行

## 共通基盤は実装済みです。再実装せず利用してください

- `CodemedX.Scenarios.ScenarioController<TState>` / `ScenarioStateMachine<TState>`
- `CodemedX.Dialogue.DialogueRunner` / `DialogueGraph` / `DialogueChoice`
- `CodemedX.Affect.AffectActor` / `AffectParameter.Trust` / `AffectParameter.Urgency` /
  `AffectThreshold` / `AffectCondition`
- `CodemedX.Logging.EventLogger`、`CodemedX.Core.EventTypes` / `ErrorTypes` / `PayloadBuilder`

**選択肢の前提条件チェックは `DialogueRunner` に実装済みです。**
`DialogueChoice.requirements` に `AffectCondition` を設定すれば、条件未達で
選んだ場合に `prematureErrorType` が自動で記録されます。

## 実装するもの

### 1. `GatekeeperDialogueManager.cs`

`ScenarioController<GatekeeperState>` を継承する。

```csharp
public enum GatekeeperState
{
    Approach, Listening, RiskAssessment, SafetyPlanning, Referral,
    SuccessConnection, Withdrawn, Aborted
}
```

`Withdrawn`（相手が心を閉ざした状態）は `AllowFromAny` にし、
そこから `Listening` へ戻れるようにする。一度の失言で終わりにはしない
（実務でも関係の立て直しは可能であり、その練習に価値がある）。ただし
回復には時間を消費する構造にする。

### 2. 相手の内部モデル

相手（生徒／住民）`resident` に `AffectActor` を付け、
**`Trust`（0-100）と `Urgency`（0-100）**を使う。増減は `DialogueChoice.effects` にデータで持たせる。

| 学習者の対応 | Trust | Urgency |
|---|---|---|
| 安易な励まし（「頑張って」「死ぬなんて言わないで」） | `-30` | `+20` |
| 説教・正論（「家族が悲しむよ」） | `-25` | `+15` |
| 受容的な傾聴（「そう感じているんですね」） | `+15` | `-5` |
| 具体的な事実の確認（睡眠・食事・出来事） | `+10` | `0` |
| 希死念慮の直接確認（適切なタイミング） | `+20` | `0` |

安易な励ましの選択肢には `errorType = ErrorTypes.InappropriateEncouragement` を設定する。

`AffectThreshold`:

- `Trust <= 25` → `Withdrawn`。「もういいです」で開示が止まる
- `Trust >= 50` → `Open`
- `Trust >= 70` → `OpenHeart`。計画・手段・支援の有無を自ら語り始める

**`Urgency` は実行中の UI に絶対に表示しないこと。**
安易な励ましは相手を黙らせるだけでなく、表面上は落ち着いたように見えるまま
切迫度を上げる。この乖離をデブリーフィングで初めて開示することが教材の核心。
実行中に見えると、数値を見て操作する練習になり、かつ「切迫度が見える」という
誤った前提を学習させてしまう。

### 3. 直接的な問いかけ

「死にたいと考えていますか？」の選択肢に
`AffectCondition(resident, Trust, AtLeast, 50)` を `requirements` として設定する。

- 条件を満たして選択 → `OpenHeart` へ遷移し、アセスメント項目が確認可能になる
- 条件を満たさずに選択 → `prematureErrorType = ErrorTypes.PrematureProbing`、`Trust -15`
- **最後まで選択しなかった** → 完了時に `ErrorTypes.RiskAssessmentIncomplete` を記録する

3 つ目を必ず実装すること。尋ねないことは「安全な選択」ではなく**不作為の誤り**であり、
ゲートキーパー研修でもっとも実行されにくい行動がこれにあたる。

この選択肢は `hideWhenUnavailable = false` にする。条件未達でも UI に出し、
学習者自身が「今聞くべきか」を判断する構造にしないと訓練にならない。

### 4. アセスメント項目

`bool` フラグとして保持し、対話ルートから自動検出する。
立った時点で `EventTypes.AssessmentItemChecked` を送る。

| フラグ | 確認内容 | objective_id |
|---|---|---|
| `hasSuicidalIdeation` | 希死念慮の直接確認 | `GTK-ASK-01` |
| `hasPlan` | 具体的な計画の有無 | `GTK-RSK-01` |
| `hasMeansAccess` | 手段へのアクセス | `GTK-RSK-01` |
| `hasPreviousAttempt` | これまでの未遂歴 | `GTK-RSK-01` |
| `hasSupport` | 周囲のサポート・孤立の程度 | `GTK-WCH-01` |
| `hasSafetyPlan` | 安全の約束 | `GTK-WCH-01` |
| `hasReferral` | 専門相談窓口への接続合意 | `GTK-CON-01` |

### 5. 完了判定

`hasSafetyPlan && hasReferral` が両方成立した場合のみ `SuccessConnection` として
`FinishScenario(true)`。それ以外はすべて未完了とし、`FinishScenario(false)` のうえで
不足項目をデブリーフィングに提示する。

完了時、全アセスメントのチェック状況を `payload_json` に載せる
（`PayloadBuilder` で各フラグを `bool` として追加する）。

## 取り扱い上の注意（実装に反映すること）

- 台詞は**必ず専門家の監修を経る**前提で、すべて `DialogueGraph` アセットに置く。
  C# に文言を埋め込まない。監修者が直接修正できる状態を保つこと。
- **学習者自身が当事者である可能性を前提にする。** シナリオ開始前と終了後に
  相談窓口の案内を常設 UI として表示し、シナリオの成否に関わらず出す。
  中断はいつでも可能にする。
- 相手の描写を特定の属性・疾患に紐づけない。「精神疾患のある人は危険／特別」
  という誤ったスキーマを強化しないこと。

## 守ること

- デリケートな分岐ロジックを、モジュールごとに明確に分離した構造にする
  （対話進行 / アセスメント集計 / 完了判定 を別クラスに分ける）。
- `[SerializeField] private` を使う。`EventTypes` / `ErrorTypes` の定数を使う。
- `objective_id` は `GTK-*` のみ。`UnityWebRequest` を直接呼ばない。
- Unity 6 で非推奨の API を使わない。
- アセット追加後に `python3 codemed-x/tools/generate_unity_meta.py` を実行する。

## 成果物

1. `GatekeeperDialogueManager.cs`（完全にコンパイル可能なもの）
2. アセスメント集計クラスと、相談窓口の常設案内 UI
3. サンプルの `DialogueGraph`（気づき → 傾聴 → 直接確認 → つなぎ の最小構成）
4. EditMode テスト — 安易な励ましによる `Trust` 急落と `Urgency` 上昇、
   条件未達の直接確認が `PrematureProbing` になること、
   未実施の場合に `RiskAssessmentIncomplete` が記録されることをカバーする
5. シーン構成手順を `docs/scenarios/05-gatekeeper.md` の末尾に追記
