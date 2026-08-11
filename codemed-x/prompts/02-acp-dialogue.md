# ② 困難な対話（ACP / SPIKES）— Claude Code 入力用プロンプト

以下をそのまま Claude Code に貼る。

---

あなたは Unity 6 (URP) に習熟したシニアゲームエンジニアです。
Codemed-x プロジェクトの共通基盤の上に、終末期 ACP（人生会議）の
「困難対話用マルチエージェント意思決定システム」を実装してください。

## 事前に読むもの

- `codemed-x/CLAUDE.md`
- `codemed-x/docs/architecture.md`
- `codemed-x/docs/scenarios/02-acp-dialogue.md`
- `codemed-x/schema/objectives.csv` の `acp_dialogue_v1` の行

## 共通基盤は実装済みです。再実装せず利用してください

- `CodemedX.Scenarios.ScenarioController<TState>` / `ScenarioStateMachine<TState>`
- `CodemedX.Dialogue.DialogueRunner` / `DialogueGraph` / `DialogueNode` / `DialogueChoice`
- `CodemedX.Affect.AffectActor` / `AffectState` / `AffectParameter` / `AffectThreshold` / `AffectEffect`
- `CodemedX.Logging.EventLogger`、`CodemedX.Core.EventTypes` / `ErrorTypes` / `PayloadBuilder`
- `CodemedX.Assessment.ScoreCard`

**沈黙の判定は `DialogueRunner` に実装済みです。**
`DialogueNode.silenceSeconds` を設定するだけで、待てた場合は `SilenceRespected`、
遮った場合は `ErrorTypes.InterruptedSilence` が自動で記録されます。自前で書かないでください。

## 実装するもの

### 1. `AcpDialogueManager.cs`

`ScenarioController<SpikesState>` を継承する。

```csharp
public enum SpikesState
{
    Setting, Perception, Invitation, Knowledge, Emotion, Strategy, Completed, Breakdown
}
```

- 遷移表は SPIKES の正規の順序のみを許可する。
- 各ステップ完了時に `EventTypes.ProtocolStepCompleted` を、対応する
  `objective_id`（`ACP-SET-01` 〜 `ACP-STR-01`）付きで送る。
- ステップを飛ばす操作は `ForceTransitionTo` + `ErrorTypes.ProtocolStepSkipped` の
  記録として実装する。**進行自体は止めない**（何が悪かったかを体験させ、
  デブリーフィングで振り返らせるため）。

### 2. 患者と家族の非対称な感情モデル

`patient`（本人：治療中止希望）と `family`（家族：延命希望）の 2 体に `AffectActor` を付ける。
使うパラメータは `Trust` / `Anxiety` / `Comprehension`。

選択肢の影響は `DialogueChoice.effects`（`AffectEffect` のリスト）として
**データで**表現する。C# に増減値を書かないこと。例:

| 選択の傾向 | patient | family |
|---|---|---|
| 本人の意思のみを肯定 | `Trust +20` | `Trust -10`, `Anxiety +15` |
| 家族の不安のみに応じる | `Trust -15` | `Trust +15`, `Anxiety -10` |
| 双方の懸念を言語化してから本人に確認 | `Trust +10` | `Trust +5`, `Anxiety -5` |
| 専門用語で押し切る | `Comprehension -20` | `Anxiety +10` |

`AffectThreshold` を設定する。

- family `Anxiety >= 80` → 状態名 `Agitated`。**感情的激高状態**。
  `ThresholdCrossed` を購読して発言トーンを切り替え、選択肢を制限する。
- patient `Trust <= 20` → `Withdrawn`
- patient `Comprehension <= 30` → `Confused`（合意しても「理解を伴わない同意」として扱う）

**ドミノ倒し**: family が `Agitated` に入ったら、以降は patient の `Trust` にも
負の副作用を持つノード群へ分岐させる。片側だけを見て対応すると両方を失う構造を
対話グラフのデータで表現すること。

### 3. 沈黙の設定

悪い知らせを伝えた直後のノードに `silenceSeconds = 4` を設定する。
実装は不要（`DialogueRunner` が処理する）。データとして設定するだけ。

### 4. 終了判定

- `Completed`: 双方の `Trust >= 50` かつ `Strategy` に到達 → `FinishScenario(true)`
- `Breakdown`: family `Anxiety >= 95` または patient `Trust <= 10` → `FinishScenario(false)`

基底が `ScoreCard` のサマリを送信する。加えて、**両アクターのパラメータ推移を
毎回 `payload_json` に載せる**こと（片方だけだと、後からログを見ても
「どちらを犠牲にしたか」が再現できない）。

## 守ること

- ダイアログノードは `ScriptableObject`（`DialogueGraph`）で管理する。
  監修者が Unity 上で文言と増減値を編集できる構造にすること。
- `[SerializeField] private` を使う。`public` フィールドは JSON DTO のみ。
- `EventTypes` / `ErrorTypes` の定数を使う。`objective_id` は `ACP-*` のみ。
- `UnityWebRequest` を直接呼ばない。
- Unity 6 で非推奨の API を使わない。
- アセット追加後に `python3 codemed-x/tools/generate_unity_meta.py` を実行する。

## 成果物

1. `AcpDialogueManager.cs`（完全にコンパイル可能なもの）
2. アバターの演技状態を `AffectActor.CurrentStateName` から切り替えるコンポーネント
3. サンプルの `DialogueGraph`（SPIKES 6 ステップ分の最小構成）
4. EditMode テスト — 非対称な感情変動、`Agitated` への遷移、
   ステップスキップが `ProtocolStepSkipped` として記録されることをカバーする
5. シーン構成手順を `docs/scenarios/02-acp-dialogue.md` の末尾に追記
