# ④ 薬剤師：服薬指導から疑義照会 — Claude Code 入力用プロンプト

以下をそのまま Claude Code に貼る。

---

あなたは Unity 6 (URP) に習熟したシニアゲームエンジニアです。
Codemed-x プロジェクトの共通基盤の上に、薬学実務実習 OSCE 向けの
「服薬指導から医師への疑義照会を繋ぐ多者間連鎖シミュレーションエンジン」を実装してください。

## 事前に読むもの

- `codemed-x/CLAUDE.md`
- `codemed-x/docs/architecture.md`
- `codemed-x/docs/scenarios/04-pharmacist-inquiry.md`
- `codemed-x/schema/objectives.csv` の `pharmacist_inquiry_v1` の行

> 法令番号に注意: 疑義照会義務は**薬剤師法第24条**（第21条は調剤応需義務、
> 第28条は調剤録の記載義務）。教材内の表示・解説でもこの番号を使うこと。

## 共通基盤は実装済みです。再実装せず利用してください

- `CodemedX.Scenarios.ScenarioController<TState>` / `ScenarioStateMachine<TState>`
- `CodemedX.Dialogue.DialogueRunner` / `DialogueGraph` / `DialogueChoice`
- `CodemedX.Affect.AffectActor` / `AffectParameter.Pressure` / `AffectThreshold` / `AffectCondition`
- `CodemedX.Observation.ObservationTarget`（検査値パネルの項目に使う）
- `CodemedX.Logging.EventLogger`、`CodemedX.Core.EventTypes` / `ErrorTypes` / `PayloadBuilder`

**選択肢の前提条件チェックは `DialogueRunner` に実装済みです。**
`DialogueChoice.requirements`（`AffectCondition` のリスト）を設定すれば、
条件未達で選んだ場合に `prematureErrorType` が自動で記録されます。

## 実装するもの

### 1. `PharmacistInquiryManager.cs`

`ScenarioController<InquiryState>` を継承する。

```csharp
public enum InquiryState
{
    Counseling, LabReview, DoctorCall, DialogueConflict,
    PrescriptionChange, Completed, FailedInquiry
}
```

遷移表:
`Counseling → LabReview → DoctorCall → DialogueConflict →` `PrescriptionChange` または `FailedInquiry`。
`FailedInquiry` は `AllowFromAny`（学習者がいつでも引き下がれるようにするため）。

### 2. 服薬指導（Counseling）

患者の訴え（「最近少し胃が痛くて…」等）から有害事象の可能性を拾う対話を
`DialogueRunner` + `DialogueGraph` で構成する。
拾えた場合に `objective_id = "PHM-CNS-01"` で加点する。

### 3. 検査値の確認（LabReview）

- 電子カルテ・血液検査データの各項目を `ObservationTarget` として配置する。
- 正しい項目（例: SCr 上昇、eGFR 低下）をダブルクリックで「疑義の証拠」として
  アタッチさせ、`EventTypes.LabConfirmed` と `EventTypes.EvidenceAttached` を
  `objective_id = "PHM-LAB-01"` で送る。payload に `marker`、`value`、
  `attached_as_evidence` を含める。
- アタッチ状態は Manager 側で保持し、次フェーズの選択肢の解放条件に使う。

### 4. 疑義照会（DoctorCall / DialogueConflict）

医師 `doctor` に `AffectActor` を付け、**`AffectParameter.Pressure`（権威勾配 0-100）**を使う。

| 学習者の応答 | Pressure |
|---|---|
| 検査値・症状という具体的根拠を挙げる | `-20` |
| 「念のため」「一応」など曖昧な言い方 | `+20` |
| 制限時間内に応答しない（言い淀み） | `+15` |
| 一度引き下がってから再度主張（2 チャレンジルール） | `-10` |

増減は `DialogueChoice.effects` に**データで**持たせること。

`AffectThreshold`:

- `Pressure >= 80` → 状態名 `Dismissive`。医師が「忙しいからそのまま出して」で
  電話を切り、`FailedInquiry` へ遷移して**即時終了**する。
- `Pressure <= 30` → 状態名 `Receptive`。`PrescriptionChange` へ遷移する。

**応答制限時間**を設ける（例: 10 秒以内に返答しないと `Pressure +15`）。
権威勾配の圧力は即答を迫られることで生じるため、時間要素が欠かせない。

根拠を伴う選択肢には `requirements` として「証拠アタッチ済み」を設定し、
未アタッチで主張した場合の `prematureErrorType` に
`ErrorTypes.EvidenceNotPresented` を指定する。

学習者が自分から「わかりました、このまま調剤します」を選んだ場合は
`ErrorTypes.YieldedToAuthority` を記録して `FailedInquiry` へ。
**これがこのシナリオでもっとも重要な失敗パターン**（知識ではなく行動の失敗）。

### 5. 完了処理

- 成功・失敗いずれも `FinishScenario(success)` を呼ぶ。
  基底が所要時間・スコア・エラー内容を `ScoreCard` のサマリとして送信する。
- `FailedInquiry` でも必ずデブリーフィングまで進める。
  「どの時点で根拠を示せていれば通ったか」をログから提示することが教材の本体。

## 守ること

- 状態遷移のロジックを局面ごとに明確に分離する（`OnStateEntered` で分岐）。
- 医師の台詞は圧力をかけるが、**人格攻撃にはしない**。
  学習目標は「圧力下で根拠を保つ」ことであり、ハラスメント耐性の訓練ではない。
- `[SerializeField] private` を使う。`EventTypes` / `ErrorTypes` の定数を使う。
- `objective_id` は `PHM-*` のみ。`UnityWebRequest` を直接呼ばない。
- Unity 6 で非推奨の API を使わない。
- アセット追加後に `python3 codemed-x/tools/generate_unity_meta.py` を実行する。

## 成果物

1. `PharmacistInquiryManager.cs`（完全にコンパイル可能なもの）
2. 検査値パネルと証拠アタッチ UI
3. 応答制限時間つきの電話 UI コンポーネント
4. サンプルの `DialogueGraph`（服薬指導と疑義照会の最小構成）
5. EditMode テスト — `Pressure` 80 到達での即時終了、
   `YieldedToAuthority` の記録、証拠未アタッチ時の `EvidenceNotPresented` をカバーする
6. シーン構成手順を `docs/scenarios/04-pharmacist-inquiry.md` の末尾に追記
