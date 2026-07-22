using System.Text.Json;
using FastCopyPaste.Host;

if (args.Length == 1 && string.Equals(args[0], "--conflict-dialog", StringComparison.OrdinalIgnoreCase))
{
    return RunInSta(() =>
    {
        System.Windows.Forms.Application.SetHighDpiMode(System.Windows.Forms.HighDpiMode.PerMonitorV2);
        System.Windows.Forms.Application.EnableVisualStyles();
        System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
        using var dialog = new ConflictDialog(
        [
            @"D:\录像\闪耀色彩\水团120\01.青空 Jumping Heart.mkv",
            @"F:\video-compare\README with a deliberately long conflict name for DPI testing.md"
        ]);
        return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK ? 0 : 1;
    });
}

if (args.Length == 1 && string.Equals(args[0], "--inspect-conflict-dialog", StringComparison.OrdinalIgnoreCase))
{
    return RunInSta(InspectConflictDialog);
}

if (args.Length is < 1 or > 2 || !long.TryParse(args[0], out var handleValue))
{
    Console.Error.WriteLine(
        "Usage: FastCopyPaste.HostSmoke <explorer-hwnd> [focused-hwnd] | --conflict-dialog");
    return 2;
}

var foregroundWindow = new nint(handleValue);
var focusedWindow = nint.Zero;
if (args.Length == 2 && long.TryParse(args[1], out var focusedHandleValue))
{
    focusedWindow = new nint(focusedHandleValue);
}
else
{
    var threadId = NativeMethods.GetWindowThreadProcessId(foregroundWindow, out _);
    var info = new NativeMethods.GuiThreadInfo
    {
        Size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.GuiThreadInfo>()
    };
    if (!NativeMethods.GetGUIThreadInfo(threadId, ref info))
    {
        Console.Error.WriteLine("Could not resolve the Explorer focus window.");
        return 1;
    }

    focusedWindow = info.FocusWindow;
}

var resolved = false;
string? directory = null;
RunInSta(() =>
{
    var service = new ExplorerContextService();
    resolved = service.TryGetCurrentDirectory(foregroundWindow, focusedWindow, out directory);
    return 0;
});
Console.WriteLine(JsonSerializer.Serialize(new
{
    resolved,
    directory,
    focusedWindow = focusedWindow.ToInt64()
}));
return resolved && directory is not null && Directory.Exists(directory) ? 0 : 1;

static int InspectConflictDialog()
{
    using var dialog = new ConflictDialog(
    [
        @"D:\录像\闪耀色彩\水团120\01.青空 Jumping Heart.mkv",
        @"F:\video-compare\README with a deliberately long conflict name for DPI testing.md"
    ]);
    dialog.CreateControl();
    dialog.PerformLayout();

    var controls = Descendants(dialog).ToList();
    var labels = controls.OfType<System.Windows.Forms.Label>().ToList();
    var buttons = controls.OfType<System.Windows.Forms.Button>().ToList();
    var list = controls.OfType<System.Windows.Forms.ListBox>().Single();
    var result = new
    {
        dialog.Text,
        dialog.TopMost,
        dialog.StartPosition,
        dialog.ClientSize,
        labels = labels.Select(label => new { label.Text, label.Size, label.PreferredSize }),
        buttons = buttons.Select(button => new { button.Text, button.Size, button.MinimumSize }),
        conflictCount = list.Items.Count,
        horizontalScrollbar = list.HorizontalScrollbar
    };
    Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));

    return dialog.TopMost &&
           dialog.StartPosition == System.Windows.Forms.FormStartPosition.CenterParent &&
           dialog.ClientSize.Width >= 720 &&
           labels.All(label => label.Height >= label.PreferredHeight) &&
           buttons.All(button => button.Width >= button.MinimumSize.Width) &&
           list.HorizontalScrollbar ? 0 : 1;
}

static IEnumerable<System.Windows.Forms.Control> Descendants(System.Windows.Forms.Control root)
{
    foreach (System.Windows.Forms.Control child in root.Controls)
    {
        yield return child;
        foreach (var descendant in Descendants(child))
        {
            yield return descendant;
        }
    }
}

static int RunInSta(Func<int> action)
{
    var result = 1;
    Exception? error = null;
    var thread = new Thread(() =>
    {
        try
        {
            result = action();
        }
        catch (Exception exception)
        {
            error = exception;
        }
    });
    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();
    if (error is not null)
    {
        throw new InvalidOperationException("STA smoke test failed.", error);
    }

    return result;
}
