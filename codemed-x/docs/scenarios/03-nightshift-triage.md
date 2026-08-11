# ③ 看護：複数患者・優先順位判断・夜勤単独対応

`scenario_id`: `nightshift_multi_v1`

## 学習目標と準拠基準

- 時間圧（タイムプレッシャー）下でのマルチタスクと臨床判断
- 院内迅速対応システム（RRS）／急変時のエスカレーション
- SBAR による報告

中核は **「何を先にやるか」と「いつ人を呼ぶか」**。個々の手技の巧拙ではなく、
複数の要求が同時に来たときの順序づけと、抱え込まずに応援を要請できるかを測る。

## 時間台本（180 秒）

`ScenarioClock` のトリガーとして Inspector に定義する。

| 時刻 | イベント | 優先度 | 放置許容 |
|---|---|---|---|
| 10 秒 | 患者A の SpO2 が急激に低下（生体モニタ点滅） | 10 | 15 秒 |
| 30 秒 | 患者B（認知症）がベッドサイドで立ち上がろうとする（抜管・転倒リスク） | 8 | 12 秒 |
| 60 秒 | ナースステーションの電話が鳴る（事務連絡） | 2 | — |
| 90 秒 | 複数病室からナースコールが同時点灯 | 5 | 30 秒 |

## 優先順位の判定

`PriorityTaskBoard` が 2 種類のエラーを自動判定する。

- **優先順位の逆転** (`PriorityInversion`) — より緊急なタスクが発生中なのに、
  緊急度が `inversionPriorityGap` 以上低いタスクに着手した。
  例: SpO2 低下（10）を放置して電話（2）に出る。
- **放置による重大化** (`CriticalTaskNeglected`) — 許容時間を超えて未対応のまま。
  例: 抜管リスクを 12 秒以上放置 → 自己抜管が発生する。

「着手した」の検知は、共通基盤側では `PriorityTaskBoard.AttendTo(task)` の呼び出しとして
抽象化してある。実際のトリガーはシナリオ側で選ぶ。

- ベッドサイドの Trigger Collider への侵入（`XR Origin` の衝突）
- 対象オブジェクトの Grab（XRIT 3.x の Interactable の `selectEntered`）
- 器材 UI の操作

## 状態遷移

```
Briefing ──▶ Rounds ──▶ Escalation ──▶ Debrief
                │            ▲
                └────────────┘（急変のたびに往復しうる）
```

`Rounds` が本体で、局面遷移よりも `PriorityTaskBoard` の状態が主役になる。
`Escalation`（医師への SBAR 電話・RRS 要請）はどの時点からでも入れるようにする。

## 評価ルーブリック

| objective_id | 測る対象 | 判定 |
|---|---|---|
| `NGT-TRI-01` | 優先順位判断 | `PriorityInversion` の回数 |
| `NGT-MON-01` | モニタ異常の把握 | SpO2 低下の発見までの秒数 |
| `NGT-SAF-01` | 転倒・抜管の予防的介入 | 患者B への到達時間 |
| `NGT-SAF-02` | インシデント時の対応・記録・報告 | 自己抜管発生後の行動 |
| `NGT-SAF-03` | 患者安全の実践 | 全体 |
| `NGT-ESC-01` | 応援要請 | **急変発生から SBAR 報告までの秒数**（Time to critical decision） |

**もっとも重要な指標は「急変からエスカレーションまでの秒数」**。
新人看護師の教育で問題になるのは知識ではなく「呼ぶのが遅れる」ことなので、
これを単独の指標として取り出せるようにログ設計している。

**重大アクシデントのフラグ**

- 患者A の SpO2 低下を 15 秒以上放置 → 状態悪化（`CriticalError` 相当）
- 患者B を 12 秒以上放置 → 自己抜管の発生
- 上記いずれかが起きたまま制限時間終了 → `ScenarioFailed`

## 主なログイベント

| event_type | payload の主な内容 |
|---|---|
| `TriageAction`（着手） | `chosen`, `chosen_priority`, `deferred`, `deferred_unattended_sec` |
| `TriageAction`（解決） | `resolved`, `time_to_action_sec` |
| `TriageAction`（重大化） | `escalated`, `priority`, `unattended_sec` |
| `EscalationPerformed` | `target`, `time_to_decision_sec`, `sbar_complete` |

## 実装メモ

- タイマーと台本は `ScenarioClock` に持たせ、C# のハードコードにしない。
  演習ごとに秒数を変えて難易度を調整するため。
- `PriorityTask` は各ベッド・各設備の GameObject に付け、`ScenarioClock` の
  `UnityEvent` から `Activate()` を呼ぶ。
- 制限時間は `ScenarioDefinition.timeLimitSeconds = 180`。
  時間切れは `ScenarioController` の基底が `Timeout` として処理する。
- 移動方式は `continuous`（病棟内を歩き回るため）。VR 酔いの影響を後から
  解析できるよう `locomotion_mode` は必ず記録される。
- 非同期処理はコルーチンで書く。XRIT 3.x のイベント（`selectEntered` 等）と
  組み合わせる際、`async/await` はシーン遷移時のキャンセル漏れを起こしやすい。
