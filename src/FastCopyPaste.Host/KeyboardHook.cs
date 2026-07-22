using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace FastCopyPaste.Host;

internal sealed class KeyboardHook : IDisposable
{
    private readonly NativeMethods.LowLevelKeyboardProc _callback;
    private readonly Func<bool> _tryIntercept;
    private nint _hook;
    private bool _handledVDown;

    public KeyboardHook(Func<bool> tryIntercept)
    {
        _tryIntercept = tryIntercept;
        _callback = HookCallback;
    }

    public bool Enabled => _hook != nint.Zero;

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
            throw new Win32Exception(Marshal.GetLastWin32Error(), "无法安装 Ctrl+V 键盘钩子。");
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
        _handledVDown = false;
    }

    public static bool IsExplorerFileView(out nint foregroundWindow)
    {
        foregroundWindow = NativeMethods.GetForegroundWindow();
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

        var foundFileView = false;
        var current = info.FocusWindow;
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

    public static void ReplayVKey()
    {
        var controlIsDown = NativeMethods.GetAsyncKeyState(NativeMethods.VkControl) < 0;
        var inputs = new List<NativeMethods.Input>();
        if (!controlIsDown)
        {
            inputs.Add(CreateKeyInput(NativeMethods.VkControl, keyUp: false));
        }

        inputs.Add(CreateKeyInput(NativeMethods.VkV, keyUp: false));
        inputs.Add(CreateKeyInput(NativeMethods.VkV, keyUp: true));
        if (!controlIsDown)
        {
            inputs.Add(CreateKeyInput(NativeMethods.VkControl, keyUp: true));
        }

        NativeMethods.SendInput(
            (uint)inputs.Count,
            inputs.ToArray(),
            Marshal.SizeOf<NativeMethods.Input>());
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
                    Flags = keyUp ? NativeMethods.KeyeventfKeyup : 0,
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
            if (data.VirtualKeyCode == NativeMethods.VkV && !isOwnReplay)
            {
                if (message is NativeMethods.WmKeyUp or NativeMethods.WmSysKeyUp)
                {
                    _handledVDown = false;
                }
                else if (message is NativeMethods.WmKeyDown or NativeMethods.WmSysKeyDown &&
                         NativeMethods.GetAsyncKeyState(NativeMethods.VkControl) < 0)
                {
                    if (_handledVDown || _tryIntercept())
                    {
                        _handledVDown = true;
                        return 1;
                    }
                }
            }
        }

        return NativeMethods.CallNextHookEx(_hook, code, wParam, lParam);
    }

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
