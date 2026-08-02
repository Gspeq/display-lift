using Microsoft.Win32;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace DisplayLift;

internal sealed class ProfileStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public ProfileStore()
    {
        DirectoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DisplayLift");
        FilePath = Path.Combine(DirectoryPath, "profiles.json");
    }

    public string DirectoryPath { get; }
    public string FilePath { get; }

    public AppConfiguration Load()
    {
        AppConfiguration configuration;
        try
        {
            if (!File.Exists(FilePath))
            {
                configuration = AppConfiguration.CreateDefault(FindRustExecutable());
                Save(configuration);
                return configuration;
            }

            var json = File.ReadAllText(FilePath);
            configuration = JsonSerializer.Deserialize<AppConfiguration>(json, JsonOptions)
                ?? AppConfiguration.CreateDefault(FindRustExecutable());
        }
        catch
        {
            TryBackUpDamagedFile();
            configuration = AppConfiguration.CreateDefault(FindRustExecutable());
        }

        configuration.Version = 7;
        configuration.PollIntervalMilliseconds = Math.Clamp(configuration.PollIntervalMilliseconds, 150, 3000);
        configuration.Profiles ??= [];
        foreach (var profile in configuration.Profiles)
        {
            profile.Validate();
        }

        var rustProfile = configuration.Profiles.FirstOrDefault(profile =>
            string.Equals(profile.ProcessName, "RustClient", StringComparison.OrdinalIgnoreCase));
        if (rustProfile is null)
        {
            rustProfile = DisplayProfile.CreateRust(FindRustExecutable());
            configuration.Profiles.Insert(0, rustProfile);
        }
        else if (string.IsNullOrWhiteSpace(rustProfile.ExecutablePath))
        {
            rustProfile.ExecutablePath = FindRustExecutable();
        }

        if (configuration.LastSelectedProfileId is null ||
            configuration.Profiles.All(profile => profile.Id != configuration.LastSelectedProfileId))
        {
            configuration.LastSelectedProfileId = rustProfile.Id;
        }

        return configuration;
    }

    public void Save(AppConfiguration configuration)
    {
        Directory.CreateDirectory(DirectoryPath);
        configuration.Version = 7;
        foreach (var profile in configuration.Profiles)
        {
            profile.Validate();
        }

        var tempPath = FilePath + ".tmp";
        var json = JsonSerializer.Serialize(configuration, JsonOptions);
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, FilePath, overwrite: true);
    }

    public void ExportProfile(DisplayProfile profile, string destinationPath)
    {
        var json = JsonSerializer.Serialize(profile, JsonOptions);
        File.WriteAllText(destinationPath, json);
    }

    public DisplayProfile ImportProfile(string sourcePath)
    {
        var json = File.ReadAllText(sourcePath);
        var profile = JsonSerializer.Deserialize<DisplayProfile>(json, JsonOptions)
            ?? throw new InvalidDataException("The selected file does not contain a DisplayLift profile.");
        profile.Id = Guid.NewGuid();
        profile.Name = $"{profile.Name} imported";
        profile.Validate();
        return profile;
    }

    public static string FindRustExecutable()
    {
        foreach (var root in EnumerateSteamRoots())
        {
            var direct = Path.Combine(root, "steamapps", "common", "Rust", "RustClient.exe");
            if (File.Exists(direct))
            {
                return direct;
            }

            var libraryFile = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(libraryFile))
            {
                continue;
            }

            string contents;
            try
            {
                contents = File.ReadAllText(libraryFile);
            }
            catch
            {
                continue;
            }

            foreach (Match match in Regex.Matches(contents, "\\\"path\\\"\\s+\\\"([^\\\"]+)\\\"", RegexOptions.IgnoreCase))
            {
                var libraryRoot = match.Groups[1].Value.Replace("\\\\", "\\");
                var candidate = Path.Combine(libraryRoot, "steamapps", "common", "Rust", "RustClient.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return string.Empty;
    }

    private void TryBackUpDamagedFile()
    {
        try
        {
            if (!File.Exists(FilePath)) return;
            Directory.CreateDirectory(DirectoryPath);
            var backup = Path.Combine(DirectoryPath, $"profiles-damaged-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            File.Copy(FilePath, backup, overwrite: false);
        }
        catch
        {
            // Best effort only.
        }
    }

    private static IEnumerable<string> EnumerateSteamRoots()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrWhiteSpace(programFilesX86))
        {
            roots.Add(Path.Combine(programFilesX86, "Steam"));
        }

        foreach (var key in new[]
        {
            @"HKEY_CURRENT_USER\Software\Valve\Steam",
            @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam",
            @"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam"
        })
        {
            foreach (var valueName in new[] { "SteamPath", "InstallPath" })
            {
                var value = Registry.GetValue(key, valueName, null) as string;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    roots.Add(value.Replace('/', '\\'));
                }
            }
        }

        return roots.Where(Directory.Exists);
    }
}
