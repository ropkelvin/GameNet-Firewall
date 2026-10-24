using System.Text.RegularExpressions;
using Microsoft.Win32;
using GameNetFirewall.Models;

namespace GameNetFirewall.Services;

/// <summary>
/// Discovers Steam libraries and installed games from local Steam app manifests.
/// Reads local metadata only and does not modify firewall settings.
/// </summary>
public sealed class SteamLibraryScanner
{
    private static readonly Regex LibraryPathRegex =
        new("\"path\"\\s*\"([^\"]+)\"", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public IReadOnlyList<SteamGame> Scan()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var steamRoot = FindSteamRoot();
        if (!string.IsNullOrWhiteSpace(steamRoot))
            roots.Add(steamRoot);

        // Expand the known Steam libraries. libraryfolders.vdf contains paths
        // for the primary library and additional Steam libraries.
        foreach (var root in roots.ToArray())
        {
            var libraryFile = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(libraryFile))
                continue;

            try
            {
                var text = File.ReadAllText(libraryFile);
                foreach (Match match in LibraryPathRegex.Matches(text))
                {
                    var path = match.Groups[1].Value.Replace(@"\\", @"\");
                    if (Directory.Exists(path))
                        roots.Add(path);
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        var games = new Dictionary<uint, SteamGame>();
        foreach (var root in roots)
        {
            var appsDirectory = Path.Combine(root, "steamapps");
            if (!Directory.Exists(appsDirectory))
                continue;

            IEnumerable<string> manifests;
            try { manifests = Directory.EnumerateFiles(appsDirectory, "appmanifest_*.acf", SearchOption.TopDirectoryOnly).ToArray(); }
            catch (IOException) { continue; }
            catch (UnauthorizedAccessException) { continue; }

            foreach (var manifest in manifests)
            {
                try
                {
                    var content = File.ReadAllText(manifest);
                    var appIdMatch = Regex.Match(content, "\"appid\"\\s*\"(\\d+)\"");
                    var nameMatch = Regex.Match(content, "\"name\"\\s*\"([^\"]+)\"");
                    var directoryMatch = Regex.Match(content, "\"installdir\"\\s*\"([^\"]+)\"");

                    if (!appIdMatch.Success || !uint.TryParse(appIdMatch.Groups[1].Value, out var appId))
                        continue;

                    var name = nameMatch.Success ? nameMatch.Groups[1].Value : $"Steam App {appId}";
                    var installDirectory = directoryMatch.Success
                        ? Path.Combine(appsDirectory, "common", directoryMatch.Groups[1].Value)
                        : Path.Combine(appsDirectory, "common");

                    games[appId] = new SteamGame(appId, name, installDirectory, manifest);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        return games.Values.OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    private static string? FindSteamRoot()
    {
        var candidates = new List<string>();
        foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            foreach (var keyPath in new[]
            {
                @"Software\Valve\Steam",
                @"Software\WOW6432Node\Valve\Steam"
            })
            {
                try
                {
                    using var key = hive.OpenSubKey(keyPath);
                    if (key?.GetValue("SteamPath") is string steamPath && !string.IsNullOrWhiteSpace(steamPath))
                        candidates.Add(steamPath.Replace('/', Path.DirectorySeparatorChar));
                    if (key?.GetValue("InstallPath") is string installPath && !string.IsNullOrWhiteSpace(installPath))
                        candidates.Add(installPath);
                }
                catch (UnauthorizedAccessException) { }
            }
        }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrWhiteSpace(programFiles))
            candidates.Add(Path.Combine(programFiles, "Steam"));

        return candidates.FirstOrDefault(path => Directory.Exists(Path.Combine(path, "steamapps")));
    }
}
