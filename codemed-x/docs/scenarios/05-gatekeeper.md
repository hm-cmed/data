# ⑤ 保健・公衆衛生：ゲートキーパー／自殺リスク評価と危機介入対話

`scenario_id`: `gatekeeper_crisis_v1`

## 学習目標と準拠基準

- 厚生労働省 ゲートキーパー養成研修用テキストの 4 要素
  **気づき・傾聴・つなぎ・見守り**
- 自殺総合対策大綱
- 精神保健及び精神障害者福祉に関する法律（相談窓口・専門機関への接続）

中核は **「安易に励まさずに聴き、直接尋ね、確実につなぐ」**こと。
善意の励ましが相手のシャッターを閉じさせる、という反直感的な構造を体験させる。

## 状態遷移

```
Approach ─▶ Listening ─▶ RiskAssessment ─▶ SafetyPlanning ─▶ Referral ─▶ SuccessConnection
                │                                                │
                └────────────▶ Withdrawn（相手が心を閉ざす）◀────┘
```

`Withdrawn` に入っても即終了にはせず、関係を立て直す選択肢を残す
（実務でも一度の失言で終わりではないため）。ただし `Trust` の回復には
`Listening` の局面をやり直す必要があり、時間を消費する。

`SuccessConnection` に到達できるのは、**安全の約束（セーフティプラン）と
専門機関への接続の両方**が成立した場合のみ。

## 隠れパラメータ

相手（生徒／住民）`resident` に `AffectActor` を付ける。

| 学習者の対応 | Trust | Urgency |
|---|---|---|
| 安易な励まし（「頑張って」「死ぬなんて言わないで」） | `-30` | `+20` |
| 説教・正論（「家族が悲しむよ」） | `-25` | `+15` |
| 受容的な傾聴（「そう感じているんですね」） | `+15` | `-5` |
| 具体的な事実の確認（睡眠・食事・出来事） | `+10` | `0` |
| 希死念慮の直接確認（適切なタイミング） | `+20` | `0`（**可視化される**） |

`Urgency` は**学習者に見えない**。安易な励ましは相手を黙らせるだけでなく、
表面上「落ち着いたように見える」まま切迫度が上がる。この乖離を
デブリーフィングで開示することが、この教材の核心にあたる。

| 閾値 | 状態名 | 起きること |
|---|---|---|
| `Trust <= 25` | `Withdrawn` | 「もういいです」で開示が止まる |
| `Trust >= 50` | `Open` | 直接的な問いかけが有効になる |
| `Trust >= 70` | `OpenHeart` | 計画・手段・支援の有無を自ら語り始める |

## 直接的な問いかけ

「死にたいと考えていますか？」という選択肢には
`AffectCondition(resident, Trust, AtLeast, 50)` を設定する。

- 条件を満たして選択 → `OpenHeart` へ。以降のアセスメント項目が確認可能になる
- 条件を満たさずに選択 → `PrematureProbing`。`Trust -15` で `Withdrawn` に近づく
- **最後まで選択しなかった** → 完了時に `RiskAssessmentIncomplete`

3 つ目が重要。尋ねないことは「安全な選択」ではなく**不作為の誤り**として
明示的に記録する。ゲートキーパー研修でもっとも実行されにくい行動がこれにあたる。

この選択肢は `hideWhenUnavailable = false` にする。条件未達でも UI に出し、
学習者が「今聞くべきか」を自分で判断する構造にしないと訓練にならない。

## アセスメント項目

`bool` フラグとして保持し、対話ルートから自動検出する。

| フラグ | 確認内容 | objective_id |
|---|---|---|
| `hasSuicidalIdeation` | 希死念慮の直接確認 | `GTK-ASK-01` |
| `hasPlan` | 具体的な計画の有無 | `GTK-RSK-01` |
| `hasMeansAccess` | 手段へのアクセス | `GTK-RSK-01` |
| `hasPreviousAttempt` | これまでの未遂歴 | `GTK-RSK-01` |
| `hasSupport` | 周囲のサポート・孤立の程度 | `GTK-WCH-01` |
| `hasSafetyPlan` | 安全の約束 | `GTK-WCH-01` |
| `hasReferral` | 専門相談窓口への接続合意 | `GTK-CON-01` |

各フラグが立った時点で `AssessmentItemChecked` を送る。

## 評価ルーブリック

| objective_id | 測る対象 |
|---|---|
| `GTK-AWR-01` | 気づき：SOS サインの察知 |
| `GTK-LST-01` | 傾聴：`InappropriateEncouragement` の回数 |
| `GTK-ASK-01` | 希死念慮の直接確認の実施とタイミング |
| `GTK-RSK-01` | 計画性・手段・未遂歴の確認（3 項目の充足率） |
| `GTK-CON-01` | 専門資源への確実な接続 |
| `GTK-WCH-01` | 安全の約束と支援体制 |
| `GTK-CRS-01` | 危機対応としてのリスクコミュニケーション |

**完了条件**: `hasSafetyPlan && hasReferral` → `SuccessConnection`。
それ以外はすべて未完了として扱い、不足している項目をデブリーフィングで提示する。

## 主なログイベント

| event_type | payload の主な内容 |
|---|---|
| `DialogueSelected` | `node`, `choice_index`, `resident_trust`（`urgency` も記録） |
| `AssessmentItemChecked` | `item`, `checked` |
| `AffectThresholdCrossed` | `parameter`, `value`, `state` |
| `SafetyPlanAgreed` | `plan_elements` |
| `ReferralAgreed` | `resource`, `agreed` |

## 実装メモ・取り扱い上の注意

- 台詞は**必ず専門家の監修を経る**こと。共通基盤側は分岐の実行だけを担い、
  文言はすべて `DialogueGraph` アセットにある。監修者が直接修正できる状態を保つ。
- 学習者自身が当事者である可能性を前提にする。シナリオ開始前と終了後に、
  相談窓口の案内を常設で表示する（アプリ内の固定 UI として、シナリオの
  成否に関わらず出す）。中断も常時可能にする。
- `Urgency` を実行中の UI に出してはならない。数値を見て操作する練習になり、
  かつ「切迫度が見える」という誤った前提を学習させてしまう。
- 相手の描写は特定の属性・疾患に紐づけない。「精神疾患のある人は危険／特別」
  という誤ったスキーマを強化しないこと。
