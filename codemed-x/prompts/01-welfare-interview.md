# ① 相談援助面接シミュレーター — 実装済み

**このシナリオは実装が終わっています。プロンプトを実行する必要はありません。**

実体: [`codemed-x/standalone/01-welfare-interview/`](../standalone/01-welfare-interview/)

## 動かし方

1. Unity プロジェクトの `Assets/` の下に `01-welfare-interview` フォルダをコピーする
2. **GameObject > Create Empty** で空の GameObject を作る
3. `WelfareInterviewSim` スクリプトを付ける
4. **Play**

Canvas も XR パッケージも要らない。詳細は
[`codemed-x/standalone/README.md`](../standalone/README.md)。

## 中身を変えたいとき

文言・配点・観察対象・保護者の台詞は、すべて
`WelfareScenarioData.cs` の `CreateDefault()` にまとまっている。
進行のコード（`WelfareInterviewSim.cs`）を触らずに差し替えられる。

Claude Code に渡すなら、こう頼むのが早い。

```
codemed-x/standalone/01-welfare-interview/WelfareScenarioData.cs の CreateDefault() を
以下の方針で書き直してください。進行のコード（WelfareInterviewSim.cs）は変更しないこと。

- 【ここに、監修者からの指摘や差し替えたい題材を書く】
- 観察対象は、リスクサインと中立な項目を混ぜたまま保つこと
- 一時保護の根拠の選択肢には、根拠にならないもの（態度や生活水準への印象）を
  必ず 2 つ以上残すこと
- objective_id は codemed-x/schema/objectives.csv の WLF-* のみを使うこと
```

## 設計の詳細

[`codemed-x/docs/scenarios/01-welfare-interview.md`](../docs/scenarios/01-welfare-interview.md)

VR 版（空間観察を 3D 空間で行う形）に進める場合は、
共通基盤の `ObservationTarget` / `GazeDwellTracker`
（[`codemed-x/unity/Assets/CodemedX/Runtime/Observation/`](../unity/Assets/CodemedX/Runtime/Observation/)）
が使えるが、まず単体版で内容を固めてからで間に合う。

## 次にやること

②〜⑤ を、この①と同じ 3 ファイル構成で 1 つずつ作る。
プロンプトは [`prompts/02`](02-acp-dialogue.md) 〜 [`prompts/05`](05-gatekeeper.md)。

①を実際に触って、進行の粒度や記録する項目を調整してから次に進むのが確実。
**1 本目で決めた形が、残り 4 本のひな形になる。**
