using System.Runtime.InteropServices;

namespace FastCopyPaste.Host;

[ComImport]
[Guid("6D5140C1-7436-11CE-8034-00AA006009FA")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IComServiceProvider
{
    [PreserveSig]
    int QueryService(
        ref Guid service,
        ref Guid interfaceId,
        [MarshalAs(UnmanagedType.Interface)] out object value);
}

[ComImport]
[Guid("000214E3-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellView
{
    [PreserveSig]
    int GetWindow(out nint window);

    [PreserveSig]
    int ContextSensitiveHelp([MarshalAs(UnmanagedType.Bool)] bool enterMode);
}

[ComImport]
[Guid("000214E2-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellBrowser
{
    // IOleWindow methods must be repeated here to preserve the native vtable layout.
    [PreserveSig]
    int GetWindow(out nint window);

    [PreserveSig]
    int ContextSensitiveHelp([MarshalAs(UnmanagedType.Bool)] bool enterMode);

    [PreserveSig]
    int InsertMenusSB(nint sharedMenu, nint menuWidths);

    [PreserveSig]
    int SetMenuSB(nint sharedMenu, nint oleMenu, nint activeWindow);

    [PreserveSig]
    int RemoveMenusSB(nint sharedMenu);

    [PreserveSig]
    int SetStatusTextSB([MarshalAs(UnmanagedType.LPWStr)] string? statusText);

    [PreserveSig]
    int EnableModelessSB([MarshalAs(UnmanagedType.Bool)] bool enable);

    [PreserveSig]
    int TranslateAcceleratorSB(nint message, ushort commandId);

    [PreserveSig]
    int BrowseObject(nint itemIdList, uint flags);

    [PreserveSig]
    int GetViewStateStream(uint mode, out nint stream);

    [PreserveSig]
    int GetControlWindow(uint id, out nint window);

    [PreserveSig]
    int SendControlMsg(
        uint id,
        uint message,
        nuint wParam,
        nint lParam,
        out nint result);

    [PreserveSig]
    int QueryActiveShellView([MarshalAs(UnmanagedType.Interface)] out IShellView view);
}
