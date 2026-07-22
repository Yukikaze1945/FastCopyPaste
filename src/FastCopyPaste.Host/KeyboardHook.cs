using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace FastCopyPaste.Host;

internal sealed class KeyboardHook : IDisposable
{
    private readonly NativeMethods.LowLevelKeyboardProc _callback;
    private readonly Func<bool> _tryIntercept;
    private readonly HashSet<int> _suppressedCaptureModifierKeys = [];
    private nint _hook;
    private HotkeyGesture _gesture;
    private int _handledVirtualKey;
    private int _capturedMainKey;
    private HotkeyModifiers _captureModifiers;
    private Action<HotkeyGesture>? _captureCompleted;

    public KeyboardHook(HotkeyGesture gesture, Func<bool> tryIntercept)
    {
        _gesture = gesture.Normalize();
        _tryIntercept = tryIntercept;
        _callback = HookCallback;
    }

    public bool Enabled => _hook != nint.Zero;
    public bool IsCapturing => _captureCompleted is not null;
    public HotkeyGesture Gesture => _gesture;

    public void UpdateGesture(HotkeyGesture gesture) => _gesture = gesture.Normalize();

    public void BeginCapture(Action<HotkeyGesture> captureCompleted)
    {
        ArgumentNullException.ThrowIfNull(captureCompleted);
        _captureModifiers = GetPressedModifiers();
        _captureCompleted = captureCompleted;
        _capturedMainKey = 0;
    }

    public void CancelCapture()
    {
        _captureCompleted = null;
        _captureModifiers = HotkeyModifiers.None;
        _capturedMainKey = 0;
    }

    public void Enable()
    {
        if (Enabled)
        {
            return;
        }

        _hook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WhKeyboardLl,
            _callback,
            NativeMethods.GetModuleHandle(null),
            0);
        if (_hook == nint.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "无法安装快捷键钩子。");
        }
    }

    public void Disable()
    {
        if (!Enabled)
        {
            return;
        }

        NativeMethods.UnhookWindowsHookEx(_hook);
        _hook = nint.Zero;
        _handledVirtualKey = 0;
        CancelCapture();
        _suppressedCaptureModifierKeys.Clear();
    }

    public static bool IsExplorerFileView(out nint foregroundWindow, out nint focusedWindow)
    {
        foregroundWindow = NativeMethods.GetForegroundWindow();
        focusedWindow = nint.Zero;
        if (foregroundWindow == nint.Zero)
        {
            return false;
        }

        var threadId = NativeMethods.GetWindowThreadProcessId(foregroundWindow, out var processId);
        try
        {
            using var process = Process.GetProcessById((int)processId);
            if (!string.Equals(process.ProcessName, "explorer", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }
        catch (ArgumentException)
        {
            return false;
        }

        var info = new NativeMethods.GuiThreadInfo
        {
            Size = (uint)Marshal.SizeOf<NativeMethods.GuiThreadInfo>()
        };
        if (!NativeMethods.GetGUIThreadInfo(threadId, ref info) || info.FocusWindow == nint.Zero)
        {
            return false;
        }

        focusedWindow = info.FocusWindow;

        var foundFileView = false;
        var current = focusedWindow;
        for (var depth = 0; current != nint.Zero && depth < 16; depth++)
        {
            var className = GetClassName(current);
            if (IsTextEntryClass(className))
            {
                return false;
            }

            if (string.Equals(className, "DirectUIHWND", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(className, "SysListView32", StringComparison.OrdinalIgnoreCase))
            {
                foundFileView = true;
            }

            current = NativeMethods.GetParent(current);
        }

        return foundFileView;
    }

    public static void ReplayGesture(HotkeyGesture gesture)
    {
        var inputs = CreateReplayInputs(gesture, IsKeyDown);
        NativeMethods.SendInput(
            (uint)inputs.Length,
            inputs,
            Marshal.SizeOf<NativeMethods.Input>());
    }

    internal static NativeMethods.Input[] CreateReplayInputs(
        HotkeyGesture gesture,
        Func<int, bool> isKeyDown)
    {
        gesture = gesture.Normalize();
        var inputs = new List<NativeMethods.Input>();
        var pressedByReplay = new List<int>();
        foreach (var (modifier, virtualKey) in ModifierInputs)
        {
            if (!gesture.Modifiers.HasFlag(modifier) || isKeyDown(virtualKey))
            {
                continue;
            }

            inputs.Add(CreateKeyInput(virtualKey, keyUp: false));
            pressedByReplay.Add(virtualKey);
        }

        inputs.Add(CreateKeyInput(gesture.VirtualKey, keyUp: false));
        inputs.Add(CreateKeyInput(gesture.VirtualKey, keyUp: true));
        for (var index = pressedByReplay.Count - 1; index >= 0; index--)
        {
            inputs.Add(CreateKeyInput(pressedByReplay[index], keyUp: true));
        }

        return inputs.ToArray();
    }

    private static NativeMethods.Input CreateKeyInput(int virtualKey, bool keyUp) =>
        new()
        {
            Type = NativeMethods.InputKeyboard,
            Union = new NativeMethods.InputUnion
            {
                Keyboard = new NativeMethods.KeyboardInput
                {
                    VirtualKey = checked((ushort)virtualKey),
                    Flags = (keyUp ? NativeMethods.KeyeventfKeyup : 0) |
                            (IsExtendedKey(virtualKey) ? NativeMethods.KeyeventfExtendedkey : 0),
                    ExtraInfo = NativeMethods.ReplayExtraInfo
                }
            }
        };

    private nint HookCallback(int code, nint wParam, nint lParam)
    {
        if (code >= 0)
        {
            var message = unchecked((int)(long)wParam);
            var data = Marshal.PtrToStructure<NativeMethods.KeyboardHookData>(lParam);
            var isOwnReplay =
                (data.Flags & NativeMethods.LlkhfInjected) != 0 &&
                data.ExtraInfo == NativeMethods.ReplayExtraInfo;
            if (!isOwnReplay)
            {
                var virtualKey = checked((int)data.VirtualKeyCode);
                var keyDown = message is NativeMethods.WmKeyDown or NativeMethods.WmSysKeyDown;
                var keyUp = message is NativeMethods.WmKeyUp or NativeMethods.WmSysKeyUp;
                if ((keyDown || keyUp) && HandleCapture(virtualKey, keyDown, keyUp))
                {
                    return 1;
                }

                if (keyUp && _handledVirtualKey == virtualKey)
                {
                    _handledVirtualKey = 0;
                    return 1;
                }

                var gesture = _gesture;
                if (keyDown && gesture.Matches(virtualKey, GetPressedModifiers()) &&
                    (_handledVirtualKey == virtualKey || _tryIntercept()))
                {
                    _handledVirtualKey = virtualKey;
                    return 1;
                }
            }
        }

        return NativeMethods.CallNextHookEx(_hook, code, wParam, lParam);
    }

    private bool HandleCapture(int virtualKey, bool keyDown, bool keyUp)
    {
        var modifier = GetModifier(virtualKey);
        if (modifier != HotkeyModifiers.None && _captureCompleted is not null)
        {
            if (keyDown)
            {
                _captureModifiers |= modifier;
                _suppressedCaptureModifierKeys.Add(virtualKey);
            }
            else if (keyUp)
            {
                _captureModifiers &= ~modifier;
                _suppressedCaptureModifierKeys.Remove(virtualKey);
            }

            return true;
        }

        if (keyUp && _suppressedCaptureModifierKeys.Remove(virtualKey))
        {
            return true;
        }

        if (keyUp && _capturedMainKey == virtualKey)
        {
            _capturedMainKey = 0;
            return true;
        }

        if (!keyDown || _captureCompleted is null || HotkeyGesture.IsModifierKey(virtualKey))
        {
            return false;
        }

        var completed = _captureCompleted;
        var gesture = new HotkeyGesture(virtualKey, _captureModifiers).Normalize();
        _captureCompleted = null;
        _captureModifiers = HotkeyModifiers.None;
        _capturedMainKey = virtualKey;
        completed(gesture);
        return true;
    }

    internal static HotkeyModifiers GetPressedModifiers()
        => GetPressedModifiers(IsKeyDown);

    internal static HotkeyModifiers GetPressedModifiers(Func<int, bool> isKeyDown)
    {
        var modifiers = HotkeyModifiers.None;
        if (isKeyDown(NativeMethods.VkControl)) modifiers |= HotkeyModifiers.Control;
        if (isKeyDown(NativeMethods.VkAlt)) modifiers |= HotkeyModifiers.Alt;
        if (isKeyDown(NativeMethods.VkShift)) modifiers |= HotkeyModifiers.Shift;
        if (isKeyDown(NativeMethods.VkLeftWindows) ||
            isKeyDown(NativeMethods.VkRightWindows))
        {
            modifiers |= HotkeyModifiers.Windows;
        }

        return modifiers;
    }

    private static bool IsKeyDown(int virtualKey) =>
        NativeMethods.GetAsyncKeyState(virtualKey) < 0;

    private static HotkeyModifiers GetModifier(int virtualKey) => virtualKey switch
    {
        NativeMethods.VkShift or NativeMethods.VkLeftShift or NativeMethods.VkRightShift =>
            HotkeyModifiers.Shift,
        NativeMethods.VkControl or NativeMethods.VkLeftControl or NativeMethods.VkRightControl =>
            HotkeyModifiers.Control,
        NativeMethods.VkAlt or NativeMethods.VkLeftAlt or NativeMethods.VkRightAlt =>
            HotkeyModifiers.Alt,
        NativeMethods.VkLeftWindows or NativeMethods.VkRightWindows => HotkeyModifiers.Windows,
        _ => HotkeyModifiers.None
    };

    private static bool IsExtendedKey(int virtualKey) => virtualKey is
        NativeMethods.VkLeftWindows or NativeMethods.VkRightWindows or
        NativeMethods.VkLeftControl or NativeMethods.VkRightControl or
        NativeMethods.VkLeftAlt or NativeMethods.VkRightAlt;

    private static readonly (HotkeyModifiers Modifier, int VirtualKey)[] ModifierInputs =
    [
        (HotkeyModifiers.Control, NativeMethods.VkControl),
        (HotkeyModifiers.Alt, NativeMethods.VkAlt),
        (HotkeyModifiers.Shift, NativeMethods.VkShift),
        (HotkeyModifiers.Windows, NativeMethods.VkLeftWindows)
    ];

    private static string GetClassName(nint window)
    {
        var buffer = new StringBuilder(256);
        return NativeMethods.GetClassName(window, buffer, buffer.Capacity) > 0
            ? buffer.ToString()
            : string.Empty;
    }

    private static bool IsTextEntryClass(string className) =>
        className.Contains("Edit", StringComparison.OrdinalIgnoreCase) ||
        className.Contains("RichEdit", StringComparison.OrdinalIgnoreCase) ||
        className.Contains("ComboBox", StringComparison.OrdinalIgnoreCase);

    public void Dispose() => Disable();
}
