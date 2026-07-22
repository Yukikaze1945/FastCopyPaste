namespace FastCopyPaste.Host;

internal sealed class AppLog
{
    private readonly object _gate = new();

    public AppLog()
    {
        DirectoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FastCopyPaste",
            "Logs");
        Directory.CreateDirectory(DirectoryPath);
    }

    public string DirectoryPath { get; }

    public void Info(string message) => Write("INFO", message);

    public void Error(string message, Exception? exception = null) =>
        Write("ERROR", exception is null ? message : $"{message} | {exception}");

    private void Write(string level, string message)
    {
        var line = $"{DateTimeOffset.Now:O} [{level}] {message}{Environment.NewLine}";
        var path = Path.Combine(DirectoryPath, $"host-{DateTime.Now:yyyyMMdd}.log");
        lock (_gate)
        {
            File.AppendAllText(path, line);
        }
    }
}

