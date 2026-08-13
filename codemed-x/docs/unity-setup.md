# Unity プロジェクトのセットアップ手順

## 先に結論：XR パッケージは最初は要りません

`Assets/CodemedX`（共通基盤）は **XR Interaction Toolkit にも OpenXR にも依存していません**。
使っているのは Unity 標準の機能（`Physics.Raycast`、`UnityWebRequest`、`ScriptableObject`）だけです。

| やりたいこと | 必要なパッケージ |
|---|---|
| 共通基盤をプロジェクトに入れてコンパイルを通す | **なし**（新規プロジェクトのままでよい） |
| EditMode テストを実行する | Test Framework（新規プロジェクトに最初から入っている） |
| PC 上でシナリオのロジックを組む・デバッグする | なし |
| HMD（Quest / Pico）で動かす | XR Interaction Toolkit、OpenXR Plugin、Input System |

まずは手順 1〜3 だけ行い、PC 上でロジックが動くのを確認してから 4 に進むのが確実です。
XR の設定は詰まりやすく、そこで止まると基盤の検証まで進めません。

---

## 1. プロジェクトを作る

Unity Hub → **New project** → テンプレート **Universal 3D**（旧称 3D (URP)）→ Unity 6 (6000.0 LTS) を選択して作成。

## 2. 共通基盤を入れる

このリポジトリの `codemed-x/unity/Assets/CodemedX` フォルダを、
作ったプロジェクトの `Assets/` の下にそのままコピーします。

```
<プロジェクト>/Assets/CodemedX/
    Runtime/
    Editor/
    Tests/
```

`.meta` ファイルも一緒にコピーしてください（同梱済みです）。Unity に戻ると
自動でインポートされ、コンソールにエラーが出なければ成功です。

## 3. テストを実行して確認する

**Window > General > Test Runner** → **EditMode** タブ → **Run All**。

`CodemedX.Tests.EditMode` の項目がすべて緑になれば、共通基盤は正しく入っています。
ここまで XR パッケージは一切不要です。

---

## 4. XR パッケージを追加する（HMD で動かす段になったら）

### 4-1. Package Manager を開く

**Window > Package Manager**

左のサイドバーで **Unity Registry** を選ぶと、Unity 公式パッケージの一覧が出ます。
（**In Project** は今入っているもの、**Built-in** は標準機能の一覧です。）

### 4-2. 名前で入れる（確実な方法）

検索で見つからないときは、こちらが確実です。

1. Package Manager 左上の **＋** ボタンを押す
2. **Install package by name…**（バージョンによっては *Add package by name…*）を選ぶ
3. 下記の名前を 1 つずつ入力して **Install**。バージョン欄は**空のまま**にすると、
   Unity 6 が解決した互換バージョンが入ります

| パッケージ | 入力する名前 |
|---|---|
| XR Interaction Toolkit | `com.unity.xr.interaction.toolkit` |
| OpenXR Plugin | `com.unity.xr.openxr` |
| Input System | `com.unity.inputsystem` |
| Test Framework | `com.unity.test-framework` |

補足:

- **Input System** は XR Interaction Toolkit が依存しているので、XRIT を入れると
  自動で一緒に入ります。個別に入れる必要はありません。
- **Test Framework** は新規プロジェクトに最初から入っています。
  **In Project** の一覧に見当たらないときだけ追加してください。
- Input System が入ると「バックエンドを有効にしてエディタを再起動するか」と
  聞かれます。**Yes** を選んでください（再起動します）。

### 4-3. Starter Assets を取り込む

XR Interaction Toolkit には、XR Origin やコントローラ入力の設定が
すぐ使える形で同梱されています。これを入れないと、
シーンに XR のカメラリグを一から組む必要があり大変です。

1. Package Manager で **In Project** → **XR Interaction Toolkit** を選ぶ
2. 右側のペインの **Samples** タブを開く
3. **Starter Assets** の **Import** を押す

`Assets/Samples/XR Interaction Toolkit/<バージョン>/Starter Assets/` に展開されます。
この中の **XR Interaction Setup** プレハブをシーンに置けば、
そのまま HMD で歩き回れる状態になります。

### 4-4. XR を有効化する

**Edit > Project Settings > XR Plug-in Management**

1. **Install XR Plug-in Management** を押す（未インストールの場合）
2. タブを切り替えて、使うプラットフォームで **OpenXR** にチェック
   - PC でエディタ再生・PCVR で確認する場合 → **Windows, Mac, Linux** タブ
   - Quest / Pico の実機ビルド → **Android** タブ
3. 左のツリーに現れる **OpenXR** を選び、**Interaction Profiles** に
   使うコントローラのプロファイルを **＋** で追加する
   （Quest なら *Oculus Touch Controller Profile*）
4. Android タブでは、Quest 向けの機能グループにチェックを入れる

### 4-5. 検証と切り替え

- **Project Settings > XR Plug-in Management > Project Validation** を開き、
  警告が出ていれば **Fix** / **Fix All** を押す。ここで潰しておくと実機で詰まりません。
- 実機ビルドするなら **File > Build Profiles**（Unity 6 での名称。旧 Build Settings）で
  プラットフォームを **Android** に切り替える。

---

## 代わりに manifest.json を直接編集する方法

GUI より確実です。プロジェクトを閉じて `<プロジェクト>/Packages/manifest.json` の
`dependencies` に以下を足し、Unity で開き直すと自動で解決・取得されます。
バージョンは書かず、いったん `"1.0.0"` 等と書いてしまわないよう注意してください
（存在しないバージョンを書くと解決に失敗します）。

Package Manager の **＋ > Install package by name** で名前だけ入れる方法と
結果は同じなので、GUI で入るならそちらで構いません。

---

---

## 5. ログ送信設定アセットについて

`Assets/CodemedX/Resources/CodemedXEventLoggerSettings.asset` として**同梱済み**なので、
手順 2 でフォルダごとコピーしていれば作成作業は不要です。Project ウィンドウで選び、
Inspector で `Endpoint Url` を設定してください。

見当たらない場合は **Tools > Codemed-x > ログ送信設定アセットを作成 or 選択** で作成できます。
`Assets 右クリック > Create > Codemed-x > Event Logger Settings` でも同じものが作れますが、
Create メニューは項目が非常に多く目的のものを探しにくいため、Tools 側に入口を用意しています。

**Tools メニューに「Codemed-x」が出てこない場合、スクリプトがコンパイルできていません。**
メニュー項目は C# のコンパイルが通って初めて登録されるためです。
まず Console（**Window > General > Console**）を開いて赤いエラーを確認してください。

現在の状態は **Tools > Codemed-x > セットアップ状態を確認** で Console に出力できます
（設定アセットの有無、送信先、端末内スプールの保存先）。

---

## つまずきやすい点

| 症状 | 原因と対処 |
|---|---|
| `Create > Codemed-x` や `Tools > Codemed-x` がメニューに出ない | スクリプトがコンパイルできていない。Console の赤いエラーを先に潰す。エラーが無いのに出ない場合は、コピーしたフォルダに `Runtime` / `Editor` / `Tests` と各 `.asmdef` が揃っているかを確認する |
| `Unexpected transport error from import worker`（`code=10054` 等） | アセットインポート用の別プロセスが落ちた。Unity を再起動する。頻発する場合は ① プロジェクトを OneDrive / Dropbox / ネットワークドライブの外（例 `C:\Unity\...`）へ移す ② ウイルス対策ソフトの除外にプロジェクトフォルダと Unity を追加 ③ `Library` フォルダを削除して開き直す（キャッシュなので消して安全。`Assets` と `ProjectSettings` は消さない） |
| Unity Registry に XR Interaction Toolkit が出ない | 検索欄で `XR Interaction` と入れる。それでも出なければ 4-2 の「名前で入れる」を使う |
| XRIT を入れたらコンソールに Input System 関連のエラーが出る | バックエンド切り替えの再起動がまだ。Unity を再起動する |
| Starter Assets の Import ボタンがない | **Unity Registry** ではなく **In Project** 側で選び直す。Samples タブはインストール済みパッケージにのみ出る |
| XRIT 2.x 向けの記事どおりに書いたらコンパイルエラー | 3.x で名前空間が変わっている。`UnityEngine.XR.Interaction.Toolkit.Interactables` / `.Interactors` を使う |
| `Assets/CodemedX` を入れた直後にエラーが大量に出る | `.meta` ごとコピーできているか、`Runtime` / `Editor` / `Tests` の各 `.asmdef` が揃っているかを確認する |
