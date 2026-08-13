# ⑤ ゲートキーパー：自殺リスク評価と危機介入 — Claude Code 入力用プロンプト

以下をそのまま Claude Code に貼る。

---

あなたは Unity 6 に習熟したシニアゲームエンジニアです。
自殺防止ゲートキーパー研修用の「希死念慮アセスメントと危機介入対話」を、
**単体で完結する 1 フォルダ**として実装してください。

## 事前に読むもの

- `codemed-x/standalone/README.md`（作り方の約束事）
- `codemed-x/standalone/01-welfare-interview/` の 3 ファイル（**必ず読む**）
- `codemed-x/docs/scenarios/05-gatekeeper.md`（**取り扱い上の注意を必ず読むこと**）
- `codemed-x/schema/objectives.csv` の `gatekeeper_crisis_v1` の行

## 作るもの

`codemed-x/standalone/05-gatekeeper/` に 3 ファイル。

| ファイル | 役割 |
|---|---|
| `GatekeeperSim.cs` | 局面の進行と画面（`OnGUI`） |
| `GatekeeperScenarioData.cs` | 文言・パラメータ増減・配点 |
| `GatekeeperTrainingLog.cs` | 学習履歴の記録 |

`scenario_id` は `gatekeeper_crisis_v1`、名前空間は `CodemedX.Gatekeeper`。

### 守ること（①と揃える）

- **空の GameObject にスクリプトを 1 つ付けて Play するだけで動くこと。**
  Canvas、Prefab、XR パッケージ、`.asmdef` は使わない。画面は IMGUI。
- **`TrainingEvent` のフィールドは①と 1 文字も変えない。**
  `TrainingEvent` / `Payload` / ログクラスは①からコピーして名前空間だけ変える。
- `objective_id` は `GTK-*` のみ使う。
- 台詞は**すべて** `GatekeeperScenarioData.CreateDefault()` に集約する。
  進行のコードに文言を埋め込まない。監修者が 1 ファイルだけ見れば直せる状態を保つ。

## 内容

### 局面

```
Approach → Listening → RiskAssessment → SafetyPlanning → Referral → Debrief
               ↑            │
               └── Withdrawn ┘（相手が心を閉ざす。関係を立て直せる）
```

`Withdrawn` に入っても終わりにしない。`Listening` へ戻れるようにする
（実務でも一度の失言で終わりではなく、その立て直しの練習に価値がある）。
ただし戻ると対話のターンを消費する。

### 相手の内部モデル

相手（生徒／住民）は `Trust`（初期 40）と `Urgency`（初期 50）を持つ。

| 学習者の対応 | Trust | Urgency |
|---|---|---|
| 安易な励まし（「頑張って」「死ぬなんて言わないで」） | `-30` | `+20` |
| 説教・正論（「家族が悲しむよ」） | `-25` | `+15` |
| 受容的な傾聴（「そう感じているんですね」） | `+15` | `-5` |
| 具体的な事実の確認（睡眠・食事・出来事） | `+10` | `0` |
| 希死念慮の直接確認（適切なタイミング） | `+20` | `0` |

安易な励ましの選択肢には `InappropriateEncouragement` を設定する。

| 閾値 | 状態 | 起きること |
|---|---|---|
| `Trust <= 25` | `Withdrawn` | 「もういいです」で開示が止まる |
| `Trust >= 50` | `Open` | 直接的な問いかけが有効になる |
| `Trust >= 70` | `OpenHeart` | 計画・手段・支援の有無を自ら語り始める |

**`Urgency` は対話中の画面に絶対に表示しないこと。**
安易な励ましは相手を黙らせるだけでなく、**表面上は落ち着いたように見えるまま切迫度を上げる**。
この乖離を振り返りで初めて開示することが、この教材の核心にあたる。
対話中に見えると、数値を見て操作する練習になり、かつ「切迫度は見えるものだ」という
誤った前提を学習させてしまう。`Trust` も同様に非表示にする。

### 直接的な問いかけ

「死にたいと考えていますか？」という選択肢を用意する。

- `Trust >= 50` で選択 → `OpenHeart` へ。以降のアセスメント項目が確認可能になる
- `Trust < 50` で選択 → `PrematureProbing` として記録、`Trust -15`
- **最後まで選択しなかった** → 完了時に `RiskAssessmentIncomplete` を記録する

3 つ目を必ず実装すること。尋ねないことは「安全な選択」ではなく**不作為の誤り**であり、
ゲートキーパー研修でもっとも実行されにくい行動がこれにあたる。

**この選択肢は条件を満たさなくても画面に出し続けること。**
グレーアウトも非表示もしない。学習者自身が「今聞くべきか」を判断する構造にしないと訓練にならない。

### アセスメント項目

`bool` で保持し、対話ルートから自動で検出する。立った時点で
`AssessmentItemChecked` を記録する。

| フラグ | 確認内容 | objective_id |
|---|---|---|
| `hasSuicidalIdeation` | 希死念慮の直接確認 | `GTK-ASK-01` |
| `hasPlan` | 具体的な計画の有無 | `GTK-RSK-01` |
| `hasMeansAccess` | 手段へのアクセス | `GTK-RSK-01` |
| `hasPreviousAttempt` | これまでの未遂歴 | `GTK-RSK-01` |
| `hasSupport` | 周囲のサポート・孤立の程度 | `GTK-WCH-01` |
| `hasSafetyPlan` | 安全の約束 | `GTK-WCH-01` |
| `hasReferral` | 専門相談窓口への接続合意 | `GTK-CON-01` |

### 完了判定

`hasSafetyPlan && hasReferral` が両方成立した場合のみ成功（`SuccessConnection`）。
それ以外は未完了として扱い、不足している項目を振り返りで提示する。

完了時、全フラグの状況を `payload_json` に載せる。

### 振り返りで見せるもの

- `Trust` と `Urgency` の推移。**特に「Trust が下がったのに Urgency が上がった」場面を明示する**
- 確認できた項目と、確認しなかった項目
- 直接確認を行ったか、行った場合のタイミング

## 取り扱い上の注意（実装に必ず反映すること）

- **学習者自身が当事者である可能性を前提にする。**
  相談窓口の案内を、シナリオの開始前・実行中・終了後を通じて画面に常設する。
  シナリオの成否に関わらず表示し、いつでも中断できるボタンを置く。
- 相手の描写を特定の属性・疾患に紐づけない。
  「精神疾患のある人は危険／特別」という誤ったスキーマを強化しない。
- 台詞は専門家の監修を経る前提で、`GatekeeperScenarioData` の 1 箇所にまとめる。
- 振り返り画面と README に、**監修前の仮版であること**を明記する。

## 成果物

1. 上記 3 ファイル（そのままコンパイルが通るもの）
2. `codemed-x/standalone/README.md` の⑤の行を、①と同じ書式で埋める
3. 相談窓口の常設案内が全局面で出ていることを、実際に Play して確認する
