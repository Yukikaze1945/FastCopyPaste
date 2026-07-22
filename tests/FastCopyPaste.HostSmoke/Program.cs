using System.Text.Json;
using FastCopyPaste.Host;

if (args.Length != 1 || !long.TryParse(args[0], out var handleValue))
{
    Console.Error.WriteLine("Usage: FastCopyPaste.HostSmoke <explorer-hwnd>");
    return 2;
}

var service = new ExplorerContextService();
var resolved = service.TryGetCurrentDirectory(new nint(handleValue), out var directory);
Console.WriteLine(JsonSerializer.Serialize(new { resolved, directory }));
return resolved && directory is not null && Directory.Exists(directory) ? 0 : 1;
