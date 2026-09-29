using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace BongoCatAutoClaim.Core;

public sealed partial class GameDetector
{
    private const string AppId = "3419430";

    public IReadOnlyList<GameInstallation> Detect()
    {
        var results = new Dictionary<string, GameInstallation>(StringComparer.OrdinalIgnoreCase);
        foreach (var steamRoot in GetSteamRoots())
        {
            var steamApps = Path.Combine(steamRoot, "steamapps");
            var manifest = Path.Combine(steamApps, $"appmanifest_{AppId}.acf");
            if (!File.Exists(manifest))
                continue;

            var text = File.ReadAllText(manifest);
            var installDir = CaptureValue(text, "installdir");
            var buildId = CaptureValue(text, "buildid");
            if (string.IsNullOrWhiteSpace(installDir))
                continue;

            var root = Path.GetFullPath(Path.Combine(steamApps, "common", installDir));
            var installation = new GameInstallation(root, buildId);
            if (File.Exists(installation.AssemblyPath))
                results[root] = installation;
        }

        return results.Values.OrderBy(item => item.RootPath, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public GameInstallation FromSelectedFolder(string folder)
    {
        var root = Path.GetFullPath(folder.Trim());
        var installation = new GameInstallation(root, TryReadBuildId(root));
        if (!File.Exists(installation.AssemblyPath))
            throw new FileNotFoundException("Assembly-CSharp.dll was not found under BongoCat_Data\\Managed.", installation.AssemblyPath);
        return installation;
    }

    private static IEnumerable<string> GetSteamRoots()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var keyPath in new[]
                 {
                     @"HKEY_CURRENT_USER\Software\Valve\Steam",
                     @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam",
                     @"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam"
                 })
        {
            var value = Registry.GetValue(keyPath, "SteamPath", null) ?? Registry.GetValue(keyPath, "InstallPath", null);
            if (value is string path && Directory.Exists(path))
                roots.Add(Path.GetFullPath(path));
        }

        foreach (var root in roots.ToArray())
        {
            var libraryFile = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(libraryFile))
                continue;
            var text = File.ReadAllText(libraryFile);
            foreach (Match match in LibraryPathRegex().Matches(text))
            {
                var path = match.Groups[1].Value.Replace("\\\\", "\\");
                if (Directory.Exists(path))
                    roots.Add(Path.GetFullPath(path));
            }
        }

        return roots;
    }

    private static string? TryReadBuildId(string gameRoot)
    {
        var common = Directory.GetParent(gameRoot);
        var steamApps = common?.Parent;
        if (common?.Name.Equals("common", StringComparison.OrdinalIgnoreCase) != true || steamApps is null)
            return null;
        var manifest = Path.Combine(steamApps.FullName, $"appmanifest_{AppId}.acf");
        return File.Exists(manifest) ? CaptureValue(File.ReadAllText(manifest), "buildid") : null;
    }

    private static string? CaptureValue(string text, string key)
    {
        var match = Regex.Match(text, $"\"{Regex.Escape(key)}\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }

    [GeneratedRegex("\"path\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex LibraryPathRegex();
}
