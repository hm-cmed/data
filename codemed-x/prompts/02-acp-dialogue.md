# ② 困難な対話（ACP / SPIKES）— Claude Code 入力用プロンプト

以下をそのまま Claude Code に貼る。

---

あなたは Unity 6 に習熟したシニアゲームエンジニアです。
終末期 ACP（人生会議）の「困難な対話」を訓練するシミュレーションを、
**単体で完結する 1 フォルダ**として実装してください。

## 事前に読むもの

- `codemed-x/standalone/README.md`（作り方の約束事）
- `codemed-x/standalone/01-welfare-interview/` の 3 ファイル（**必ず読む**）
- `codemed-x/docs/scenarios/02-acp-dialogue.md`（設計の詳細）
- `codemed-x/schema/objectives.csv` の `acp_dialogue_v1` の行

## 作るもの

`codemed-x/standalone/02-acp-dialogue/` に 3 ファイル。

| ファイル | 役割 |
|---|---|
| `AcpDialogueSim.cs` | 局面の進行と画面（`OnGUI`） |
| `AcpScenarioData.cs` | 文言・パラメータ増減・配点 |
| `AcpTrainingLog.cs` | 学習履歴の記録 |

`scenario_id` は `acp_dialogue_v1`、名前空間は `CodemedX.Acp`。

### 守ること（①と揃える）

- **空の GameObject にスクリプトを 1 つ付けて Play するだけで動くこと。**
  Canvas、Prefab、TextMeshPro、XR パッケージ、`.asmdef` は使わない。画面は IMGUI。
- **`TrainingEvent` のフィールドは①と 1 文字も変えない。**
  `TrainingEvent` / `Payload` / ログクラスは①からコピーして名前空間だけ変える。
  共通化しようとしないこと（依存を作らないことのほうが今は価値がある）。
- `event_type` / `error_type` は定数クラス（`EventKind` / `ErrorKind`）にまとめる。
- `objective_id` は `ACP-*` のみ使う。
- 文言と数値は `AcpScenarioData.CreateDefault()` に集約し、進行のコードと混ぜない。
- 判定に効いた値は `payload_json` に必ず残す。

## 内容

### 局面

```
Setting → Perception → Invitation → Knowledge → Emotion → Strategy → Debrief
                                                    │
                                                    └→ Breakdown（対話決裂）
```

SPIKES の 6 ステップをそのまま局面にする。各ステップ完了時に
`ProtocolStepCompleted` を、対応する `objective_id`（`ACP-SET-01` 〜 `ACP-STR-01`）で記録する。

ステップを飛ばす操作（「先に病状説明へ進む」ボタン等）を用意し、選んだ場合は
`ProtocolStepSkipped` として記録する。**進行自体は止めない**。
何が悪かったかを体験させ、振り返りで示すことが目的。

### 患者と家族の非対称な感情モデル

`patient`（本人：治療中止を希望）と `family`（家族：延命を希望）の 2 人が同席する。
それぞれ `Trust` / `Anxiety` / `Comprehension`（各 0-100）を持つ。
選択肢は両者に**非対称に**効く。

| 選択の傾向 | patient | family |
|---|---|---|
| 本人の意思のみを肯定 | `Trust +20` | `Trust -10`, `Anxiety +15` |
| 家族の不安のみに応じる | `Trust -15` | `Trust +15`, `Anxiety -10` |
| 双方の懸念を言語化してから本人に確認 | `Trust +10` | `Trust +5`, `Anxiety -5` |
| 専門用語で押し切る | `Comprehension -20` | `Anxiety +10` |

増減値は `AcpScenarioData` に持たせ、進行のコードに直接書かない。

閾値を跨いだ瞬間に `AffectThresholdCrossed` を記録する（跨いだ瞬間だけ。
値が閾値の内側で動き続けても再発火させない）。

| 対象 | 閾値 | 状態 | 起きること |
|---|---|---|---|
| family | `Anxiety >= 80` | `Agitated` | 感情的激高。発言が変わり、選択肢が減る |
| patient | `Trust <= 20` | `Withdrawn` | 本人が語らなくなる |
| patient | `Comprehension <= 30` | `Confused` | 合意しても「理解を伴わない同意」として減点 |

**ドミノ倒し**: family が `Agitated` に入ったら、以降の選択肢は patient の `Trust` にも
負の副作用を持つものに切り替える。片側だけを見て対応すると両方を失う構造にする。

### 沈黙

悪い知らせを伝えた直後の場面に「沈黙」を作る。この場面では、
**4 秒経つまで選択肢を押せないようにするのではなく、押せる状態のまま待たせる**。

- 4 秒待ってから選択 → `SilenceRespected`（加点）
- 4 秒以内に選択 → その選択に加えて `InterruptedSilence` を記録（減点）

待つことを強制すると訓練にならない。待てるかどうかを測る。

### 終了判定

- `Completed`: 双方の `Trust >= 50` かつ `Strategy` に到達
- `Breakdown`: family `Anxiety >= 95` または patient `Trust <= 10`

いずれの場合も振り返り画面まで進める。

### 振り返りで見せるもの

- 患者と家族それぞれの `Trust` / `Anxiety` / `Comprehension` の最終値と推移
- 飛ばしたステップ、沈黙を遮った回数
- 「どちらを犠牲にしたか」が分かる形で提示する

**対話中は 3 つのパラメータを一切表示しないこと。**
数値を見て操作する練習になってしまう。振り返りで初めて開示する。

なお、選択のたびに**両者の値を `payload_json` に載せる**こと。
片方だけだと、後からログを見ても何が起きたか再現できない。

## 成果物

1. 上記 3 ファイル（そのままコンパイルが通るもの）
2. `codemed-x/standalone/README.md` の②の行を、①と同じ書式で埋める
3. 題材が監修前の仮版であることを、振り返り画面と README に明記する
