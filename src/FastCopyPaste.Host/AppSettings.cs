using System.Text.Json;
using System.Text.Json.Serialization;

namespace FastCopyPaste.Host;

internal sealed class AppSettings
{
    [JsonPropertyName("fastCopyPath")]
    public string FastCopyPath { get; set; } = string.Empty;

    [JsonPropertyName("hookEnabled")]
    public bool HookEnabled { get; set; } = true;
}

internal sealed class SettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };
    private readonly string _settingsPath;

    public SettingsStore()
    {
        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FastCopyPaste");
        Directory.CreateDirectory(dataDirectory);
        _settingsPath = Path.Combine(dataDirectory, "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_settingsPath))
                    ?? new AppSettings();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // A valid default is safer than preventing the resident process from starting.
        }

        var settings = new AppSettings();
        Save(settings);
        return settings;
    }

    public void Save(AppSettings settings)
    {
        var temporaryPath = _settingsPath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, SerializerOptions));
        File.Move(temporaryPath, _settingsPath, true);
    }
}
