# TrainingEvent 仕様

正本は [`schema/training-event.schema.json`](../schema/training-event.schema.json) と
[`schema/training-event-batch.schema.json`](../schema/training-event-batch.schema.json)。
C# 側の定義は `unity/Assets/CodemedX/Runtime/Core/TrainingEvent.cs`。
**フィールド名がそのまま JSON のキーになる**ため、片方だけを変更してはならない。

## フィールド

| フィールド | 型 | 必須 | 内容 |
|---|---|---|---|
| `schema_version` | string | ✓ | 現在 `"1.0"`。互換性を壊す変更時のみ上げる |
| `user_id` | string | ✓ | 匿名化された学習者ID（ソルト付き SHA-256 の先頭 16 桁） |
| `session_id` | string (UUID) | ✓ | セッションごとの GUID |
| `scenario_id` | string | ✓ | `welfare_abuse_v1` 等。`^[a-z0-9_]+_v[0-9]+$` |
| `event_type` | string | ✓ | `EventTypes` のカタログ値 |
| `objective_id` | string | | 評価項目ID。該当なしは空文字 |
| `event_timestamp_unix_ms` | integer | ✓ | UTC ミリ秒 |
| `sequence` | integer | ✓ | セッション内の単調増加連番（0 始まり） |
| `score` | number | | 加減点または進捗。持たないイベントは 0 |
| `error_type` | string | | `ErrorTypes` のカタログ値。エラーなしは `"none"` |
| `device_model` | string | | `SystemInfo.deviceModel` |
| `build_version` | string | | `Application.version` |
| `locomotion_mode` | string | | `teleport` / `continuous` / `seated` / `desktop` / `unknown` |
| `payload_json` | string | | シナリオ固有の詳細（JSON 文字列。最大 4096 文字） |

### 実務レポートの最小データ構造からの拡張点

当初案の 11 フィールドはすべて維持したうえで、運用上不可欠な 3 つを足している。

- **`sequence`** — ミリ秒タイムスタンプは同一フレーム内で衝突する。順序の確定と
  再送時の重複排除のために `(user_id, session_id, sequence)` を冪等キーとして使う。
- **`schema_version`** — 端末の更新は演習会場ごとにばらつく。新旧バージョンの
  イベントが同時に届く前提で、受信側が分岐できるようにする。
- **`payload_json`** — 判定に効いた値（隠れパラメータ、注視秒数、経過時間）を残すため。
  入れ子オブジェクトではなく**文字列**にしているのは、`JsonUtility` が可変構造を
  扱えないことと、LMS 側の列定義を固定したままシナリオ固有の情報を運ぶため。

## payload_json の扱い

- 必ず `PayloadBuilder` で組み立てる。文字列連結はエスケープ漏れでログ全体を壊す。
- 小数はロケール非依存（`InvariantCulture`）で出力される。`"2,1"` のような
  ロケール依存の書式は JSON を破壊するため、手書き禁止。
- **自由記述の生データ・個人情報を入れてはならない**。入れてよいのは
  シナリオ内の識別子・数値・真偽値だけ。

```csharp
string payload = PayloadBuilder.Create()
    .Add("target", "kitchen_liquor_bottles")
    .Add("dwell_sec", 2.1f)
    .Add("critical", true)
    .Build();
// => {"target":"kitchen_liquor_bottles","dwell_sec":2.1,"critical":true}
```

## 送信プロトコル

イベントは 1 件ずつではなく **バッチ**で POST する。

```http
POST /events HTTP/1.1
Content-Type: application/json
Authorization: Bearer <token>
X-Codemedx-Batch-Id: 8c1f0a44-72be-4d39-b5a1-0e6c93f27d84
X-Codemedx-Schema-Version: 1.0

{
  "schema_version": "1.0",
  "batch_id": "8c1f0a44-72be-4d39-b5a1-0e6c93f27d84",
  "sent_at_unix_ms": 1786406800000,
  "retry_count": 0,
  "events": [ /* 1〜200 件 */ ]
}
```

### サーバ側に求める応答

| 状況 | ステータス | クライアントの挙動 |
|---|---|---|
| 受理 | 2xx | バッチを破棄 |
| 既に受理済みの `batch_id` | 2xx | 同上（**重複登録してはならない**） |
| スキーマ違反・認証失敗 | 400 / 401 / 403 | 再送しても通らないのでスプールへ退避 |
| 過負荷・一時障害 | 408 / 429 / 5xx | 指数バックオフ + ジッタで再送 |
| 通信断 | — | 同上 |

`batch_id` は再送しても変わらない。サーバは `batch_id` で冪等に処理すること。
実装例は [`server/gas/Code.gs`](../server/gas/Code.gs)（Apps Script）と
[`tools/mock_lms_server.py`](../tools/mock_lms_server.py)（ローカル検証用）を参照。

## 匿名化

```csharp
LearnerIdentity.SetFromRawIdentifier(studentNumber, saltFromSecureConfig);
// 内部で SHA-256(salt + ":" + rawIdentifier) の先頭 16 桁を user_id にする
```

- ソルトはビルドに埋め込まない。MDM の設定・初回起動時の入力など、実行時に渡す。
- ソルト無しのハッシュは総当たりで元の識別子に戻せるため、API 側で例外にしている。
- ソルトを変えると同一学習者が別人として集計される。演習の期をまたぐ場合は
  ソルトのローテーション方針を先に決めておくこと。

## event_type / error_type

カタログは `Runtime/Core/EventTypes.cs` / `ErrorTypes.cs`。
主要なものだけ抜粋する。

**ライフサイクル**: `SessionStarted` `ScenarioStarted` `StateChanged` `ScenarioCompleted` `ScenarioFailed`

**対話**: `DialogueNodeEntered` `DialogueSelected` `SilenceRespected` `AffectThresholdCrossed` `ProtocolStepCompleted`

**観察**: `RiskSignObserved` `ObservationMissed`

**判断・行動**: `TriageAction` `LabConfirmed` `EvidenceAttached` `AssessmentItemChecked` `AssessmentSubmitted` `EscalationPerformed` `ReferralAgreed` `SafetyPlanAgreed`

**エラー（教育的に意味のある逸脱）**: `CombativeSpeech` `MissedRiskSign` `InterruptedSilence`
`ProtocolStepSkipped` `PriorityInversion` `CriticalTaskNeglected` `DelayedEscalation`
`InquirySkipped` `EvidenceNotPresented` `YieldedToAuthority` `InappropriateEncouragement`
`PrematureProbing` `RiskAssessmentIncomplete` `ReferralOmitted`

## サンプル

[`samples/events.sample.jsonl`](../samples/events.sample.jsonl) に 5 シナリオ分の
代表的なイベントが入っている。`tools/validate_codemedx.py` がこれをスキーマ検証する。
