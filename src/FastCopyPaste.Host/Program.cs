using System.IO.Pipes;
using System.Text.Json;

namespace FastCopyPaste.Host;

internal static class Program
{
    internal const string MutexName = @"Local\FastCopyPaste.Host.v1";
    internal const string PipeName = "FastCopyPaste.Host.v1";

    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var requestedTarget = ParseTarget(args);

        if (requestedTarget is not null && PipeClient.TrySendTarget(requestedTarget, 700))
        {
            return 0;
        }

        using var mutex = new Mutex(true, MutexName, out var createdNew);
        if (!createdNew)
        {
            if (requestedTarget is not null)
            {
                for (var attempt = 0; attempt < 5; attempt++)
                {
                    Thread.Sleep(200);
                    if (PipeClient.TrySendTarget(requestedTarget, 700))
                    {
                        return 0;
                    }
                }
            }

            return 1;
        }

        Application.Run(new HostApplicationContext(requestedTarget));
        return 0;
    }

    private static string? ParseTarget(IReadOnlyList<string> args)
    {
        for (var index = 0; index < args.Count - 1; index++)
        {
            if (string.Equals(args[index], "--paste-target", StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }

        return null;
    }
}

internal static class PipeClient
{
    public static bool TrySendTarget(string target, int timeoutMilliseconds)
    {
        try
        {
            using var client = new NamedPipeClientStream(
                ".",
                Program.PipeName,
                PipeDirection.Out,
                PipeOptions.None);
            client.Connect(timeoutMilliseconds);
            using var writer = new StreamWriter(client) { AutoFlush = true };
            writer.WriteLine(JsonSerializer.Serialize(new PipeMessage(target)));
            return true;
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}

internal sealed record PipeMessage(string TargetDirectory);

