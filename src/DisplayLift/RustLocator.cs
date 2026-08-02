using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace DisplayLift;

internal static class RustLocator
{
    public static string FindExecutable()
    {
        foreach (var root in EnumerateSteamRoots())
        {
            var direct = Path.Combine(root, "steamapps", "common", "Rust", "RustClient.exe");
            if (File.Exists(direct)) return direct;

            var librariesFile = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(librariesFile)) continue;

            string content;
            try { content = File.ReadAllText(librariesFile); }
            catch { continue; }

            foreach (Match match in Regex.Matches(content, "\\\"path\\\"\\s+\\\"([^\\\"]+)\\\"", RegexOptions.IgnoreCase))
            {
                var library = match.Groups[1].Value.Replace("\\\\", "\\");
                var candidate = Path.Combine(library, "steamapps", "common", "Rust", "RustClient.exe");
                if (File.Exists(candidate)) return candidate;
            }
        }

        return string.Empty;
    }

    private static IEnumerable<string> EnumerateSteamRoots()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrWhiteSpace(programFilesX86)) roots.Add(Path.Combine(programFilesX86, "Steam"));

        foreach (var keyPath in new[]
        {
            @"HKEY_CURRENT_USER\Software\Valve\Steam",
            @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam",
            @"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam"
        })
        {
            foreach (var valueName in new[] { "SteamPath", "InstallPath" })
            {
                var value = Registry.GetValue(keyPath, valueName, null) as string;
                if (!string.IsNullOrWhiteSpace(value)) roots.Add(value.Replace('/', '\\'));
            }
        }

        return roots.Where(Directory.Exists);
    }
}
