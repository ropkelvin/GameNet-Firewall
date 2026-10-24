using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace GameNetFirewall.Services;

public sealed record NetworkDependency(string Name, string Path, string Kind);

public sealed class NetworkDependencyScanner
{
    public IReadOnlyList<NetworkDependency> Scan()
    {
        var found = new Dictionary<string, NetworkDependency>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in GetSteamPaths())
        {
            if (File.Exists(path))
                found[path] = new NetworkDependency("Steam", path, "Steam client");
        }

        foreach (var processName in new[] { "mudfish", "mudrun", "mudflow", "mudfish-launcher" })
        {
            foreach (var process in SafeGetProcesses(processName))
            {
                try
                {
                    var path = process.MainModule?.FileName;
                    if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                        found[path] = new NetworkDependency(process.ProcessName, path, "Running VPN process");
                }
                catch { /* Process may exit or deny access while scanning. */ }
                finally { process.Dispose(); }
            }
        }

        foreach (var path in GetMudfishInstallPaths())
        {
            if (File.Exists(path))
                found[path] = new NetworkDependency("Mudfish", path, "Installed VPN candidate");
        }

        return found.Values.OrderBy(x => x.Kind).ThenBy(x => x.Name).ToArray();
    }

    private static IEnumerable<string> GetSteamPaths()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in new[]
        {
            @"HKEY_CURRENT_USER\Software\Valve\Steam",
            @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam",
            @"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam"
        })
        {
            try
            {
                var value = Registry.GetValue(key, "SteamExe", null) as string
                    ?? Registry.GetValue(key, "InstallPath", null) as string;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    if (value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        paths.Add(value);
                    else
                        paths.Add(Path.Combine(value, "steam.exe"));
                }
            }
            catch { }
        }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrWhiteSpace(programFiles))
            paths.Add(Path.Combine(programFiles, "Steam", "steam.exe"));
        return paths;
    }

    private static IEnumerable<string> GetMudfishInstallPaths()
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        }.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var root in roots)
        {
            foreach (var candidate in new[]
            {
                Path.Combine(root, "Mudfish", "mudfish.exe"),
                Path.Combine(root, "Mudfish", "mudrun.exe"),
                Path.Combine(root, "Mudfish Cloud VPN", "mudfish.exe"),
                Path.Combine(root, "Mudfish Cloud VPN", "mudrun.exe")
            })
                yield return candidate;
        }
    }

    private static IEnumerable<Process> SafeGetProcesses(string name)
    {
        try { return Process.GetProcessesByName(name); }
        catch { return Array.Empty<Process>(); }
    }
}
