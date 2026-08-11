# ① 相談援助面接シミュレーター（児童相談所・生活保護・DV・MSW/PSW・ケアマネ）

`scenario_id`: `welfare_abuse_v1`

## 学習目標と準拠基準

- 児童虐待の防止等に関する法律（早期発見義務・通告義務）
- 児童福祉法第33条（一時保護）
- 厚生労働省の子ども家庭支援におけるアセスメントの考え方

中核となる能力は **「観察事実」と「解釈」を混同せず、法的要件に照らして
一時保護の必要性を判定する**こと。「なんとなく心配」ではなく、
何を見て・何を聞いて・どの要件に当たると考えたのかを分離して報告できることを測る。

## 状態遷移

```
Preparation ──▶ Observation ──▶ Interview ──▶ Assessment ──▶ Escalation ──▶ Completed
     │              │               │              │              │
     └──────────────┴───────────────┴──────────────┴──────────────┴──▶ Aborted
```

| 状態 | 内容 | 遷移トリガー |
|---|---|---|
| `Preparation` | デスクトップUIでケース資料（家族構成・過去の通告履歴）を確認 | 必読資料をすべて開いた／時間経過で先へ進む |
| `Observation` | 玄関・居室を巡り、リスクサインを注視で「証拠」として登録 | 学習者が面談開始を選択 |
| `Interview` | 防衛的な保護者との個別対話 | 対話グラフが終端に到達／`Trust` 30 以下で強制終了 |
| `Assessment` | 一時保護の要否と根拠を選択 | 判定の提出 |
| `Escalation` | 上司（AI）へ報告 | 報告完了 |
| `Completed` | デブリーフィング | — |

`Preparation` で資料を読まずに進んだ場合、`Interview` で保護者から出る
情報の一部が「既知の情報と矛盾している」ことに気づけない設計にする
（=事前準備の欠如が後段のスコアに効く）。

## 隠れパラメータ

保護者 `guardian` に `AffectActor` を付け、`Trust` を使う。

| 閾値 | 状態名 | 起きること |
|---|---|---|
| `Trust <= 30` | `Shutdown` | 面談を強制終了。以降の情報開示が得られない |
| `Trust >= 70` | `Disclosing` | 追加情報（経済状況・孤立・支援の有無）を開示する |

初期値は 40 程度（=最初から警戒している）に設定し、
共感的・非審判的な選択で上がり、高圧的・詰問的な選択で下がる。

## 評価ルーブリック

| objective_id | 測る対象 | 判定 |
|---|---|---|
| `WLF-OBS-01` | リスクサインの発見数 | `RiskSignObserved` の件数 / `ObservationMissed` で critical のもの |
| `WLF-OBS-02` | 事実と解釈の区別 | 報告UIで「観察事実」欄に解釈語（不適切・ひどい 等）を選んだか |
| `WLF-INT-01` | 傾聴・共感 | 共感的選択肢の選択率、`Trust` の推移 |
| `WLF-INT-02` | 高圧的でないこと | `CombativeSpeech` の発生回数 |
| `WLF-LAW-01` | 一時保護要件の判定 | 判定と根拠の組み合わせの整合性 |
| `WLF-ESC-01` | 上司への報告 | 報告に含めた要素（事実・懸念・依頼）の充足 |
| `WLF-SOC-01` | 社会資源への接続 | 生活保護等の資源提示の有無 |

**不合格に直結する条件**（サーバ側判定の候補）

- critical なリスクサインの見落としが 2 件以上
- 一時保護要件を満たす状況で「保護不要」と判定した（`UnjustifiedCustodyDecision`）
- `CombativeSpeech` により面談が強制終了した

## 主なログイベント

| event_type | 発生タイミング | payload の主な内容 |
|---|---|---|
| `RiskSignObserved` | 注視 2 秒で発見 | `target`, `dwell_sec`, `critical` |
| `ObservationMissed` | `Observation` 終了時に未発見だったもの | `target`, `critical` |
| `DialogueSelected` | 選択肢の決定 | `node`, `choice_index`, `guardian_trust` |
| `AffectThresholdCrossed` | `Trust` が閾値を跨いだ | `parameter`, `value`, `state` |
| `AssessmentSubmitted` | 判定の提出 | `decision`, `grounds`, `correct` |
| `EscalationPerformed` | 上司への報告 | `elements_included`, `duration_sec` |

## 実装メモ

- `ScenarioController<WelfareState>` を継承する。遷移表は `ConfigureTransitions` で宣言する。
- リスクサインは `ObservationTarget`（`requiredDwellSeconds = 2`, critical なものに
  `isCritical = true`）を配置し、`GazeDwellTracker` を XR Origin に 1 つ付ける。
- `Observation` を抜けるとき、未発見の `ObservationTarget` すべてに
  `ReportMissed()` を呼ぶ。見落としは「イベントが無いこと」ではなく明示的に記録する。
- 面談は `DialogueRunner` + `DialogueGraph`。感情値表示スライダーは
  **デブリーフィング時のみ**表示する（面談中に見えると、相手の反応ではなく
  数値を見て操作する練習になってしまう）。
- 一時保護の判定 UI は「判定」と「根拠（複数選択）」を分ける。
  判定だけ合っていて根拠が伴わない場合を区別できるようにするため。
