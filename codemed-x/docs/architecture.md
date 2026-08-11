# Codemed-x 共通基盤アーキテクチャ

## 全体構成

```
┌─────────────── HMD / PC クライアント (Unity 6 + URP + OpenXR/XRIT 3.x) ───────────────┐
│                                                                                      │
│  シナリオ層（テーマごとに実装）                                                       │
│   WelfareScenarioManager / AcpDialogueManager / NurseWorkloadScheduler / ...          │
│        │ 継承                    │ 参照                    │ 参照                     │
│  ┌─────▼──────────┐  ┌───────────▼──────────┐  ┌──────────▼──────────┐               │
│  │ ScenarioCtrl   │  │ DialogueRunner       │  │ GazeDwellTracker    │               │
│  │ StateMachine   │  │ + DialogueGraph(SO)  │  │ + ObservationTarget │               │
│  │ ScoreCard      │  │ + AffectActor        │  │ ScenarioClock       │               │
│  └─────┬──────────┘  └───────────┬──────────┘  │ PriorityTaskBoard   │               │
│        │                         │             └──────────┬──────────┘               │
│        └────────────┬────────────┴────────────────────────┘                          │
│                     ▼                                                                │
│              EventLogger（唯一の送信口）                                              │
│                キュー → バッチ化 → 再送(指数バックオフ) → 失敗時スプール             │
│                     │                          └─→ 端末内 JSON Lines（次回起動で再送）│
└─────────────────────┼────────────────────────────────────────────────────────────────┘
                      │ HTTPS POST  TrainingEventBatch (JSON)
                      ▼
              API Gateway / Apps Script
                      │
                      ▼
         LMS・Google スプレッドシート・BigQuery
```

## 設計判断とその理由

### 1. 端末はシンクライアントにする

合否判定・スコアの正規化・ルーブリックの重み付けは**サーバ側**で行う。端末は
「何を見た」「何を選んだ」「何秒かかった」という観測事実を送るだけにする。

- 改造ビルドで成績を書き換えられない。
- ルーブリックを変えたときに、過去のログを再集計するだけで新基準の成績が出る。
  端末側で点数を確定していると、アプリを配り直さない限り基準を変えられない。

`ScoreCard.TotalScore` は端末のデブリーフィング画面に即時表示するための暫定値であり、
成績としては扱わない。

### 2. ログの送信口を 1 つに絞る

各シナリオが `UnityWebRequest` を直接叩くと、シナリオごとに再送やオフライン処理の
実装差が生まれ、ある演習だけログが欠ける、という事故が起きる。
`EventLogger` に一本化し、以下をすべてそこで引き受ける。

| 起きること | 対処 |
|---|---|
| 会場の Wi-Fi が不安定 | 指数バックオフ + ジッタで再送（一斉復帰時の同時再送を散らす） |
| 通信が復帰しない | 端末内 JSON Lines へ退避、次回起動時に自動再送 |
| 学習者が HMD を外した / アプリが停止した | `OnApplicationPause` / `OnApplicationQuit` で同期的に退避 |
| 再送でサーバに二重登録される | `batch_id` を冪等キーにしてサーバ側で重複排除 |
| 同一ミリ秒のイベントの順序が不定 | セッション内 `sequence` で確定 |

### 3. シナリオ差分はコードではなくデータで表す

対話の分岐、隠れパラメータの増減、閾値、時間トリガーは ScriptableObject と
Inspector のデータとして持たせる。理由は 2 つ。

- シナリオ監修者（教員・臨床家）が C# を触らずに検証・修正できる。
  対人援助の教材は**文言の妥当性そのものが品質**なので、ここが開発者ボトルネックに
  なると監修サイクルが回らない。
- 5 テーマが同じランナーを共有できる。②④⑤ の対話はすべて `DialogueRunner` +
  `DialogueGraph` の組み合わせで表現でき、差分はアセットだけになる。

### 4. 隠れパラメータを 5 軸に共通化する

5 テーマの「相手の内部状態」は、次の 5 軸の組み合わせで表現できる（`AffectParameter`）。

| 軸 | 使うテーマ | 意味 |
|---|---|---|
| `Trust` | ①②⑤ | 信頼度。低いと開示が止まり面談が打ち切られる |
| `Anxiety` | ②③ | 不安・動揺。高いと感情的激高・不穏行動へ |
| `Urgency` | ⑤ | 切迫度。学習者には見えないリスクの高さ |
| `Comprehension` | ② | 理解度。低いまま同意を取ると不適切な意思決定 |
| `Pressure` | ④ | 権威勾配。高いと主張を取り下げてしまう |

閾値を跨いだ瞬間だけ `AffectThresholdCrossed` を発火させ（値が閾値の内側で
動き続けても再発火しない）、NPC の演技状態の切り替えとログ記録を同時に行う。

### 5. 「見落とし」を明示的に記録する

`ObservationTarget.ReportMissed()` で、未発見のまま局面が終わったことを能動的にログに残す。
教育上もっとも価値があるのは「気づけたもの」ではなく「気づけなかったもの」であり、
イベントが飛ばないことを見落としの証拠として使うのは（通信欠損と区別できず）危険。

### 6. 評価項目を医学教育モデル・コア・カリキュラムに接続する

`objective_id`（例 `WLF-INT-01`）は `schema/objectives.csv` を介して
コアカリの `id`（例 `JkxhhWA` = CM-01-02-02）に対応づけられる。

- シミュレータの学習履歴を、大学のカリキュラムマップと同じ粒度で集計できる。
- インデックス（CM-01-02-02）ではなく `id` で紐づけるため、コアカリ改訂で
  番号が動いても対応関係が壊れない。
- `tools/validate_codemedx.py` が、参照先がコアカリに実在するかを機械的に検証する。

## 主要クラス

### Core

| クラス | 役割 |
|---|---|
| `TrainingEvent` | 共通イベント。フィールド名が JSON キーになるのでリネーム禁止 |
| `SessionContext` | セッション単位の不変メタデータ + `sequence` 採番 + 匿名化 |
| `LearnerIdentity` | 匿名学習者IDの唯一の保持場所 |
| `PayloadBuilder` | `payload_json` を安全に組み立てる（エスケープ / InvariantCulture） |
| `EventTypes` / `ErrorTypes` / `LocomotionModes` | 文字列カタログ |

### Logging

| クラス | 役割 |
|---|---|
| `EventLogger` | キュー・バッチ化・再送・スプール。シナリオはここだけを呼ぶ |
| `HttpEventSink` | API Gateway / Apps Script への POST と結果分類 |
| `EventSpool` | 端末内 JSON Lines への退避と復元 |
| `EventLoggerSettings` | エンドポイント・バッチ・再送ポリシー（**認証情報は持たない**） |

### Scenario / Dialogue / Observation / Timing / Assessment

| クラス | 役割 |
|---|---|
| `ScenarioController<TState>` | 全シナリオの基底。セッション開始・制限時間・完了送信 |
| `ScenarioStateMachine<TState>` | 遷移表に無い進行を拒否。滞在時間の履歴を保持 |
| `DialogueRunner` / `DialogueGraph` | 対話の実行と沈黙の扱い |
| `AffectActor` / `AffectState` | NPC の隠れパラメータと閾値遷移 |
| `ObservationTarget` / `GazeDwellTracker` | 注視による発見と見落としの記録 |
| `ScenarioClock` / `PriorityTaskBoard` | 時間トリガーと優先順位逆転・放置の判定 |
| `ScoreCard` / `ObjectiveCatalog` | 評価項目ごとの集計とコアカリへの接続 |

## 未実装（次の段階）

- サーバ側の集計とルーブリック適用（現状はモック LMS と Apps Script のみ）
- デブリーフィング UI（`EventLogger.EventLogged` を購読すれば作れる状態にはしてある）
- 音声入力による発話評価（現状は選択肢ベース。`DialogueRunner.Select` の前段に
  音声→選択肢のマッピングを挟む想定）
