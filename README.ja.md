<p align="center">
  <img src="assets/fastcopy-paste-logo.png" width="180" alt="FastCopy Paste Logo">
</p>

<h1 align="center">FastCopy Paste</h1>

<p align="center">
  <a href="https://github.com/Yukikaze1945/FastCopyPaste/releases/latest"><img src="https://img.shields.io/badge/release-20260723-0078D4" alt="Release 20260723"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-2EA44F" alt="MIT License"></a>
</p>

<p align="center">
  <a href="README.md">简体中文</a> |
  <a href="README.en.md">English</a> |
  <a href="README.ja.md">日本語</a>
</p>

FastCopy Paste は、64 ビット版 Windows 10/11 のエクスプローラーに FastCopy を統合するツールです。エクスプローラーのファイル一覧で設定したショートカットによるファイルの貼り付けを FastCopy に引き渡します。

> 本プロジェクトは FastCopy 公式プロジェクトとは関係のない独立したプロジェクトです。FastCopy の同梱や再配布は行いません。[FastCopy 公式サイト](https://fastcopy.jp/)から別途入手し、FastCopy のライセンス条件に従ってください。

## 機能

- Windows 標準の `Ctrl+C`、`Ctrl+X`、ファイル用クリップボードをそのまま使用し、既定ではエクスプローラーのファイル一覧上の `Ctrl+V` をフックします。
- タスクトレイから任意の代替ショートカットを記録できます。変更後、元の `Ctrl+V` は完全にエクスプローラー標準動作へ戻ります。
- コピーには FastCopy の `diff`、切り取りには `move` を使用し、FastCopy 標準の進行状況ウィンドウを表示します。
- 複数選択、Unicode、空白を含むパス、長いパスに対応し、ジョブを順番に処理します。
- 同名項目が存在する場合は既定でキャンセルし、明示的に確認した場合のみ結合および上書きを行います。
- ドライブのルートをコピー元にする操作、コピー元とコピー先が同一の操作、フォルダーを自分自身またはその配下へ貼り付ける操作を拒否します。
- 切り取り後のクリップボードは、移動が成功し、その間にクリップボードが変更されていない場合に限って消去します。
- タスクトレイメニューからフックの一時停止、ショートカットの記録、FastCopy のパス変更、ログ表示、終了ができます。

## 動作要件

- 64 ビット版 Windows 10 バージョン 2004 / Build 19041 以降、または 64 ビット版 Windows 11。
- 64 ビット版 FastCopy。FastCopy 5.11.3 で動作確認済みです。
- 現在の Windows ユーザーにのみインストールされ、管理者権限は不要です。
- Release の ZIP は自己完結型 x64 ビルドのため、利用者が .NET Runtime を別途インストールする必要はありません。

Windows 10 Build 19041 未満、32 ビット版 Windows、ZIP/MTP/ごみ箱などの仮想 Shell フォルダーには対応していません。FastCopy 独自のコンテキストメニューはそのまま維持され、本アプリは変更しません。

## インストール

1. GitHub Releases から `FastCopyPaste-current-user.zip` をダウンロードし、ZIP 全体を展開します。
2. 展開先で PowerShell を開き、次のコマンドを実行します。

```powershell
powershell -ExecutionPolicy Bypass -File .\Install.ps1
```

インストーラーは `PATH`、登録済みアプリケーションのパス、一般的なインストール先から `FastCopy.exe` を検索します。見つからない場合はファイル選択ダイアログを表示します。ポータブル版の場所を明示的に指定することもできます。

```powershell
powershell -ExecutionPolicy Bypass -File .\Install.ps1 `
  -FastCopyPath "D:\Tools\FastCopy\FastCopy.exe"
```

プログラムは `%LOCALAPPDATA%\Programs\FastCopyPaste` にインストールされ、設定とログは `%LOCALAPPDATA%\FastCopyPaste` に保存されます。インストーラーは現在のユーザー用ログオン自動起動を登録し、タスクトレイ常駐 Host を起動します。バージョン 1.2.0 以前からの更新時は、廃止されたメニュー用パッケージも登録解除します。再実行すると安全に上書きインストールできます。

インターネットから取得したスクリプトが Windows によってブロックされた場合は、ZIP を右クリックして「プロパティ」から「許可する」または「ブロックの解除」を選択し、再度展開してください。現在のリリース バイナリにはコード署名がないため、発行元不明の警告が表示される場合があります。本リポジトリの Releases からのみダウンロードし、必要に応じて公開されている SHA-256 を確認してください。

## 使い方

1. エクスプローラーでファイルを選択し、標準の `Ctrl+C` または `Ctrl+X` を実行します。
2. 通常のファイルシステム上のフォルダーへ移動します。
3. ファイル一覧で現在のショートカット（既定は `Ctrl+V`）を押します。

アドレスバー、検索ボックス、ほかのアプリ、仮想フォルダー、ファイル以外のクリップボード、フックの一時停止中は処理を横取りしません。同じフォルダー内へのコピーはエクスプローラー標準の処理へ戻します。FastCopy の終了コードが `0` の場合のみ成功とみなし、失敗時はコピー元とクリップボードを保持します。

## 設定とログ

設定ファイルは `%LOCALAPPDATA%\FastCopyPaste\settings.json` に保存されます。

```json
{
  "fastCopyPath": "D:\\Tools\\FastCopy\\FastCopy.exe",
  "hookEnabled": true,
  "hotkey": {
    "virtualKey": 86,
    "modifiers": 1
  }
}
```

ショートカットの記録、FastCopy のパス変更、フックの一時停止にはタスクトレイメニューを使用してください。修飾キー以外の任意のキーと `Ctrl` / `Alt` / `Shift` / `Win` を自由に組み合わせられます。修飾キー単独、および通常のアプリに渡されない Windows のセキュリティ キー操作のみ使用できません。ログは `%LOCALAPPDATA%\FastCopyPaste\Logs` に保存され、自動的に外部へ送信されることはありません。

## アンインストール

展開した Release フォルダーから次のコマンドを実行します。

```powershell
powershell -ExecutionPolicy Bypass -File .\Uninstall.ps1
```

アンインストールでは、自動起動、インストール先、本ツールの設定とログ、および旧バージョンのメニュー用パッケージ登録を削除します。FastCopy の削除や変更は行いません。

## ソースからのビルド

ビルドには .NET 8 SDK 以降が必要です。

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Build.ps1
```

スクリプトは単体テストを実行し、自己完結型 .NET Host を発行して、`artifacts\FastCopyPaste-current-user.zip` を生成します。Release ZIP に PDB、FastCopy、ユーザー設定は含まれません。

テストだけを個別に実行することもできます。

```powershell
dotnet run --project .\tests\FastCopyPaste.Tests\FastCopyPaste.Tests.csproj -c Release
powershell -ExecutionPolicy Bypass -File .\scripts\Test-Integration.ps1 `
  -FastCopyPath "D:\Tools\FastCopy\FastCopy.exe"
```

統合テストでは FastCopy のコマンドライン実行ファイルを一時フォルダーへコピーするため、通常利用している FastCopy の設定は変更されません。

## セキュリティとプライバシー

- 常駐 Host は設定されたショートカットが押された場合のみアクティブなエクスプローラーのファイル一覧を解決し、エクスプローラーへコードを注入しません。
- FastCopy の引数は Shell コマンド文字列として連結せず、`ProcessStartInfo.ArgumentList` を使用して渡します。
- ファイル操作はすべてローカルで完結します。テレメトリ、ネットワーク通信、自動更新機能はありません。
- FastCopy 自体は設定に応じてログや履歴を書き込む場合があります。本プロジェクトはそれらを削除しません。

## 既知の制限

- x64 Windows 11 と FastCopy 5.11.3 の組み合わせで動作確認済みです。Windows 10 用のマニフェストとインストール処理は有効になっていますが、実機での受け入れテストはまだ必要です。
- ほかの FastCopy 5.x も互換性があると見込まれますが、個別には検証していません。
- 未署名のプログラムでは、SmartScreen や PowerShell の取得元に関する警告が表示される場合があります。
- Windows Update によりエクスプローラーのフォーカス構造が変わる可能性があります。不具合を報告する際は Host のログを添付してください。
- 現在のアプリ UI は主に簡体字中国語です。プロジェクト文書は簡体字中国語、英語、日本語で提供しています。

## ライセンス

本プロジェクトは [MIT License](LICENSE) で公開されています。FastCopy は独立したサードパーティ製ソフトウェアであり、本プロジェクトのライセンス対象ではなく、Release ZIP にも含まれません。
