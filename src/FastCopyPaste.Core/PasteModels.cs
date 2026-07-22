namespace FastCopyPaste.Core;

public enum PasteMode
{
    Copy,
    Move
}

public enum PastePlanStatus
{
    Ready,
    PassThrough,
    Invalid
}

public sealed record PastePlan(
    PastePlanStatus Status,
    PasteMode Mode,
    IReadOnlyList<string> Sources,
    string TargetDirectory,
    IReadOnlyList<string> Conflicts,
    string? Message)
{
    public static PastePlan Invalid(string message, PasteMode mode = PasteMode.Copy) =>
        new(PastePlanStatus.Invalid, mode, [], string.Empty, [], message);
}

public sealed record ClipboardSnapshot(
    IReadOnlyList<string> Sources,
    PasteMode Mode,
    uint SequenceNumber);

