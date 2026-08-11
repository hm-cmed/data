# 共通前提（① 〜 ⑤ すべてのプロンプトに含まれる部分）

各シナリオのプロンプトファイルにはこの内容が既に埋め込まれているので、
通常は `prompts/01-*.md` 〜 `prompts/05-*.md` をそのまま Claude Code に渡せばよい。
ここは、新しいシナリオを 6 つ目として足すときのテンプレート。

---

あなたは Unity 6 (URP)、OpenXR、XR Interaction Toolkit 3.x に習熟したシニアゲームエンジニアです。
Codemed-x プロジェクトの共通基盤の**上に**、指定されたシナリオを実装してください。

## 前提：共通基盤は実装済みです

`codemed-x/unity/Assets/CodemedX/Runtime/` に以下が既にあります。
**これらを再実装せず、必ず利用してください。**

| 名前空間 | クラス | 用途 |
|---|---|---|
| `CodemedX.Core` | `TrainingEvent`, `SessionContext`, `LearnerIdentity`, `PayloadBuilder`, `EventTypes`, `ErrorTypes` | 共通ログのデータ構造と定数カタログ |
| `CodemedX.Logging` | `EventLogger` | 唯一のログ送信口。バッチ化・再送・オフライン退避を内包 |
| `CodemedX.Scenarios` | `ScenarioController<TState>`, `ScenarioStateMachine<TState>`, `ScenarioDefinition` | 局面遷移とセッション管理の基底 |
| `CodemedX.Affect` | `AffectActor`, `AffectState`, `AffectParameter`, `AffectThreshold`, `AffectEffect`, `AffectCondition` | NPC の隠れパラメータと閾値遷移 |
| `CodemedX.Dialogue` | `DialogueRunner`, `DialogueGraph`, `DialogueNode`, `DialogueChoice` | 対話の実行・沈黙の判定・分岐 |
| `CodemedX.Observation` | `ObservationTarget`, `GazeDwellTracker` | 注視による発見と見落としの記録 |
| `CodemedX.Timing` | `ScenarioClock`, `PriorityTask`, `PriorityTaskBoard` | 時間トリガーと優先順位・放置の判定 |
| `CodemedX.Assessment` | `ScoreCard`, `ObjectiveCatalog`, `ObjectiveEntry` | 評価項目ごとの集計 |

まず `codemed-x/CLAUDE.md`、`codemed-x/docs/architecture.md`、
該当シナリオの `codemed-x/docs/scenarios/*.md` を読んでから着手してください。

## 実装の型

```csharp
public enum XxxState { /* 局面 */ }

public class XxxScenarioManager : ScenarioController<XxxState>
{
    protected override XxxState InitialState { get { return XxxState.First; } }

    protected override void ConfigureTransitions(ScenarioStateMachine<XxxState> machine)
    {
        machine.Allow(XxxState.First, XxxState.Second)
               .AllowFromAny(XxxState.Aborted);
    }

    protected override void OnStateEntered(XxxState previous, XxxState current) { /* 局面ごとの処理 */ }

    // イベント送信は基底の LogEvent を使う（ScoreCard への集約も同時に行われる）
    // LogEvent(EventTypes.RiskSignObserved, "WLF-OBS-01", 1f, ErrorTypes.None, payload);
}
```

## 守ること

- `[SerializeField] private` を使う。`public` フィールドは JSON DTO のみ。
- Unity 6 で非推奨の API を使わない（`rigidbody.velocity` → `linearVelocity`、
  `FindObjectOfType` → `FindAnyObjectByType`）。
- `event_type` / `error_type` に文字列リテラルを直接書かず `EventTypes` / `ErrorTypes` の定数を使う。
  必要な種別が無ければ、まずカタログのクラスに定数を追加する。
- `objective_id` は `codemed-x/schema/objectives.csv` に定義済みのものだけを使う。
  追加が必要なら CSV に足し、`python3 codemed-x/tools/validate_codemedx.py` を通す。
- 判定に効いた数値（隠れパラメータ、秒数）は `PayloadBuilder` で `payload_json` に残す。
- `UnityWebRequest` を直接呼ばない。送信は `EventLogger` 経由のみ。
- 台詞・分岐・閾値・時間台本は C# にハードコードせず、ScriptableObject と
  `[SerializeField]` のデータとして持たせる（監修者が Unity で編集できるように）。
- 新しいアセットを追加したら `python3 codemed-x/tools/generate_unity_meta.py` を実行する。

## 成果物

1. シナリオ Manager の C#（完全にコンパイル可能なもの）
2. 必要な補助コンポーネント
3. EditMode テスト（`Tests/EditMode`）— 状態遷移の拒否と、主要な減点条件が
   正しく記録されることを最低限カバーする
4. シーン構成の手順を `docs/scenarios/` の該当ファイル末尾に追記
