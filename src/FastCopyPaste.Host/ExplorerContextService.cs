using System.Runtime.InteropServices;
using Microsoft.CSharp.RuntimeBinder;

namespace FastCopyPaste.Host;

internal sealed class ExplorerContextService
{
    public bool TryGetCurrentDirectory(nint foregroundWindow, out string? directory)
    {
        directory = null;
        var shellType = Type.GetTypeFromProgID("Shell.Application");
        if (shellType is null)
        {
            return false;
        }

        object? shell = null;
        object? windows = null;
        try
        {
            shell = Activator.CreateInstance(shellType);
            if (shell is null)
            {
                return false;
            }

            dynamic dynamicShell = shell;
            windows = dynamicShell.Windows();
            if (windows is null)
            {
                return false;
            }

            dynamic dynamicWindows = windows;
            var count = (int)dynamicWindows.Count;
            var foregroundRoot = NativeMethods.GetAncestor(foregroundWindow, NativeMethods.GaRoot);
            for (var index = 0; index < count; index++)
            {
                object? window = null;
                try
                {
                    window = dynamicWindows.Item(index);
                    if (window is null)
                    {
                        continue;
                    }

                    dynamic dynamicWindow = window;
                    var windowHandle = new nint(Convert.ToInt64(dynamicWindow.HWND));
                    if (NativeMethods.GetAncestor(windowHandle, NativeMethods.GaRoot) != foregroundRoot)
                    {
                        continue;
                    }

                    var path = Convert.ToString(dynamicWindow.Document.Folder.Self.Path);
                    if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                    {
                        directory = path;
                        return true;
                    }
                }
                catch (Exception ex) when (ex is COMException or InvalidCastException)
                {
                    // Ignore transient or non-Explorer Shell windows.
                }
                finally
                {
                    ReleaseComObject(window);
                }
            }
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or RuntimeBinderException)
        {
            return false;
        }
        finally
        {
            ReleaseComObject(windows);
            ReleaseComObject(shell);
        }

        return false;
    }

    private static void ReleaseComObject(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            Marshal.FinalReleaseComObject(value);
        }
    }
}
