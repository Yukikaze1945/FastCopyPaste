namespace FastCopyPaste.Core;

public sealed record FastCopyCommand(string ExecutablePath, IReadOnlyList<string> Arguments);

public static class FastCopyCommandFactory
{
    public static FastCopyCommand Create(
        string executablePath,
        PasteMode mode,
        IEnumerable<string> sources,
        string targetDirectory)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new ArgumentException("FastCopy 路径不能为空。", nameof(executablePath));
        }

        var arguments = new List<string>
        {
            mode == PasteMode.Move ? "/cmd=move" : "/cmd=diff",
            "/auto_close",
            "/open_window",
            "/estimate"
        };

        arguments.AddRange(sources);
        arguments.Add($"/to={targetDirectory}");
        return new FastCopyCommand(executablePath, arguments);
    }
}

public static class ClipboardSequencePolicy
{
    public static bool ShouldClear(
        PasteMode mode,
        bool succeeded,
        uint capturedSequence,
        uint currentSequence) =>
        mode == PasteMode.Move && succeeded && capturedSequence == currentSequence;
}

public static class ClipboardDropEffectParser
{
    private const uint DropEffectMove = 2;

    public static PasteMode Parse(uint dropEffect) =>
        (dropEffect & DropEffectMove) != 0 ? PasteMode.Move : PasteMode.Copy;
}

