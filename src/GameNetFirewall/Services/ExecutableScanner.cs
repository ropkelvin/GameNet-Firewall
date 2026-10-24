using GameNetFirewall.Models;

namespace GameNetFirewall.Services;

public sealed class GameExecutable
{
    public required string Name { get; init; }
    public required string Path { get; init; }
    public override string ToString() => $"{Name} — {System.IO.Path.GetFileName(Path)}";
}

public sealed class ExecutableScanner
{
    private static readonly string[] ExcludedDirectoryNames =
    [
        "redist", "redistributables", "directx", "vcredist", "easyanticheat", "battleye",
        "support", "tools", "crashreporter", "crashreport", "prerequisites"
    ];

    public IReadOnlyList<GameExecutable> Scan(SteamGame game)
    {
        if (!Directory.Exists(game.InstallDirectory))
            return Array.Empty<GameExecutable>();

        var found = new List<GameExecutable>();
        var pending = new Stack<(string Directory, int Depth)>();
        pending.Push((game.InstallDirectory, 0));

        while (pending.Count > 0 && found.Count < 500)
        {
            var (directory, depth) = pending.Pop();
            try
            {
                foreach (var file in Directory.EnumerateFiles(directory, "*.exe", SearchOption.TopDirectoryOnly))
                {
                    var name = System.IO.Path.GetFileName(file);
                    if (name.Equals("unins000.exe", StringComparison.OrdinalIgnoreCase)
                        || name.Contains("setup", StringComparison.OrdinalIgnoreCase)
                        || name.Contains("crash", StringComparison.OrdinalIgnoreCase)
                        || name.Contains("update", StringComparison.OrdinalIgnoreCase))
                        continue;

                    found.Add(new GameExecutable { Name = name, Path = file });
                }

                if (depth >= 4)
                    continue;

                foreach (var child in Directory.EnumerateDirectories(directory))
                {
                    var name = System.IO.Path.GetFileName(child);
                    if (ExcludedDirectoryNames.Contains(name, StringComparer.OrdinalIgnoreCase)
                        || name.StartsWith(".", StringComparison.Ordinal))
                        continue;
                    pending.Push((child, depth + 1));
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
        }

        return found
            .OrderByDescending(item => IsLikelyGameExecutable(item.Name))
            .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static bool IsLikelyGameExecutable(string name)
    {
        var lower = name.ToLowerInvariant();
        return !lower.Contains("launcher") && !lower.Contains("helper") && !lower.Contains("crash");
    }
}
