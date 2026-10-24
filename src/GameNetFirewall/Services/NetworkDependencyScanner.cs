using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace GameNetFirewall.Services;

public sealed record NetworkDependency(string Name, string Path, string Kind);

public sealed class NetworkDependencyScanner
{
    // Heuristic only: users can always add an executable manually. VPNs often rely on
    // services, drivers, or virtual adapters that are not represented by a single EXE.
    private static readonly string[] NetworkToolNameHints =
    {
        "vpn", "wireguard", "openvpn", "proton", "nord", "mullvad", "expressvpn",
        "windscribe", "surfshark", "tailscale", "zerotier", "cloudflarewarp",
        "warp", "exitlag", "wtfast", "gearup", "mudfish", "mudrun", "mudflow",
        "privateinternetaccess", "pia", "ivpn", "softether", "v2ray", "clash",
        "sing-box", "nekoray", "outline", "zerotier", "hamachi", "radmin"
    };

    public IReadOnlyList<NetworkDependency> Scan()
    {
        var found = new Dictionary<string, NetworkDependency>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in GetSteamPaths())
        {
            if (File.Exists(path))
                found[path] = new NetworkDependency("Steam", path, "Steam client");
        }

        Process[] processes;
        try { processes = Process.GetProcesses(); }
        catch { processes = Array.Empty<Process>(); }

        foreach (var process in processes)
        {
            try
            {
                var name = process.ProcessName;
                if (!NetworkToolNameHints.Any(hint => name.Contains(hint, StringComparison.OrdinalIgnoreCase)))
                    continue;

                var path = process.MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    found[path] = new NetworkDependency(name, path, "Detected running network tool (heuristic)");
            }
            catch { /* Some system processes deny access or may exit during scanning. */ }
            finally { process.Dispose(); }
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
}
