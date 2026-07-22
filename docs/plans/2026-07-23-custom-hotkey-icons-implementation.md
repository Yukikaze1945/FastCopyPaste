# Custom Hotkey and Icon Integration Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Ship FastCopy Paste 1.2.0 with a replacement-style configurable paste shortcut and consistent project icons across the Host, tray, package, and Explorer command.

**Architecture:** Represent a shortcut as one immutable virtual-key gesture plus a modifier bitmask. The existing low-level keyboard hook will match the configured gesture exactly, temporarily switch into capture mode for the settings dialog, and replay the same gesture when target resolution falls back to Explorer. Persist the gesture in the existing JSON settings with `Ctrl+V` as the migration-safe default; derive every application icon from the committed transparent project Logo.

**Tech Stack:** .NET 8 WinForms, Win32 `WH_KEYBOARD_LL`/`SendInput`, System.Text.Json, native C++ `IExplorerCommand`, PowerShell release packaging, x64 Windows 10/11.

---

### Task 1: Add a testable hotkey model

**Files:**
- Create: `src/FastCopyPaste.Host/HotkeyGesture.cs`
- Modify: `tests/FastCopyPaste.HostSmoke/Program.cs`

**Steps:**

1. Add smoke assertions for default `Ctrl+V`, exact modifier matching, modifier-key rejection, arbitrary unmodified keys, Windows-key combinations, and readable display text.
2. Run `dotnet run --project tests/FastCopyPaste.HostSmoke/FastCopyPaste.HostSmoke.csproj -c Release -- --hotkey-tests` and confirm it fails because the model does not exist.
3. Implement `HotkeyModifiers` flags and immutable `HotkeyGesture` with `Default`, `IsModifierKey`, `IsUsable`, `Matches`, and `ToDisplayString`.
4. Rerun the smoke command and require every assertion to pass.

### Task 2: Persist and migrate the shortcut

**Files:**
- Modify: `src/FastCopyPaste.Host/AppSettings.cs`
- Modify: `tests/FastCopyPaste.HostSmoke/Program.cs`

**Steps:**

1. Add serialization tests for `{ "hotkey": { "virtualKey": 86, "modifiers": 1 } }`, missing legacy settings, and invalid modifier-only settings.
2. Add `Hotkey` to `AppSettings`; normalize missing or unusable values to `HotkeyGesture.Default` during load.
3. Keep the existing `fastCopyPath` and `hookEnabled` properties backward compatible.
4. Run HostSmoke hotkey tests and inspect a temporary serialized settings round trip.

### Task 3: Generalize interception, replay, and capture

**Files:**
- Modify: `src/FastCopyPaste.Host/KeyboardHook.cs`
- Modify: `src/FastCopyPaste.Host/NativeMethods.cs`
- Modify: `src/FastCopyPaste.Host/HostApplicationContext.cs`
- Test: `tests/FastCopyPaste.HostSmoke/Program.cs`

**Steps:**

1. Replace hard-coded `VkV`, `_handledVDown`, and `ReplayVKey` behavior with a current `HotkeyGesture`, exact modifier matching, generic key-down tracking, and `ReplayGesture`.
2. Preserve injected-event protection using `ReplayExtraInfo` and suppress the matching main-key up event only after a successful interception.
3. Add temporary capture mode: modifier key presses update state; the next non-modifier key completes the gesture and is swallowed so Windows does not execute it while recording.
4. Make changing the gesture live and thread-safe without reinstalling the hook.
5. On Explorer target-resolution failure, replay the captured gesture; if `Ctrl+V` is no longer configured, native `Ctrl+V` must pass through untouched.
6. Run HostSmoke tests and build the Host with warnings treated as errors.

### Task 4: Add the shortcut recorder dialog

**Files:**
- Create: `src/FastCopyPaste.Host/HotkeyDialog.cs`
- Modify: `src/FastCopyPaste.Host/HostApplicationContext.cs`
- Modify: `tests/FastCopyPaste.HostSmoke/Program.cs`

**Steps:**

1. Add a tray command named `设置快捷键...` showing the active gesture.
2. Build a DPI-aware modal WinForms dialog with a large readout, `录制新快捷键`, `恢复默认`, `保存`, and `取消` controls.
3. Allow every non-modifier virtual key with any or no modifier combination. Explain that modifier-only gestures and secure Windows sequences cannot be recorded; do not impose policy restrictions on ordinary keys.
4. Enable the hook temporarily while recording if interception is paused, then restore its prior enabled state when the dialog closes.
5. Save only after explicit confirmation, update the hook immediately, and refresh tray text without restarting the Host.
6. Add `--hotkey-dialog` to HostSmoke and visually inspect default, recording, reset, and cancel states.

### Task 5: Integrate the project icon

**Files:**
- Create: `assets/FastCopyPaste.ico`
- Modify: `src/FastCopyPaste.Host/FastCopyPaste.Host.csproj`
- Modify: `src/FastCopyPaste.Host/HostApplicationContext.cs`
- Modify: `src/FastCopyPaste.Shell/ShellCommand.cpp`
- Modify: `scripts/Build.ps1`

**Steps:**

1. Generate a multi-size ICO from `assets/fastcopy-paste-logo.png` with 16, 20, 24, 32, 40, 48, 64, and 256-pixel PNG frames.
2. Set the Host `ApplicationIcon` and load that embedded icon for the notification area instead of `SystemIcons.Application`.
3. Return the installed Host executable icon from `IExplorerCommand::GetIcon`.
4. Replace the generated green placeholder package images with high-quality transparent resizes of the committed Logo.
5. Build the managed Host and native x64 Shell DLL; inspect the executable, tray, package images, and Explorer menu icon.

### Task 6: Version, documentation, packaging, and regression

**Files:**
- Modify: `src/FastCopyPaste.Host/FastCopyPaste.Host.csproj`
- Modify: `src/FastCopyPaste.Host/app.manifest`
- Modify: `packaging/AppxManifest.xml`
- Modify: `README.md`
- Modify: `README.en.md`
- Modify: `README.ja.md`

**Steps:**

1. Bump product/package versions from 1.1.1 to 1.2.0.
2. Document replacement semantics: choosing another shortcut returns `Ctrl+V` to native Explorer behavior.
3. Run `scripts/Build.ps1`, `scripts/Test-Integration.ps1 -FastCopyPath 'F:\FastCopy\FastCopy.exe'`, install the resulting bundle, and run `scripts/Test-Installed.ps1`.
4. Verify arbitrary shortcuts, no-file pass-through, paused state, address/search fields, multi-tab Explorer targets, and shortcut persistence after restart.
5. Confirm the release ZIP contains the ICO, package Logo variants, updated README files, and no FastCopy binary.
6. Commit the implementation, push `codex/custom-hotkey-icons`, merge to `main` only after all checks pass, and leave formal Release creation for an explicit release request.
