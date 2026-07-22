using System.Collections.Specialized;
using System.Runtime.InteropServices;
using FastCopyPaste.Core;

namespace FastCopyPaste.Host;

internal sealed class ClipboardService
{
    private const string PreferredDropEffect = "Preferred DropEffect";

    public bool HasFileDropList()
    {
        try
        {
            return Clipboard.ContainsFileDropList();
        }
        catch (ExternalException)
        {
            return false;
        }
    }

    public bool TryCapture(out ClipboardSnapshot? snapshot)
    {
        snapshot = null;
        try
        {
            if (!Clipboard.ContainsFileDropList())
            {
                return false;
            }

            StringCollection paths = Clipboard.GetFileDropList();
            if (paths.Count == 0)
            {
                return false;
            }

            var dropEffect = ReadDropEffect(Clipboard.GetDataObject());
            snapshot = new ClipboardSnapshot(
                paths.Cast<string>().ToArray(),
                ClipboardDropEffectParser.Parse(dropEffect),
                NativeMethods.GetClipboardSequenceNumber());
            return true;
        }
        catch (ExternalException)
        {
            return false;
        }
    }

    public uint GetSequenceNumber() => NativeMethods.GetClipboardSequenceNumber();

    public bool TryClear()
    {
        try
        {
            Clipboard.Clear();
            return true;
        }
        catch (ExternalException)
        {
            return false;
        }
    }

    private static uint ReadDropEffect(IDataObject? dataObject)
    {
        var data = dataObject?.GetData(PreferredDropEffect, false);
        return data switch
        {
            byte[] bytes when bytes.Length >= sizeof(uint) => BitConverter.ToUInt32(bytes, 0),
            MemoryStream stream => ReadStream(stream),
            _ => 1
        };
    }

    private static uint ReadStream(MemoryStream stream)
    {
        var position = stream.Position;
        try
        {
            stream.Position = 0;
            Span<byte> bytes = stackalloc byte[sizeof(uint)];
            return stream.Read(bytes) == bytes.Length ? BitConverter.ToUInt32(bytes) : 1;
        }
        finally
        {
            stream.Position = position;
        }
    }
}

