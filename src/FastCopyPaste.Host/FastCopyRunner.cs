using System.Diagnostics;
using FastCopyPaste.Core;

namespace FastCopyPaste.Host;

internal sealed class FastCopyRunner
{
    public async Task<int> RunAsync(FastCopyCommand command, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = command.ExecutablePath,
            WorkingDirectory = Path.GetDirectoryName(command.ExecutablePath) ?? Environment.CurrentDirectory,
            UseShellExecute = false,
            CreateNoWindow = false
        };
        foreach (var argument in command.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("FastCopy 进程未能启动。");
        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode;
    }
}

