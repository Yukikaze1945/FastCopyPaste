using System.Runtime.InteropServices;
using Microsoft.CSharp.RuntimeBinder;

namespace FastCopyPaste.Host;

internal sealed class ExplorerContextService
{
    private static readonly Guid TopLevelBrowserService =
        new("4C96BE40-915C-11CF-99D3-00AA004AE837");
    private static readonly Guid ShellBrowserInterface = typeof(IShellBrowser).GUID;

    public bool TryGetCurrentDirectory(
        nint foregroundWindow,
        nint focusedWindow,
        out string? directory)
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
            var candidateDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < count; index++)
            {
                object? window = null;
                object? document = null;
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

                    document = dynamicWindow.Document;
                    if (document is null)
                    {
                        continue;
                    }

                    dynamic dynamicDocument = document;
                    var path = Convert.ToString(dynamicDocument.Folder.Self.Path);
                    if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                    {
                        candidateDirectories.Add(path);
                        if (TryGetShellViewWindow(document, out var viewWindow) &&
                            IsDescendantOrSelf(viewWindow, focusedWindow))
                        {
                            directory = path;
                            return true;
                        }
                    }
                }
                catch (Exception ex) when (ex is COMException or InvalidCastException)
                {
                    // Ignore transient or non-Explorer Shell windows.
                }
                finally
                {
                    ReleaseComObject(document);
                    ReleaseComObject(window);
                }
            }

            // Older Explorer versions can expose only one entry or omit the view service.
            // A single unambiguous candidate remains safe; multiple tabs must never guess.
            if (candidateDirectories.Count == 1)
            {
                directory = candidateDirectories.Single();
                return true;
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

    private static bool TryGetShellViewWindow(object source, out nint viewWindow)
    {
        viewWindow = nint.Zero;
        if (source is not IComServiceProvider provider)
        {
            return false;
        }

        object? service = null;
        IShellView? view = null;
        try
        {
            var serviceId = TopLevelBrowserService;
            var interfaceId = ShellBrowserInterface;
            var result = provider.QueryService(ref serviceId, ref interfaceId, out service);
            if (result < 0 || service is not IShellBrowser browser)
            {
                return false;
            }

            result = browser.QueryActiveShellView(out view);
            return result >= 0 && view is not null && view.GetWindow(out viewWindow) >= 0 &&
                   viewWindow != nint.Zero;
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException)
        {
            return false;
        }
        finally
        {
            ReleaseComObject(view);
            ReleaseComObject(service);
        }
    }

    internal static bool IsDescendantOrSelf(nint ancestor, nint window)
    {
        var current = window;
        for (var depth = 0; current != nint.Zero && depth < 32; depth++)
        {
            if (current == ancestor)
            {
                return true;
            }

            current = NativeMethods.GetParent(current);
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
