using System.Diagnostics;
using System.Security.Principal;

namespace GameNetFirewall.Services;

public sealed class FirewallService
{
    public const string RulePrefix = "GameNetFirewall - ";
    private static readonly string BackupDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GameNetFirewall", "Backups");

    public bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    public string CreateBackup()
    {
        Directory.CreateDirectory(BackupDirectory);
        var path = Path.Combine(BackupDirectory, $"firewall-{DateTime.Now:yyyyMMdd-HHmmss}.wfw");
        RunNetsh($"advfirewall export \"{path}\"");
        if (!File.Exists(path) || new FileInfo(path).Length == 0)
            throw new InvalidOperationException("Windows Firewall backup was not created.");
        return path;
    }

    public string GetCurrentProfiles()
    {
        var result = RunNetsh("advfirewall show allprofiles");
        return result;
    }

    public void AddProgramAllowRule(string ruleName, string executablePath, string direction)
    {
        if (!File.Exists(executablePath))
            throw new FileNotFoundException("Executable does not exist.", executablePath);
        if (direction is not ("in" or "out"))
            throw new ArgumentException("Direction must be 'in' or 'out'.", nameof(direction));

        RunNetsh($"advfirewall firewall add rule name=\"{Escape(ruleName)}\" dir={direction} action=allow program=\"{Escape(executablePath)}\" enable=yes profile=any");
    }

    public void RemoveRule(string ruleName)
    {
        RunNetsh($"advfirewall firewall delete rule name=\"{Escape(ruleName)}\"");
    }

    public void RestoreBackup(string backupPath)
    {
        if (!File.Exists(backupPath) || !backupPath.EndsWith(".wfw", StringComparison.OrdinalIgnoreCase))
            throw new FileNotFoundException("Choose a valid GameNet Firewall .wfw backup.", backupPath);
        RunNetsh($"advfirewall import \"{Escape(backupPath)}\"");
    }

    private static string Escape(string value) => value.Replace("\"", "\\\"");

    private static string RunNetsh(string arguments)
    {
        var start = new ProcessStartInfo("netsh.exe", arguments)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start netsh.exe.");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Windows Firewall command failed ({process.ExitCode}).\n{stderr}\n{stdout}");
        return stdout;
    }
}
