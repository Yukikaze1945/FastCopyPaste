using System.Diagnostics;

namespace FastCopyPaste.Host;

internal sealed class HostApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _toggleItem;
    private readonly AppSettings _settings;
    private readonly SettingsStore _settingsStore;
    private readonly AppLog _log;
    private readonly ClipboardService _clipboard;
    private readonly ExplorerContextService _explorer;
    private readonly PasteCoordinator _coordinator;
    private readonly KeyboardHook _keyboardHook;
    private readonly PipeServer _pipeServer;
    private readonly SynchronizationContext _uiContext;

    public HostApplicationContext(string? initialTarget)
    {
        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        _uiContext = SynchronizationContext.Current!;
        _settingsStore = new SettingsStore();
        _settings = _settingsStore.Load();
        _log = new AppLog();
        _clipboard = new ClipboardService();
        _explorer = new ExplorerContextService();

        _toggleItem = new ToolStripMenuItem();
        _toggleItem.Click += ToggleHook;
        var chooseFastCopyItem = new ToolStripMenuItem("设置 FastCopy 路径...", null, ChooseFastCopyPath);
        var openLogsItem = new ToolStripMenuItem("查看日志", null, OpenLogs);
        var exitItem = new ToolStripMenuItem("退出", null, ExitApplication);
        var menu = new ContextMenuStrip();
        menu.Items.AddRange([_toggleItem, chooseFastCopyItem, openLogsItem, new ToolStripSeparator(), exitItem]);

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "FastCopy 粘贴",
            ContextMenuStrip = menu,
            Visible = true
        };

        _coordinator = new PasteCoordinator(
            _clipboard,
            new FastCopyRunner(),
            _settings,
            _log,
            Notify);

        _keyboardHook = new KeyboardHook(_settings.Hotkey, TryInterceptHotkey);
        ApplyHookSetting(showError: true);

        _pipeServer = new PipeServer(
            target =>
            {
                var ownerWindow = NativeMethods.GetForegroundWindow();
                _uiContext.Post(
                    _ => _coordinator.TryEnqueue(target, PasteOrigin.Menu, ownerWindow),
                    null);
            },
            _log);
        _pipeServer.Start();

        if (initialTarget is not null)
        {
            var ownerWindow = NativeMethods.GetForegroundWindow();
            _uiContext.Post(
                _ => _coordinator.TryEnqueue(initialTarget, PasteOrigin.Menu, ownerWindow),
                null);
        }

        _log.Info("Host started.");
    }

    private bool TryInterceptHotkey()
    {
        if (!_settings.HookEnabled || !_clipboard.HasFileDropList() ||
            !KeyboardHook.IsExplorerFileView(out var foregroundWindow, out var focusedWindow))
        {
            return false;
        }

        _uiContext.Post(_ => HandleHotkeyPaste(foregroundWindow, focusedWindow), null);
        return true;
    }

    private void HandleHotkeyPaste(nint foregroundWindow, nint focusedWindow)
    {
        if (!_explorer.TryGetCurrentDirectory(foregroundWindow, focusedWindow, out var directory) ||
            directory is null)
        {
            _log.Info($"Hotkey target resolution failed for HWND {foregroundWindow}.");
            KeyboardHook.ReplayGesture(_keyboardHook.Gesture);
            return;
        }

        if (!_coordinator.TryEnqueue(
                directory,
                PasteOrigin.Hotkey,
                foregroundWindow,
                _keyboardHook.Gesture))
        {
            _log.Info("Hotkey clipboard capture failed; replaying native paste.");
            KeyboardHook.ReplayGesture(_keyboardHook.Gesture);
        }
    }

    private void ToggleHook(object? sender, EventArgs args)
    {
        _settings.HookEnabled = !_settings.HookEnabled;
        _settingsStore.Save(_settings);
        ApplyHookSetting(showError: true);
    }

    private void ApplyHookSetting(bool showError)
    {
        try
        {
            if (_settings.HookEnabled)
            {
                _keyboardHook.Enable();
            }
            else
            {
                _keyboardHook.Disable();
            }
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception)
        {
            _settings.HookEnabled = false;
            _settingsStore.Save(_settings);
            _log.Error("Keyboard hook installation failed.", ex);
            if (showError)
            {
                Notify("Ctrl+V 接管未启动", ex.Message, ToolTipIcon.Error);
            }
        }

        _toggleItem.Text = _settings.HookEnabled ? "暂停 Ctrl+V 接管" : "继续 Ctrl+V 接管";
        _notifyIcon.Text = _settings.HookEnabled ? "FastCopy 粘贴（已启用）" : "FastCopy 粘贴（已暂停）";
    }

    private void ChooseFastCopyPath(object? sender, EventArgs args)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "选择 FastCopy.exe",
            Filter = "FastCopy (FastCopy.exe)|FastCopy.exe|可执行文件 (*.exe)|*.exe",
            FileName = _settings.FastCopyPath,
            CheckFileExists = true
        };
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _settings.FastCopyPath = dialog.FileName;
            _settingsStore.Save(_settings);
            Notify("FastCopy 路径已更新", dialog.FileName, ToolTipIcon.Info);
        }
    }

    private void OpenLogs(object? sender, EventArgs args)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            ArgumentList = { _log.DirectoryPath },
            UseShellExecute = true
        });
    }

    private void Notify(string title, string text, ToolTipIcon icon)
    {
        _notifyIcon.ShowBalloonTip(4000, title, text, icon);
    }

    private void ExitApplication(object? sender, EventArgs args) => ExitThread();

    protected override void ExitThreadCore()
    {
        _log.Info("Host stopping.");
        _pipeServer.Dispose();
        _keyboardHook.Dispose();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        base.ExitThreadCore();
    }
}
