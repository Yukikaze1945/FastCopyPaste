using FastCopyPaste.Core;

namespace FastCopyPaste.Host;

internal enum PasteOrigin
{
    Hotkey,
    Menu
}

internal sealed record PasteJob(
    ClipboardSnapshot Clipboard,
    string TargetDirectory,
    PasteOrigin Origin,
    nint OwnerWindow,
    HotkeyGesture? ReplayGesture);

internal sealed class PasteCoordinator
{
    private readonly Queue<PasteJob> _queue = new();
    private readonly ClipboardService _clipboard;
    private readonly FastCopyRunner _runner;
    private readonly AppSettings _settings;
    private readonly AppLog _log;
    private readonly Action<string, string, ToolTipIcon> _notify;
    private bool _processing;

    public PasteCoordinator(
        ClipboardService clipboard,
        FastCopyRunner runner,
        AppSettings settings,
        AppLog log,
        Action<string, string, ToolTipIcon> notify)
    {
        _clipboard = clipboard;
        _runner = runner;
        _settings = settings;
        _log = log;
        _notify = notify;
    }

    public bool TryEnqueue(
        string targetDirectory,
        PasteOrigin origin,
        nint ownerWindow = default,
        HotkeyGesture? replayGesture = null)
    {
        if (!_clipboard.TryCapture(out var snapshot) || snapshot is null)
        {
            if (origin == PasteOrigin.Menu)
            {
                _notify("FastCopy 粘贴", "剪贴板中没有文件或目录。", ToolTipIcon.Info);
            }

            return false;
        }

        _queue.Enqueue(new PasteJob(
            snapshot,
            targetDirectory,
            origin,
            ownerWindow,
            replayGesture));
        _log.Info($"Queued {snapshot.Mode}: {snapshot.Sources.Count} source(s) -> {targetDirectory}");
        if (!_processing)
        {
            _ = ProcessQueueAsync();
        }

        return true;
    }

    private async Task ProcessQueueAsync()
    {
        _processing = true;
        try
        {
            while (_queue.TryDequeue(out var job))
            {
                await ProcessJobAsync(job);
            }
        }
        finally
        {
            _processing = false;
        }
    }

    private async Task ProcessJobAsync(PasteJob job)
    {
        var plan = PastePlanner.Create(job.Clipboard.Sources, job.TargetDirectory, job.Clipboard.Mode);
        if (plan.Status == PastePlanStatus.PassThrough)
        {
            if (job.Origin == PasteOrigin.Hotkey)
            {
                KeyboardHook.ReplayGesture(job.ReplayGesture ?? HotkeyGesture.Default);
            }
            else
            {
                MessageBox.Show(
                    plan.Message + Environment.NewLine + "请在资源管理器中使用原生 Ctrl+V。",
                    "FastCopy 粘贴",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }

            return;
        }

        if (plan.Status == PastePlanStatus.Invalid)
        {
            _notify("FastCopy 粘贴已取消", plan.Message ?? "路径验证失败。", ToolTipIcon.Warning);
            _log.Info($"Rejected paste: {plan.Message}");
            return;
        }

        if (!File.Exists(_settings.FastCopyPath))
        {
            _notify("找不到 FastCopy", _settings.FastCopyPath, ToolTipIcon.Error);
            _log.Error($"FastCopy executable missing: {_settings.FastCopyPath}");
            return;
        }

        if (plan.Conflicts.Count > 0)
        {
            if (ShowConflictDialog(plan.Conflicts, job.OwnerWindow) != DialogResult.OK)
            {
                _log.Info("Paste canceled at conflict confirmation.");
                return;
            }
        }

        var command = FastCopyCommandFactory.Create(
            _settings.FastCopyPath,
            plan.Mode,
            plan.Sources,
            plan.TargetDirectory);
        try
        {
            _log.Info($"Starting FastCopy {plan.Mode}: {plan.Sources.Count} source(s) -> {plan.TargetDirectory}");
            var exitCode = await _runner.RunAsync(command, CancellationToken.None);
            var succeeded = exitCode == 0;
            _log.Info($"FastCopy exited with code {exitCode}.");
            if (succeeded)
            {
                if (ClipboardSequencePolicy.ShouldClear(
                        plan.Mode,
                        true,
                        job.Clipboard.SequenceNumber,
                        _clipboard.GetSequenceNumber()))
                {
                    _clipboard.TryClear();
                }

                _notify("FastCopy 已完成", $"已处理 {plan.Sources.Count} 个源项目。", ToolTipIcon.Info);
            }
            else
            {
                _notify("FastCopy 失败", $"退出码：{exitCode}。剪贴板已保留。", ToolTipIcon.Error);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            _log.Error("FastCopy execution failed.", ex);
            _notify("FastCopy 启动失败", ex.Message, ToolTipIcon.Error);
        }
    }

    private static DialogResult ShowConflictDialog(
        IReadOnlyList<string> conflicts,
        nint ownerWindow)
    {
        using var dialog = new ConflictDialog(conflicts);
        if (ownerWindow != nint.Zero && NativeMethods.IsWindow(ownerWindow))
        {
            return dialog.ShowDialog(new WindowHandleOwner(ownerWindow));
        }

        return dialog.ShowDialog();
    }
}
