using System.Text.Json;
using FastCopyPaste.Host;

if (args.Length == 1 && string.Equals(args[0], "--hotkey-tests", StringComparison.OrdinalIgnoreCase))
{
    return RunHotkeyTests();
}

if (args.Length == 1 && string.Equals(args[0], "--hotkey-dialog", StringComparison.OrdinalIgnoreCase))
{
    return RunInSta(() =>
    {
        System.Windows.Forms.Application.SetHighDpiMode(System.Windows.Forms.HighDpiMode.PerMonitorV2);
        System.Windows.Forms.Application.EnableVisualStyles();
        System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
        using var hook = new KeyboardHook(HotkeyGesture.Default, () => false);
        using var dialog = new HotkeyDialog(hook, HotkeyGesture.Default);
        dialog.ShowInTaskbar = true;
        return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK ? 0 : 1;
    });
}

if (args.Length == 1 && string.Equals(args[0], "--inspect-hotkey-dialog", StringComparison.OrdinalIgnoreCase))
{
    return RunInSta(InspectHotkeyDialog);
}

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

static int InspectHotkeyDialog()
{
    using var hook = new KeyboardHook(HotkeyGesture.Default, () => false);
    using var dialog = new HotkeyDialog(hook, HotkeyGesture.Default);
    dialog.CreateControl();
    dialog.PerformLayout();

    var controls = Descendants(dialog).ToList();
    var labels = controls.OfType<System.Windows.Forms.Label>().ToList();
    var buttons = controls.OfType<System.Windows.Forms.Button>().ToList();
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        dialog.Text,
        dialog.TopMost,
        dialog.ClientSize,
        selectedGesture = dialog.SelectedGesture.ToDisplayString(),
        labels = labels.Select(label => new { label.Text, label.Size, label.PreferredSize }),
        buttons = buttons.Select(button => new { button.Text, button.Size, button.MinimumSize })
    }, new JsonSerializerOptions { WriteIndented = true }));

    return dialog.TopMost &&
           dialog.ClientSize.Width >= 620 &&
           labels.All(label => label.Height >= label.PreferredHeight) &&
           buttons.All(button => button.Width >= button.MinimumSize.Width) ? 0 : 1;
}

static int RunHotkeyTests()
{
    var failures = new List<string>();
    Check("default is Ctrl+V", () =>
    {
        AssertEqual(0x56, HotkeyGesture.Default.VirtualKey);
        AssertEqual(HotkeyModifiers.Control, HotkeyGesture.Default.Modifiers);
        AssertEqual("Ctrl+V", HotkeyGesture.Default.ToDisplayString());
    }, failures);
    Check("exact modifiers match", () =>
    {
        AssertTrue(HotkeyGesture.Default.Matches(0x56, HotkeyModifiers.Control));
        AssertFalse(HotkeyGesture.Default.Matches(
            0x56,
            HotkeyModifiers.Control | HotkeyModifiers.Shift));
        AssertFalse(HotkeyGesture.Default.Matches(0x43, HotkeyModifiers.Control));
    }, failures);
    Check("unmodified arbitrary key is allowed", () =>
    {
        var gesture = new HotkeyGesture(0x41, HotkeyModifiers.None);
        AssertTrue(gesture.IsUsable);
        AssertTrue(gesture.Matches(0x41, HotkeyModifiers.None));
        AssertEqual("A", gesture.ToDisplayString());
    }, failures);
    Check("Windows combinations are allowed", () =>
    {
        var gesture = new HotkeyGesture(
            0x44,
            HotkeyModifiers.Windows | HotkeyModifiers.Shift);
        AssertTrue(gesture.IsUsable);
        AssertEqual("Shift+Win+D", gesture.ToDisplayString());
    }, failures);
    Check("modifier-only gestures are rejected", () =>
    {
        AssertFalse(new HotkeyGesture(0x10, HotkeyModifiers.None).IsUsable);
        AssertFalse(new HotkeyGesture(0x11, HotkeyModifiers.Control).IsUsable);
        AssertFalse(new HotkeyGesture(0x12, HotkeyModifiers.Alt).IsUsable);
        AssertFalse(new HotkeyGesture(0x5B, HotkeyModifiers.Windows).IsUsable);
    }, failures);
    Check("legacy settings migrate to Ctrl+V", () =>
    {
        var settings = JsonSerializer.Deserialize<AppSettings>("{\"hookEnabled\":true}")!;
        settings.Normalize();
        AssertEqual(HotkeyGesture.Default, settings.Hotkey);
    }, failures);
    Check("invalid persisted gesture normalizes to default", () =>
    {
        var settings = JsonSerializer.Deserialize<AppSettings>(
            "{\"hotkey\":{\"virtualKey\":17,\"modifiers\":1}}")!;
        settings.Normalize();
        AssertEqual(HotkeyGesture.Default, settings.Hotkey);

        settings = JsonSerializer.Deserialize<AppSettings>(
            "{\"hotkey\":{\"virtualKey\":65,\"modifiers\":16}}")!;
        settings.Normalize();
        AssertEqual(HotkeyGesture.Default, settings.Hotkey);
    }, failures);
    Check("settings round trip arbitrary gesture", () =>
    {
        var original = new AppSettings
        {
            FastCopyPath = @"F:\\FastCopy\\FastCopy.exe",
            HookEnabled = true,
            Hotkey = new HotkeyGesture(0x70, HotkeyModifiers.None)
        };
        var serialized = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<AppSettings>(serialized)!;
        restored.Normalize();
        AssertEqual(original.Hotkey, restored.Hotkey);
    }, failures);
    Check("modifier state is derived exactly", () =>
    {
        var down = new HashSet<int>
        {
            NativeMethods.VkControl,
            NativeMethods.VkShift,
            NativeMethods.VkRightWindows
        };
        var modifiers = KeyboardHook.GetPressedModifiers(down.Contains);
        AssertEqual(
            HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Windows,
            modifiers);
    }, failures);
    Check("replay inputs preserve configured gesture", () =>
    {
        var gesture = new HotkeyGesture(
            0x41,
            HotkeyModifiers.Control | HotkeyModifiers.Shift);
        var inputs = KeyboardHook.CreateReplayInputs(gesture, _ => false);
        AssertEqual(6, inputs.Length);
        AssertEqual((ushort)NativeMethods.VkControl, inputs[0].Union.Keyboard.VirtualKey);
        AssertEqual((ushort)NativeMethods.VkShift, inputs[1].Union.Keyboard.VirtualKey);
        AssertEqual((ushort)0x41, inputs[2].Union.Keyboard.VirtualKey);
        AssertEqual((ushort)0x41, inputs[3].Union.Keyboard.VirtualKey);
        AssertTrue(inputs.All(input =>
            input.Union.Keyboard.ExtraInfo == NativeMethods.ReplayExtraInfo));
    }, failures);

    Console.WriteLine($"RESULT {10 - failures.Count}/10 passed");
    return failures.Count == 0 ? 0 : 1;
}

static void Check(string name, Action test, ICollection<string> failures)
{
    try
    {
        test();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception exception)
    {
        failures.Add(name);
        Console.Error.WriteLine($"FAIL {name}: {exception.Message}");
    }
}

static void AssertTrue(bool value)
{
    if (!value) throw new InvalidOperationException("Expected true.");
}

static void AssertFalse(bool value)
{
    if (value) throw new InvalidOperationException("Expected false.");
}

static void AssertEqual<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected {expected}, actual {actual}.");
    }
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
