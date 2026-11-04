using System.Text.RegularExpressions;
using Microsoft.Win32;
using GameWarmup.Models;

namespace GameWarmup.Services;

public static class SteamScanner
{
    public static List<GameEntry> FindInstalledGames()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var basePath in new[] {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            @"C:\Program Files (x86)", @"C:\Program Files" })
        {
            if (!string.IsNullOrWhiteSpace(basePath)) roots.Add(Path.Combine(basePath, "Steam"));
        }
        // Steam can be installed outside Program Files; try the current-user registry path.
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            var installed = key?.GetValue("SteamPath") as string;
            if (!string.IsNullOrWhiteSpace(installed) && Directory.Exists(installed))
                roots.Add(Path.GetFullPath(installed));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { }
        var locations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots)
        {
            var steamapps = Path.Combine(root, "steamapps");
            if (!Directory.Exists(steamapps)) continue;
            locations.Add(steamapps);
            var vdf = Path.Combine(steamapps, "libraryfolders.vdf");
            if (!File.Exists(vdf)) continue;
            try
            {
                var contents = File.ReadAllText(vdf);
                foreach (Match m in Regex.Matches(contents, "\"path\"\\s*\"(?<path>(?:\\\\.|[^\"])*)\"", RegexOptions.IgnoreCase))
                {
                    var folder = m.Groups["path"].Value.Replace("\\\\", "\\");
                    var apps = Path.Combine(folder, "steamapps");
                    if (Directory.Exists(apps)) locations.Add(apps);
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
        var found = new List<GameEntry>();
        foreach (var apps in locations)
        {
            string[] manifests;
            try { manifests = Directory.GetFiles(apps, "appmanifest_*.acf"); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { continue; }
            foreach (var manifest in manifests)
            {
                try
                {
                    var content = File.ReadAllText(manifest);
                    var dirname = GetField(content, "installdir");
                    var name = GetField(content, "name");
                    if (string.IsNullOrWhiteSpace(dirname) || string.IsNullOrWhiteSpace(name) ||
                        dirname.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || dirname is "." or "..") continue;
                    var path = Path.GetFullPath(Path.Combine(apps, "common", dirname));
                    var common = Path.GetFullPath(Path.Combine(apps, "common")) + Path.DirectorySeparatorChar;
                    if (!path.StartsWith(common, StringComparison.OrdinalIgnoreCase) || !Directory.Exists(path)) continue;
                    found.Add(new GameEntry { Name = name, Path = path });
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { }
            }
        }
        return found.GroupBy(g => g.Path, StringComparer.OrdinalIgnoreCase).Select(g => g.First())
            .OrderBy(g => g.Name).ToList();
    }
    private static string? GetField(string content, string field)
    {
        var match = Regex.Match(content, "\"" + Regex.Escape(field) + "\"\\s*\"(?<value>[^\"]*)\"", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["value"].Value : null;
    }
}
