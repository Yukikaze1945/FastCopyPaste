using System.IO.Pipes;
using System.Text.Json;

namespace FastCopyPaste.Host;

internal sealed class PipeServer : IDisposable
{
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Action<string> _onTarget;
    private readonly AppLog _log;
    private Task? _serverTask;

    public PipeServer(Action<string> onTarget, AppLog log)
    {
        _onTarget = onTarget;
        _log = log;
    }

    public void Start() => _serverTask = RunAsync(_cancellation.Token);

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    Program.PipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(cancellationToken);
                using var reader = new StreamReader(server);
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line is null)
                {
                    continue;
                }

                var message = JsonSerializer.Deserialize<PipeMessage>(line);
                if (message is not null && !string.IsNullOrWhiteSpace(message.TargetDirectory))
                {
                    _onTarget(message.TargetDirectory);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                _log.Error("Named pipe listener error.", ex);
                await Task.Delay(250, cancellationToken);
            }
        }
    }

    public void Dispose()
    {
        _cancellation.Cancel();
        try
        {
            _serverTask?.Wait(TimeSpan.FromSeconds(1));
        }
        catch (AggregateException)
        {
            // Cancellation is expected during application shutdown.
        }
        _cancellation.Dispose();
    }
}

