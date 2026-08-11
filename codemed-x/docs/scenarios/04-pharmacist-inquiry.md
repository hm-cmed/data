# ④ 薬剤師：服薬指導から疑義照会までを繋ぐ多者間連鎖ケース

`scenario_id`: `pharmacist_inquiry_v1`

## 学習目標と準拠基準

- **薬剤師法第24条**（処方箋中の疑義。「処方箋中に疑わしい点があるときは、
  その処方箋を交付した医師等に問い合わせて、その疑わしい点を確かめた後でなければ、
  これによつて調剤してはならない」）
- 薬学実務実習前の対人業務・臨床アセスメント（OSCE）
- チーム STEPPS の 2 チャレンジルール（権威勾配下での主張）

> 当初の設計メモでは「薬剤師法第21条」としていたが、第21条は調剤応需義務であり、
> 疑義照会義務は**第24条**。法令番号は学習内容そのものなので訂正して採用している。
> （調剤録の記載義務は第28条。`PHM-DOC-01` の準拠先はこちら。）

中核は **患者の訴え → 検査値 → 疑義の同定 → 権威勾配下での照会** という
連鎖を最後まで切らさずに繋げること。どこか 1 つでも欠けると患者に薬が渡る。

## 状態遷移

```
Counseling ─▶ LabReview ─▶ DoctorCall ─▶ DialogueConflict ─┬─▶ PrescriptionChange ─▶ Completed
                                                            │
                                                            └─▶ FailedInquiry（即時終了）
```

| 状態 | 内容 |
|---|---|
| `Counseling` | 服薬指導。患者の「最近少し胃が痛くて…」から有害事象の可能性を拾う |
| `LabReview` | 電子カルテ・血液検査データの確認。異常値を「疑義の証拠」としてアタッチ |
| `DoctorCall` | 多忙な医師へ電話 |
| `DialogueConflict` | 「いつも通りの処方だが何か問題ある？」という圧力への対応 |
| `PrescriptionChange` | 処方変更の合意 |
| `FailedInquiry` | 医師が電話を切る／学習者が引き下がる。**即時終了** |

`Counseling` で訴えを拾えなかった場合でも `LabReview` へは進めるが、
検査値だけでは根拠が弱く、`DialogueConflict` で医師を説得できない分岐になる。

## 隠れパラメータ

医師 `doctor` に `AffectActor` を付け、`Pressure`（権威勾配 0-100）を使う。

| 学習者の応答 | Pressure |
|---|---|
| 検査値・症状という具体的根拠を挙げる | `-20` |
| 「念のため」「一応」など曖昧な言い方 | `+20` |
| 沈黙・言い淀み（制限時間内に応答しない） | `+15` |
| 一度引き下がってから再度主張（2 チャレンジルール） | `-10` |

| 閾値 | 状態名 | 起きること |
|---|---|---|
| `Pressure >= 80` | `Dismissive` | 「忙しいからそのまま出して」で電話が切れる → `FailedInquiry` |
| `Pressure <= 30` | `Receptive` | 処方変更に同意 → `PrescriptionChange` |

学習者が自分から「わかりました、このまま調剤します」を選んだ場合は
`YieldedToAuthority` として記録し、`FailedInquiry` へ。
**これがこのシナリオでもっとも重要な失敗パターン**（知識ではなく行動の失敗）。

## 証拠のアタッチ

`LabReview` では、検査値パネルの各項目を `ObservationTarget` として配置する。
学習者が正しい項目（例: SCr 上昇、eGFR 低下）を選ぶと `LabConfirmed` /
`EvidenceAttached` が記録され、`DialogueConflict` で「根拠あり」の選択肢が解放される。

`DialogueChoice.requirements` に「証拠アタッチ済み」を条件として設定し、
未アタッチで根拠を主張しようとした場合は `EvidenceNotPresented` として扱う。

## 評価ルーブリック

| objective_id | 測る対象 |
|---|---|
| `PHM-CNS-01` | 服薬指導での有害事象の聴取 |
| `PHM-LAB-01` | 臓器障害と薬物動態の観点での検査値評価 |
| `PHM-ADR-01` | 有害作用・相互作用の同定 |
| `PHM-INQ-01` | 疑義を確認するまで調剤しない（第24条） |
| `PHM-SBR-01` | SBAR による簡潔な照会（**照会の所要時間**とキーワードの充足） |
| `PHM-AUT-01` | 権威勾配下での主張の維持（`YieldedToAuthority` の有無） |
| `PHM-DOC-01` | 照会内容と結果の記録 |

## 主なログイベント

| event_type | payload の主な内容 |
|---|---|
| `LabConfirmed` | `marker`, `value`, `attached_as_evidence` |
| `EvidenceAttached` | `marker`, `correct` |
| `DialogueSelected` | `node`, `choice_index`, `doctor_pressure` |
| `EscalationPerformed` | `sbar_complete`, `duration_sec` |
| `ScenarioFailed` | `reason`（`FailedInquiry`）, `authority_pressure` |

## 実装メモ

- デスクトップ UI 主体のシナリオなので `locomotion_mode` は `desktop`。
  VR 版を作る場合も、検査値の読み取りは 2D パネルのままにする（可読性のため）。
- 電話フェーズには**応答制限時間**を設ける（例: 10 秒以内に返答しないと `Pressure +15`）。
  権威勾配の圧力は「即答を迫られる」ことで生じるため、時間要素が欠かせない。
- 医師の台詞はプレッシャーをかけるが、**人格攻撃にはしない**。
  学習目標は「圧力下で根拠を保つ」ことであり、ハラスメント耐性の訓練ではない。
- `FailedInquiry` でも必ずデブリーフィングまで進める。ここで
  「どの時点で根拠を示せていれば通ったか」をログから提示することが教材の本体。
