# ② 困難な対話プラットフォーム（医療・看護・介護 / ACP・SPIKES）

`scenario_id`: `acp_dialogue_v1`

## 学習目標と準拠基準

- SPIKES プロトコル（悪い知らせを伝える 6 ステップ）
- 「人生の最終段階における医療・ケアの決定プロセスに関するガイドライン」（ACP／人生会議）

中核は **患者本人の意思と家族の希望が食い違う状況で、双方との関係を壊さずに
本人の意思決定を支える**こと。「正解の説明ができるか」ではなく
「対立を悪化させずに合意形成へ運べるか」を測る。

## 状態遷移（SPIKES）

```
Setting ─▶ Perception ─▶ Invitation ─▶ Knowledge ─▶ Emotion ─▶ Strategy ─▶ Completed
                                                        │
                                                        └─▶ Breakdown（対話決裂）
```

各ステップは `ProtocolStepCompleted` を送る。ステップを飛ばして先へ進んだ場合は
`ProtocolStepSkipped` エラーを記録する（進行自体は止めない。止めると
「何が悪かったか」を体験できないため、デブリーフィングで振り返らせる）。

## 隠れパラメータ

`patient`（本人：治療中止希望）と `family`（家族：延命希望）の 2 体に `AffectActor` を付ける。
選択肢は両者に**非対称**に効く。

| 選択の傾向 | patient | family |
|---|---|---|
| 本人の意思のみを肯定 | `Trust +20` | `Trust -10`, `Anxiety +15` |
| 家族の不安のみに応じる | `Trust -15` | `Trust +15`, `Anxiety -10` |
| 双方の懸念を言語化してから本人に確認 | `Trust +10` | `Trust +5`, `Anxiety -5` |
| 専門用語で押し切る | `Comprehension -20` | `Anxiety +10` |

| 閾値 | 対象 | 状態名 | 起きること |
|---|---|---|---|
| `Anxiety >= 80` | family | `Agitated` | 感情的激高。発言トーンが変わり、選択肢が制限される |
| `Trust <= 20` | patient | `Withdrawn` | 本人が語らなくなる |
| `Comprehension <= 30` | patient | `Confused` | 合意しても「理解を伴わない同意」として減点対象 |

**ドミノ倒し**: `family` が `Agitated` に入ると、以降の選択肢が
`patient` の `Trust` にも負の副作用を持つノードへ分岐する。片側だけを見て
対応すると両方を失う、という構造を対話グラフで表現する。

## 沈黙の扱い

悪い知らせの直後のノードに `silenceSeconds = 4` を設定する。

- 待てた場合 → `SilenceRespected`（加点）
- 待たずに選択した場合 → `DialogueSelected` に `InterruptedSilence` を付けて減点

これは共通基盤の `DialogueRunner` が自動で判定する。

## 評価ルーブリック

| objective_id | 測る対象 |
|---|---|
| `ACP-SET-01` | 面談環境・プライバシーの設定 |
| `ACP-PER-01` | 患者の病状認識の確認（説明の前に聞けたか） |
| `ACP-INV-01` | どこまで知りたいかの同意取得 |
| `ACP-KNW-01` | 専門用語を避けた段階的な情報提供（`JargonOverload` の回数） |
| `ACP-EMP-01` | 沈黙・感情への対応（`InterruptedSilence` の回数） |
| `ACP-STR-01` | 双方が納得する方針への到達 |
| `ACP-EOL-01` | 緩和ケア・人生の最終段階の医療を踏まえた説明 |
| `ACP-IPW-01` | 合意内容の多職種共有 |

**終了条件**

- `Completed`: 双方の `Trust >= 50` かつ `Strategy` に到達
- `Breakdown`: `family.Anxiety >= 95` または `patient.Trust <= 10`

## 主なログイベント

| event_type | payload の主な内容 |
|---|---|
| `ProtocolStepCompleted` | `step`, `patient_trust`, `family_anxiety` |
| `DialogueSelected` | `node`, `choice_index`, 各アクターの `trust` / `anxiety` |
| `SilenceRespected` | `node`, `waited_sec` |
| `AffectThresholdCrossed` | `actor`, `parameter`, `value`, `state` |

## 実装メモ

- SPIKES のステップ管理は `ScenarioController<SpikesState>` の状態遷移そのものに乗せる。
  ステップ用の別クラスを作らない。
- 「ステップを飛ばした」判定は、`ConfigureTransitions` で正規の順序だけを許可したうえで、
  飛ばす操作を `ForceTransitionTo` + `ProtocolStepSkipped` の記録として実装する。
- 患者と家族のパラメータ推移は `payload_json` に毎回両方載せる。
  片方だけだと、後からログを見ても「どちらを犠牲にしたか」が再現できない。
- アバターの演技切り替えは `AffectActor.ThresholdCrossed` を購読して行う。
  Animator のパラメータ名は `CurrentStateName` をそのまま使えるようにしておく。
