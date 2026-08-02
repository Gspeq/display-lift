using System.Text.Json;
using System.Text.Json.Serialization;

namespace DisplayLift;

internal sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public SettingsStore()
    {
        DirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DisplayLift");
        FilePath = Path.Combine(DirectoryPath, "settings-v9.json");
    }

    public string DirectoryPath { get; }
    public string FilePath { get; }

    public AppSettings Load()
    {
        AppSettings settings;
        try
        {
            var sourcePath = File.Exists(FilePath)
                ? FilePath
                : Path.Combine(DirectoryPath, "settings-v8.json");
            settings = File.Exists(sourcePath)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(sourcePath), JsonOptions) ?? new AppSettings()
                : new AppSettings();
        }
        catch
        {
            BackUpDamagedFile();
            settings = new AppSettings();
        }

        if (string.IsNullOrWhiteSpace(settings.RustExecutablePath))
        {
            settings.RustExecutablePath = TryMigrateRustPath() ?? RustLocator.FindExecutable();
        }

        settings.StartWithWindows = StartupManager.IsEnabled();
        settings.Validate();
        Save(settings);
        return settings;
    }

    public void Save(AppSettings settings)
    {
        settings.Validate();
        Directory.CreateDirectory(DirectoryPath);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, JsonOptions));
        File.Move(temp, FilePath, overwrite: true);
    }

    private string? TryMigrateRustPath()
    {
        var oldPath = Path.Combine(DirectoryPath, "profiles.json");
        if (!File.Exists(oldPath)) return null;

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(oldPath));
            if (!document.RootElement.TryGetProperty("Profiles", out var profiles) || profiles.ValueKind != JsonValueKind.Array)
                return null;

            foreach (var profile in profiles.EnumerateArray())
            {
                if (!profile.TryGetProperty("ProcessName", out var processName) ||
                    !string.Equals(processName.GetString(), "RustClient", StringComparison.OrdinalIgnoreCase)) continue;
                if (profile.TryGetProperty("ExecutablePath", out var executablePath))
                {
                    var value = executablePath.GetString();
                    if (!string.IsNullOrWhiteSpace(value)) return value;
                }
            }
        }
        catch
        {
            // Migration is best effort only.
        }

        return null;
    }

    private void BackUpDamagedFile()
    {
        try
        {
            if (!File.Exists(FilePath)) return;
            Directory.CreateDirectory(DirectoryPath);
            File.Copy(FilePath, Path.Combine(DirectoryPath, $"settings-v9-damaged-{DateTime.Now:yyyyMMdd-HHmmss}.json"));
        }
        catch
        {
            // Best effort only.
        }
    }
}
