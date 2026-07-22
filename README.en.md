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

FastCopy Paste integrates FastCopy with File Explorer on 64-bit Windows 10 and Windows 11. It routes file-paste operations triggered by a configurable shortcut in Explorer's file view through FastCopy.

> This is an independent project and is not affiliated with the FastCopy project. FastCopy is not bundled or redistributed. Download it separately from the [official FastCopy website](https://fastcopy.jp/) and comply with its license terms.

## Features

- Keeps native `Ctrl+C`, `Ctrl+X`, and Windows file clipboard behavior; `Ctrl+V` is the default Explorer interception shortcut.
- Records any replacement shortcut from the tray; after changing it, native `Ctrl+V` is returned completely to Explorer.
- Uses FastCopy `diff` for copy operations and `move` for cut operations while displaying FastCopy's native progress window.
- Supports multiple selections, Unicode paths, spaces, and long paths; jobs are processed sequentially.
- Cancels on name conflicts by default and merges/overwrites only after explicit confirmation.
- Rejects drive-root sources, source/target identity, and attempts to paste a directory into itself or one of its descendants.
- Clears a cut clipboard only after a successful move and only if the clipboard has not changed in the meantime.
- Provides tray commands to pause interception, record the shortcut, change the FastCopy path, open logs, or exit.

## Requirements

- 64-bit Windows 10 version 2004 / Build 19041 or later, or 64-bit Windows 11.
- A 64-bit FastCopy installation; FastCopy 5.11.3 has been tested.
- Installation is per-user and does not require administrator privileges.
- Release ZIP files are self-contained x64 builds; users do not need to install the .NET Runtime separately.

Windows 10 builds earlier than 19041, 32-bit Windows, and virtual Shell locations such as ZIP archives, MTP devices, and the Recycle Bin are not supported. FastCopy's own context menu remains separate and is not modified by this application.

## Installation

1. Download `FastCopyPaste-current-user.zip` from GitHub Releases and extract the entire archive.
2. Open PowerShell in the extracted directory and run:

```powershell
powershell -ExecutionPolicy Bypass -File .\Install.ps1
```

The installer searches `PATH`, registered application paths, and common installation directories for `FastCopy.exe`. If it cannot find FastCopy, it opens a file picker. A portable installation can also be specified explicitly:

```powershell
powershell -ExecutionPolicy Bypass -File .\Install.ps1 `
  -FastCopyPath "D:\Tools\FastCopy\FastCopy.exe"
```

The application is installed to `%LOCALAPPDATA%\Programs\FastCopyPaste`; settings and logs are stored in `%LOCALAPPDATA%\FastCopyPaste`. The installer registers a current-user logon startup entry and starts the tray host. Upgrading from version 1.2.0 or earlier also unregisters the retired menu package. Running the installer again performs an in-place replacement safely.

If Windows blocks a script downloaded from the internet, right-click the ZIP, open **Properties**, select **Unblock**, and extract it again. Release binaries are currently unsigned, so Windows may display an unknown-publisher warning. Download releases only from this repository and verify the published SHA-256 when appropriate.

## Usage

1. Select files in File Explorer and use the native `Ctrl+C` or `Ctrl+X` command.
2. Navigate to a normal file-system directory.
3. Press the configured shortcut (`Ctrl+V` by default) in the file view.

The address bar, search box, other applications, virtual folders, non-file clipboard data, and paused state are not intercepted. Copying within the same directory is passed back to Explorer. A FastCopy exit code of `0` is required for success; the source and clipboard are retained after a failure.

## Configuration and logs

Settings are stored in `%LOCALAPPDATA%\FastCopyPaste\settings.json`:

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

Use the tray menu to record the shortcut, change the FastCopy path, or pause interception. Any non-modifier key may be combined freely with `Ctrl`, `Alt`, `Shift`, and `Win`; modifier-only gestures and secure Windows combinations that applications never receive cannot be used. Logs are stored in `%LOCALAPPDATA%\FastCopyPaste\Logs` and are never uploaded automatically.

## Uninstallation

Run the following command from the extracted release directory:

```powershell
powershell -ExecutionPolicy Bypass -File .\Uninstall.ps1
```

Uninstallation removes the startup entry, installation directory, this application's settings and logs, and any legacy menu package registration. It does not delete or modify FastCopy.

## Building from source

The build environment requires the .NET 8 SDK or later:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Build.ps1
```

The script runs unit tests, publishes the self-contained .NET Host, and produces `artifacts\FastCopyPaste-current-user.zip`. The release archive does not contain PDB files, FastCopy, or user configuration.

Tests can also be run separately:

```powershell
dotnet run --project .\tests\FastCopyPaste.Tests\FastCopyPaste.Tests.csproj -c Release
powershell -ExecutionPolicy Bypass -File .\scripts\Test-Integration.ps1 `
  -FastCopyPath "D:\Tools\FastCopy\FastCopy.exe"
```

The integration test copies the FastCopy command-line executable to a temporary directory so that the normal FastCopy configuration is not modified.

## Security and privacy

- The resident Host resolves the active Explorer file view only after the configured shortcut is pressed; no code is injected into Explorer.
- FastCopy arguments are passed with `ProcessStartInfo.ArgumentList` instead of being concatenated into a Shell command.
- All file operations remain local. The project has no telemetry, network communication, or automatic update service.
- FastCopy itself may write logs and history according to its settings; this project does not remove them.

## Known limitations

- The x64 Windows 11 and FastCopy 5.11.3 combination has been verified. Windows 10 compatibility is enabled in the manifest and installer but still requires real-machine acceptance testing.
- Other FastCopy 5.x versions are expected to be compatible but have not been tested individually.
- Unsigned binaries may trigger SmartScreen or PowerShell origin warnings.
- Windows updates may change Explorer's focus structure. Attach the Host log when reporting an issue.
- The application UI is currently primarily Simplified Chinese; project documentation is available in Simplified Chinese, English, and Japanese.

## License

This project is released under the [MIT License](LICENSE). FastCopy is separate third-party software and is not covered by this project's license or included in its release archives.
