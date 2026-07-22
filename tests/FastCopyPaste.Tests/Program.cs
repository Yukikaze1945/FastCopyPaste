using FastCopyPaste.Core;

var tests = new (string Name, Action Run)[]
{
    ("drop effect copy", () => Assert.Equal(PasteMode.Copy, ClipboardDropEffectParser.Parse(1))),
    ("drop effect move", () => Assert.Equal(PasteMode.Move, ClipboardDropEffectParser.Parse(2))),
    ("clipboard clears only unchanged successful move", TestClipboardSequencePolicy),
    ("planner detects conflicts", TestPlannerDetectsConflicts),
    ("planner rejects descendant target", TestPlannerRejectsDescendant),
    ("planner passes through same-directory copy", TestPlannerSameDirectory),
    ("planner deduplicates sources", TestPlannerDeduplicates),
    ("fastcopy arguments retain individual paths", TestFastCopyArguments)
};

var failures = new List<string>();
foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception ex)
    {
        failures.Add($"FAIL {test.Name}: {ex.Message}");
        Console.Error.WriteLine(failures[^1]);
    }
}

Console.WriteLine($"RESULT {tests.Length - failures.Count}/{tests.Length} passed");
return failures.Count == 0 ? 0 : 1;

static void TestClipboardSequencePolicy()
{
    Assert.True(ClipboardSequencePolicy.ShouldClear(PasteMode.Move, true, 10, 10));
    Assert.False(ClipboardSequencePolicy.ShouldClear(PasteMode.Copy, true, 10, 10));
    Assert.False(ClipboardSequencePolicy.ShouldClear(PasteMode.Move, false, 10, 10));
    Assert.False(ClipboardSequencePolicy.ShouldClear(PasteMode.Move, true, 10, 11));
}

static void TestPlannerDetectsConflicts()
{
    using var temp = TempTree.Create();
    var source = temp.File("source", "same.txt");
    var target = temp.Directory("target");
    temp.File("target", "same.txt");

    var plan = PastePlanner.Create([source], target, PasteMode.Copy);

    Assert.Equal(PastePlanStatus.Ready, plan.Status);
    Assert.Equal(1, plan.Conflicts.Count);
}

static void TestPlannerRejectsDescendant()
{
    using var temp = TempTree.Create();
    var source = temp.Directory("source");
    var child = temp.Directory("source", "child");

    var plan = PastePlanner.Create([source], child, PasteMode.Copy);

    Assert.Equal(PastePlanStatus.Invalid, plan.Status);
}

static void TestPlannerSameDirectory()
{
    using var temp = TempTree.Create();
    var directory = temp.Directory("same");
    var source = temp.File("same", "item.txt");

    var plan = PastePlanner.Create([source], directory, PasteMode.Copy);

    Assert.Equal(PastePlanStatus.PassThrough, plan.Status);
}

static void TestPlannerDeduplicates()
{
    using var temp = TempTree.Create();
    var source = temp.File("source", "item.txt");
    var target = temp.Directory("target");

    var plan = PastePlanner.Create([source, source], target, PasteMode.Copy);

    Assert.Equal(1, plan.Sources.Count);
}

static void TestFastCopyArguments()
{
    var command = FastCopyCommandFactory.Create(
        @"C:\Tools\FastCopy\FastCopy.exe",
        PasteMode.Move,
        [@"C:\A path\a.txt", @"C:\中文\b.txt"],
        @"D:\Target path");

    Assert.Equal("/cmd=move", command.Arguments[0]);
    Assert.True(command.Arguments.Contains(@"C:\A path\a.txt"));
    Assert.True(command.Arguments.Contains(@"C:\中文\b.txt"));
    Assert.Equal(@"/to=D:\Target path", command.Arguments[^1]);
}

file sealed class TempTree : IDisposable
{
    private readonly string _root;

    private TempTree(string root) => _root = root;

    public static TempTree Create()
    {
        var root = Path.Combine(Path.GetTempPath(), "FastCopyPaste.Tests", Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(root);
        return new TempTree(root);
    }

    public string Directory(params string[] parts)
    {
        var path = Path.Combine([_root, .. parts]);
        System.IO.Directory.CreateDirectory(path);
        return path;
    }

    public string File(params string[] parts)
    {
        var path = Path.Combine([_root, .. parts]);
        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllText(path, "test");
        return path;
    }

    public void Dispose()
    {
        if (System.IO.Directory.Exists(_root))
        {
            System.IO.Directory.Delete(_root, true);
        }
    }
}

file static class Assert
{
    public static void True(bool condition)
    {
        if (!condition) throw new InvalidOperationException("Expected true.");
    }

    public static void False(bool condition)
    {
        if (condition) throw new InvalidOperationException("Expected false.");
    }

    public static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"Expected {expected}, actual {actual}.");
        }
    }
}
