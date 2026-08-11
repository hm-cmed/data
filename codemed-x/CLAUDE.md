# Codemed-x Unity 6 Project Rules

このファイルは `codemed-x/` 以下で作業するときの規約。5 つのシナリオ（① 相談援助面接 /
② 困難な対話 ACP / ③ 夜勤マルチタスク / ④ 疑義照会 / ⑤ ゲートキーパー）はすべて
`unity/Assets/CodemedX/Runtime` の共通基盤の上に実装する。

## Environment

- Unity Version: Unity 6 (6000.0 LTS)
- Rendering Pipeline: Universal Render Pipeline (URP) / ターゲットは Meta Quest 3・Pico 4
- XR Framework: OpenXR + XR Interaction Toolkit 3.x
- Scripting Backend: IL2CPP / .NET Standard 2.1

## Coding Guidelines (C#)

- Inspector に出すフィールドは `[SerializeField] private`。`public` フィールドは
  `TrainingEvent` など JsonUtility でシリアライズする DTO に限る（JSON のキー名がフィールド名になるため）。
- Unity 6 で非推奨になった API を使わない:
  - `rigidbody.velocity` ではなく `rigidbody.linearVelocity`
  - `FindObjectOfType<T>()` ではなく `FindAnyObjectByType<T>()` / `FindFirstObjectByType<T>()`
  - XRIT 3.x の Interactable / Interactor の名前空間は `UnityEngine.XR.Interaction.Toolkit.Interactables` / `.Interactors`
- 新しいアセットを追加したら `.meta` を必ず一緒にコミットする。Unity を開けない環境で
  スクリプトだけ追加した場合は `python3 codemed-x/tools/generate_unity_meta.py` を実行する。
- 認証情報・ソルト・エンドポイントのトークンを C# にも ScriptableObject にも書かない。
  実行時に `EventLogger.SetAuthToken()` / `LearnerIdentity.SetFromRawIdentifier()` で注入する。

## Architectural Concept

- **HMD アプリはシンクライアント**。端末は観測した事実を `TrainingEvent` として送るだけで、
  成績の確定はサーバ側で行う。端末側のスコアはデブリーフィング表示用の暫定値。
- ログの送信口は `EventLogger` ただ一つ。`UnityWebRequest` を各シナリオから直接叩かない。
- 判定に使う値（隠れパラメータ、注視秒数、経過時間）は必ず `payload_json` に残す。
  残っていない値は後からログだけで再現できず、教育効果の検証に使えない。

## 5 シナリオ共通の再利用部品（新規に作らずこれを使う）

| やりたいこと | 使うもの |
|---|---|
| 局面の遷移管理 | `ScenarioController<TState>` / `ScenarioStateMachine<TState>` |
| NPC の隠れパラメータ（信頼度・不安・切迫度・権威勾配） | `AffectActor` / `AffectState` / `AffectThreshold` |
| 選択肢による分岐と沈黙の扱い | `DialogueRunner` + `DialogueGraph`（ScriptableObject） |
| 空間内の見落とし／気づきの計測 | `ObservationTarget` + `GazeDwellTracker` |
| 時間経過によるイベント発生 | `ScenarioClock` |
| 複数タスクの優先順位と放置判定 | `PriorityTaskBoard` / `PriorityTask` |
| 評価項目の集計 | `ScoreCard` + `ObjectiveCatalog` |
| ログ送信 | `EventLogger.Instance.Log(...)` |

## 評価項目（objective_id）

- 正本は `codemed-x/schema/objectives.csv`。C# に新しい objective_id を直書きしない。
- 追加したら `python3 codemed-x/tools/validate_codemedx.py` を通す。
  医学教育モデル・コア・カリキュラム（令和4年度改訂版）に実在する id かを機械的に検証する。
- Unity 側へは `Tools > Codemed-x > 評価項目カタログを CSV から再生成` で取り込む。

## event_type / error_type

- 文字列リテラルを直接書かず `EventTypes` / `ErrorTypes` の定数を使う。
- 新しい種別が要るときは、まずカタログのクラスに定数を追加してから使う。

## テスト

- ロジックは MonoBehaviour から切り離し、EditMode テスト（`Tests/EditMode`）で検証できる形にする。
- 時刻に依存するクラスは `Func<float>` で時計を注入できるようにする（`ScenarioStateMachine` を参照）。
