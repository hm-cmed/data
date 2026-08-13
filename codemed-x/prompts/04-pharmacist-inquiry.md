# ④ 薬剤師：服薬指導から疑義照会 — Claude Code 入力用プロンプト

以下をそのまま Claude Code に貼る。

---

あなたは Unity 6 に習熟したシニアゲームエンジニアです。
薬学実務実習 OSCE 向けに、服薬指導から医師への疑義照会までを繋ぐシミュレーションを、
**単体で完結する 1 フォルダ**として実装してください。

## 事前に読むもの

- `codemed-x/standalone/README.md`（作り方の約束事）
- `codemed-x/standalone/01-welfare-interview/` の 3 ファイル（**必ず読む**）
- `codemed-x/docs/scenarios/04-pharmacist-inquiry.md`（設計の詳細）
- `codemed-x/schema/objectives.csv` の `pharmacist_inquiry_v1` の行

> 法令番号に注意: 疑義照会義務は**薬剤師法第24条**（第21条は調剤応需義務、
> 第28条は調剤録の記載義務）。教材内の表示・解説でもこの番号を使うこと。

## 作るもの

`codemed-x/standalone/04-pharmacist-inquiry/` に 3 ファイル。

| ファイル | 役割 |
|---|---|
| `PharmacistInquirySim.cs` | 局面の進行と画面（`OnGUI`） |
| `PharmacistScenarioData.cs` | 文言・検査値・配点 |
| `PharmacistTrainingLog.cs` | 学習履歴の記録 |

`scenario_id` は `pharmacist_inquiry_v1`、名前空間は `CodemedX.Pharmacist`。

### 守ること（①と揃える）

- **空の GameObject にスクリプトを 1 つ付けて Play するだけで動くこと。**
  Canvas、Prefab、XR パッケージ、`.asmdef` は使わない。画面は IMGUI。
- **`TrainingEvent` のフィールドは①と 1 文字も変えない。**
  `TrainingEvent` / `Payload` / ログクラスは①からコピーして名前空間だけ変える。
- `objective_id` は `PHM-*` のみ使う。
- 文言・検査値・配点は `PharmacistScenarioData.CreateDefault()` に集約する。

## 内容

### 局面

```
Counseling → LabReview → DoctorCall → PrescriptionChange → Debrief
                              │
                              └→ FailedInquiry → Debrief
```

学習者はどの時点でも「このまま調剤する」を選べるようにする。
選んだ場合は `YieldedToAuthority` として記録し `FailedInquiry` へ。

### 1. 服薬指導（Counseling）

患者との対話。「最近少し胃が痛くて…」のような訴えから有害事象の可能性を拾わせる。
拾えた場合は `objective_id = "PHM-CNS-01"` で加点する。

拾えなくても次に進めるが、その場合は後の照会で使える根拠が 1 つ減る構造にする
（症状と検査値が揃わないと医師を説得しきれない）。

### 2. 検査値の確認（LabReview）

検査値を一覧で表示し、それぞれに「疑義の証拠として添付」ボタンを置く。
異常値（例: SCr 上昇、eGFR 低下）と、正常値・無関係な項目を混ぜること。

- 正しい項目を添付 → `LabConfirmed` / `EvidenceAttached` を `PHM-LAB-01` で記録して加点
- 無関係な項目を添付 → 減点し、理由を振り返りに残す

添付した証拠は次の局面で使える選択肢を解放する。

### 3. 疑義照会（DoctorCall）

多忙な医師に電話する。医師は `Pressure`（権威勾配 0-100、初期値 40）を持つ。

| 学習者の応答 | Pressure |
|---|---|
| 検査値・症状という具体的根拠を挙げる | `-20` |
| 「念のため」「一応」など曖昧な言い方 | `+20` |
| 制限時間内に応答しない（言い淀み） | `+15` |
| 一度引き下がってから再度主張（2 チャレンジルール） | `-10` |

| 閾値 | 起きること |
|---|---|
| `Pressure >= 80` | 医師が「忙しいからそのまま出して」で電話を切る → `FailedInquiry` |
| `Pressure <= 30` | 処方変更に同意 → `PrescriptionChange` |

**応答制限時間を設ける**（10 秒以内に選ばないと `Pressure +15` して次の場面へ）。
権威勾配の圧力は即答を迫られることで生じるため、時間の要素が欠かせない。
残り秒数は画面に出してよい（`Pressure` の値は出さない）。

根拠を伴う選択肢は、対応する証拠を添付済みのときだけ有効にする。
未添付で選んだ場合は `EvidenceNotPresented` として記録し、`Pressure` を上げる。
**選択肢自体は隠さないこと。** 学習者が「今それを言えるか」を判断する構造にする。

医師の台詞は圧力をかけるが、**人格攻撃にはしない**。
学習目標は「圧力下で根拠を保つ」ことであり、ハラスメント耐性の訓練ではない。

### 4. 終了

`PrescriptionChange` に到達しても `FailedInquiry` でも、必ず振り返りまで進める。
**「どの時点で、どの根拠を示していれば通ったか」をログから示すことが教材の本体。**

### 振り返りで見せるもの

- `Pressure` の推移（対話中は非表示、ここで初めて開示する）
- 添付した証拠と、使えたはずで使わなかった証拠
- 引き下がった場合は、その直前の選択肢と、代わりに選べた選択肢

## 成果物

1. 上記 3 ファイル（そのままコンパイルが通るもの）
2. `codemed-x/standalone/README.md` の④の行を、①と同じ書式で埋める
3. 題材が監修前の仮版であることを、振り返り画面と README に明記する
