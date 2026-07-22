using System.Runtime.InteropServices;
using System.Text;

namespace FastCopyPaste.Host;

internal static class NativeMethods
{
    internal const int WhKeyboardLl = 13;
    internal const int WmKeyDown = 0x0100;
    internal const int WmKeyUp = 0x0101;
    internal const int WmSysKeyDown = 0x0104;
    internal const int WmSysKeyUp = 0x0105;
    internal const int VkShift = 0x10;
    internal const int VkControl = 0x11;
    internal const int VkAlt = 0x12;
    internal const int VkV = 0x56;
    internal const int VkLeftWindows = 0x5B;
    internal const int VkRightWindows = 0x5C;
    internal const int VkLeftShift = 0xA0;
    internal const int VkRightShift = 0xA1;
    internal const int VkLeftControl = 0xA2;
    internal const int VkRightControl = 0xA3;
    internal const int VkLeftAlt = 0xA4;
    internal const int VkRightAlt = 0xA5;
    internal const uint LlkhfInjected = 0x10;
    internal const nuint ReplayExtraInfo = 0x46435056;
    internal const uint InputKeyboard = 1;
    internal const uint KeyeventfKeyup = 0x0002;
    internal const uint GaRoot = 2;

    internal delegate nint LowLevelKeyboardProc(int code, nint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern nint SetWindowsHookEx(
        int hookId,
        LowLevelKeyboardProc callback,
        nint module,
        uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnhookWindowsHookEx(nint hook);

    [DllImport("user32.dll")]
    internal static extern nint CallNextHookEx(nint hook, int code, nint wParam, nint lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    internal static extern nint GetModuleHandle(string? moduleName);

    [DllImport("user32.dll")]
    internal static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll")]
    internal static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(nint window, out uint processId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetGUIThreadInfo(uint threadId, ref GuiThreadInfo info);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetClassName(nint window, StringBuilder className, int maxCount);

    [DllImport("user32.dll")]
    internal static extern nint GetParent(nint window);

    [DllImport("user32.dll")]
    internal static extern nint GetAncestor(nint window, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindow(nint window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetForegroundWindow(nint window);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint SendInput(uint inputCount, Input[] inputs, int inputSize);

    [DllImport("user32.dll")]
    internal static extern uint GetClipboardSequenceNumber();

    [StructLayout(LayoutKind.Sequential)]
    internal struct KeyboardHookData
    {
        internal uint VirtualKeyCode;
        internal uint ScanCode;
        internal uint Flags;
        internal uint Time;
        internal nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct GuiThreadInfo
    {
        internal uint Size;
        internal uint Flags;
        internal nint ActiveWindow;
        internal nint FocusWindow;
        internal nint CaptureWindow;
        internal nint MenuOwnerWindow;
        internal nint MoveSizeWindow;
        internal nint CaretWindow;
        internal Rect CaretRect;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Input
    {
        internal uint Type;
        internal InputUnion Union;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct InputUnion
    {
        [FieldOffset(0)]
        internal KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KeyboardInput
    {
        internal ushort VirtualKey;
        internal ushort ScanCode;
        internal uint Flags;
        internal uint Time;
        internal nuint ExtraInfo;
    }
}
