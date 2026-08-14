# Codemed-x 共通基盤

対人援助職（児童福祉・医療・看護・薬剤・公衆衛生）向け XR シミュレータ 5 テーマを、
同一の学習履歴フォーマットと同一の再利用部品の上に実装するための共通基盤。

このディレクトリは本リポジトリのデータ（医学教育モデル・コア・カリキュラム 令和4年度改訂版）
とは独立したソフトウェア成果物だが、**評価項目 `objective_id` をコアカリの `id` に
機械的に紐づける**ことで、シミュレータの学習履歴をコアカリの資質・能力の粒度で
集計できるようにしている。これがこのリポジトリに置いている理由。

## まずここから

**[`standalone/`](standalone/) に 5 テーマすべての実装がある。そのまま動く。**
フォルダを `Assets/` に置き、空の GameObject にスクリプトを 1 つ付けて Play するだけ。
Canvas も Prefab も XR パッケージも要らない。

このプロジェクトには作り方が 2 つある。

| | 単体版 `standalone/` | 共通基盤版 `unity/Assets/CodemedX/` |
|---|---|---|
| 依存 | なし。1 シナリオ = 1 フォルダ | 全シナリオが共通のアセンブリを共有 |
| 導入 | フォルダを 1 つ置くだけ | 基盤を先に入れる必要がある |
| 壊れ方 | そのシナリオだけ | 基盤が壊れると全部止まる |
| 向き | **まず動かす。個別に配る**（現在こちらで実装済み） | 5 本を同時に育てる段階になったら |

共通しているのは**学習履歴の形だけ**。どちらで作っても `TrainingEvent` の
フィールドは同じなので、LMS 側は 1 種類の受け口で 5 テーマすべてを受けられる。

迷ったら単体版から始める。詳細は [`standalone/README.md`](standalone/README.md)。

## 何が入っているか

| パス | 内容 |
|---|---|
| `standalone/` | **5 テーマの実装**。1 シナリオ = 1 フォルダで完結 |
| `schema/` | `TrainingEvent` / バッチの JSON Schema と、評価項目とコアカリ id の対応表 |
| `unity/Assets/CodemedX/Runtime/` | 5 シナリオ共通の C#（ログ送信・状態遷移・対話・観察・時間圧・採点） |
| `unity/Assets/CodemedX/Editor/` | `objectives.csv` から評価項目カタログを再生成するエディタ拡張 |
| `unity/Assets/CodemedX/Tests/EditMode/` | 共通基盤の EditMode テスト |
| `docs/` | アーキテクチャ / イベント仕様 / 5 テーマの個別設計 |
| `prompts/` | 内容の差し替え・作り直しを Claude Code に頼むときの指示（① 〜 ⑤） |
| `server/gas/` | Google スプレッドシートを受け皿にする Apps Script レシーバ |
| `tools/` | 検証スクリプト・ローカルモック LMS・`.meta` 生成 |

## セットアップ

### 1. Unity プロジェクトを用意する

手順の詳細は **[Unity セットアップ手順](docs/unity-setup.md)** にある。要点だけ書くと:

1. Unity 6 (6000.0 LTS) で **Universal 3D**（URP）テンプレートの新規プロジェクトを作る
2. このリポジトリの `codemed-x/unity/Assets/CodemedX` を、そのプロジェクトの
   `Assets/` 配下に `.meta` ごとコピーする（GUID は生成済みなので環境をまたいでも保たれる）
3. **Window > General > Test Runner** の EditMode タブで **Run All** し、全部緑になるのを確認する

**XR パッケージはこの時点では不要**。共通基盤は XR Interaction Toolkit にも OpenXR にも
依存しておらず、Unity 標準機能だけで動く。XR の設定は詰まりやすいので、
まず PC 上でロジックの動作を確認してから進めるほうが確実。

HMD で動かす段になったら、Package Manager で以下を追加する
（手順とつまずきやすい点は [docs/unity-setup.md](docs/unity-setup.md) を参照）。

| パッケージ | 名前 |
|---|---|
| XR Interaction Toolkit（3.x）— Samples の *Starter Assets* も取り込む | `com.unity.xr.interaction.toolkit` |
| OpenXR Plugin | `com.unity.xr.openxr` |
| Input System（XRIT の依存として自動で入る） | `com.unity.inputsystem` |
| Test Framework（新規プロジェクトに最初から入っている） | `com.unity.test-framework` |

> `codemed-x/unity/` には `ProjectSettings/` と `Packages/manifest.json` を意図的に含めていない。
> パッケージのバージョンを固定して配ると、Unity 側の解決と食い違ったときに
> プロジェクトが開けなくなるため。

### 2. ログ送信を設定する

設定アセットは **`Assets/CodemedX/Resources/CodemedXEventLoggerSettings.asset` として同梱済み**。
手順 1 でフォルダごとコピーしていれば、作成作業は不要。

1. Project ウィンドウでそのアセットを選び、Inspector で `Endpoint Url` に送信先を設定する。
   未設定でもアプリは動き、イベントは端末内に退避される（初期状態では Console 出力が有効）。
2. トークンは**アセットに書かない**。起動時のブートストラップから注入する。

アセットが見当たらない場合は **Tools > Codemed-x > ログ送信設定アセットを作成 or 選択** で作れる
（`Assets 右クリック > Create > Codemed-x > Event Logger Settings` でも同じものが作れるが、
Create メニューは項目が多く探しにくいため Tools 側を用意している）。
現在の状態は **Tools > Codemed-x > セットアップ状態を確認** で Console に出力できる。

> `Resources` 直下・この名前でないと `EventLogger` が自動で読み込めない。移動・改名しないこと。

```csharp
// 例: アプリ起動時
LearnerIdentity.SetFromRawIdentifier(studentNumber, saltFromSecureConfig);
EventLogger.Instance.SetAuthToken(tokenFromSecureConfig);
```

### 3. 疎通を確認する

```bash
# PC 側でモック LMS を起動
python3 codemed-x/tools/mock_lms_server.py --port 8787
# → Endpoint Url に http://<PCのIP>:8787/events を設定して Unity を再生
```

受信したイベントは `codemed-x/samples/received_events.jsonl` に溜まる。
スキーマ違反は 400 で弾かれるので、フィールドの詰め忘れがその場で分かる。

Google スプレッドシートに貯めたい場合は `server/gas/Code.gs` を参照
（この場合のみ `Event Logger Settings` の認証方式を `QueryParameter` にする）。

## 検証

```bash
# 評価項目がコアカリに実在する id を指しているか + サンプルのスキーマ適合
python3 codemed-x/tools/validate_codemedx.py

# .meta の欠落チェック（CI 向け）
python3 codemed-x/tools/generate_unity_meta.py --check
```

C# の EditMode テストは Unity の Test Runner から実行する（`CodemedX.Tests.EditMode`）。

## 5 テーマの進み方

| | 状態 | 動かすスクリプト | 設計 |
|---|---|---|---|
| ① 相談援助面接（児童相談所・生活保護・DV・MSW/PSW・ケアマネ） | **実装済み** | `WelfareInterviewSim` | [設計](docs/scenarios/01-welfare-interview.md) |
| ② 困難な対話（ACP / SPIKES） | **実装済み** | `AcpDialogueSim` | [設計](docs/scenarios/02-acp-dialogue.md) |
| ③ 夜勤・複数患者の優先順位判断 | **実装済み** | `NightShiftSim` | [設計](docs/scenarios/03-nightshift-triage.md) |
| ④ 薬剤師：服薬指導から疑義照会 | **実装済み** | `PharmacistInquirySim` | [設計](docs/scenarios/04-pharmacist-inquiry.md) |
| ⑤ ゲートキーパー：自殺リスク評価と危機介入 | **実装済み** | `GatekeeperSim` | [設計](docs/scenarios/05-gatekeeper.md) |

実体は [`standalone/`](standalone/) 以下。各フォルダを `Assets/` に置き、
空の GameObject に上記スクリプトを付けて Play するだけで動く。

題材はいずれも**監修前の仮版**。構造を体験するためのたたき台であり、
研修に使う前にそれぞれの領域の実務者によるレビューが要る。
文言は各シナリオの `*ScenarioData.cs` の `CreateDefault()` 1 箇所にまとまっているので、
C# を書かずに差し替えられる。

[`prompts/`](prompts/) には、内容を差し替えたり作り直したりするときに
Claude Code へ渡せる指示を置いてある。

## ドキュメント

- [Unity セットアップ手順](docs/unity-setup.md) — プロジェクト作成からパッケージ追加まで
- [アーキテクチャ](docs/architecture.md) — 全体構成と設計判断の理由
- [イベント仕様](docs/event-schema.md) — `TrainingEvent` の各フィールドと送信プロトコル
- [評価項目とコアカリの対応](docs/objective-mapping.md) — `objective_id` の設計と検証方法
- [CLAUDE.md](CLAUDE.md) — Claude Code 向けのコーディング規約

## ライセンス上の注意

`schema/objectives.csv` は本リポジトリのコアカリデータの `id` を参照している。
コアカリ由来のデータを利用する際は
[文部科学省ウェブサイト利用規約](https://www.mext.go.jp/b_menu/1351168.htm) に従うこと。
