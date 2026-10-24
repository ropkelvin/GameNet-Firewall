using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using GameNetFirewall.Models;
using GameNetFirewall.Services;

namespace GameNetFirewall;

public partial class MainWindow : Window
{
    private readonly SteamLibraryScanner _scanner = new();
    private readonly ExecutableScanner _executableScanner = new();
    private readonly FirewallService _firewall = new();
    private readonly NetworkDependencyScanner _dependencyScanner = new();
    private IReadOnlyList<NetworkDependency> _dependencies = Array.Empty<NetworkDependency>();
    private IReadOnlyList<SteamGame> _allGames = Array.Empty<SteamGame>();
    private IReadOnlyList<GameExecutable> _executables = Array.Empty<GameExecutable>();
    private string? _lastBackupPath;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => ScanGames();
    }

    private void Rescan_Click(object sender, RoutedEventArgs e) => ScanGames();

    private void ScanGames()
    {
        try
        {
            _allGames = _scanner.Scan();
            ApplySearchFilter();
            GameCountText.Text = _allGames.Count == 1
                ? "Found 1 installed Steam game"
                : $"Found {_allGames.Count} installed Steam games";
        }
        catch (Exception ex)
        {
            GameCountText.Text = "Steam scan failed";
            MessageBox.Show(this, ex.Message, "Steam library scan", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (IsLoaded)
            ApplySearchFilter();
    }

    private void ApplySearchFilter()
    {
        var query = SearchBox?.Text?.Trim() ?? string.Empty;
        var filtered = string.IsNullOrWhiteSpace(query)
            ? _allGames
            : _allGames.Where(game => game.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)).ToArray();

        GamesList.ItemsSource = filtered;
    }

    private void GamesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GamesList.SelectedItem is not SteamGame game)
        {
            SelectedGameName.Text = "Choose a game";
            SelectedGamePath.Text = "The install path will appear here.";
            ExecutableList.ItemsSource = null;
            _executables = Array.Empty<GameExecutable>();
            return;
        }

        SelectedGameName.Text = game.Name;
        SelectedGamePath.Text = $"Install folder:\n{game.InstallDirectory}\n\nApp ID: {game.AppId}\n\n"
            + (Directory.Exists(game.InstallDirectory)
                ? "Install folder exists. Scan to identify executable candidates."
                : "Install folder was not found. The game may have moved or the manifest may be stale.");

        _executables = Array.Empty<GameExecutable>();
        ExecutableList.ItemsSource = null;
    }

    private void ScanExecutables_Click(object sender, RoutedEventArgs e)
    {
        if (GamesList.SelectedItem is not SteamGame game)
        {
            MessageBox.Show(this, "Select a Steam game first.", "GameNet Firewall", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            _executables = _executableScanner.Scan(game);
            ExecutableList.ItemsSource = _executables;
            if (_executables.Count > 0)
                ExecutableList.SelectedIndex = 0;
            StatusText.Text = $"Found {_executables.Count} executable candidate(s). Verify the chosen file is the actual game, not a launcher or helper.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Executable scan", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetSelection(out var game, out var executable))
            return;

        var vpn = NetworkToolCheckBox.IsChecked == true
            ? "A VPN/network tool is selected, but its processes and tunnel traffic are not fully configured."
            : "No VPN profile selected.";
        var message = $"Game: {game.Name}\nExecutable: {executable.Path}\n\n"
            + "If confirmed, this build can create two app-specific ALLOW rules (inbound and outbound) for this executable.\n"
            + "It will not block other applications or change the global default policy.\n"
            + "Steam itself is not yet automatically allowlisted.\n"
            + vpn + "\n\nNo changes will be made by preview.";
        MessageBox.Show(this, message, "Firewall action preview", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Backup_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireAdministrator())
            return;
        try
        {
            _lastBackupPath = _firewall.CreateBackup();
            StatusText.Text = $"Firewall policy backup saved: {_lastBackupPath}";
            MessageBox.Show(this, $"Backup created successfully:\n{_lastBackupPath}", "Firewall backup", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ShowError("Firewall backup failed", ex);
        }
    }

    private void ApplyRules_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireAdministrator() || !TryGetSelection(out var game, out var executable))
            return;

        var warning = "This will back up your current firewall policy and add two app-specific ALLOW rules for the selected executable.\n\n"
            + "It will NOT block other apps, and it will NOT automatically allow Steam or your selected VPN/network tools.\n\n"
            + "Continue?";
        if (MessageBox.Show(this, warning, "Confirm firewall changes", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        try
        {
            _lastBackupPath = _firewall.CreateBackup();
            var stem = $"{FirewallService.RulePrefix}{game.AppId} - {Sanitize(System.IO.Path.GetFileNameWithoutExtension(executable.Path))}";
            _firewall.AddProgramAllowRule(stem + " - Outbound", executable.Path, "out");
            try
            {
                _firewall.AddProgramAllowRule(stem + " - Inbound", executable.Path, "in");
            }
            catch
            {
                _firewall.RemoveRule(stem + " - Outbound");
                throw;
            }

            StatusText.Text = $"Added game allow rules. Backup: {_lastBackupPath}";
            MessageBox.Show(this, $"Created inbound and outbound allow rules for:\n{executable.Path}\n\nBackup:\n{_lastBackupPath}\n\nReminder: these rules do not block other applications.", "Rules created", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ShowError("Could not apply firewall rules", ex);
        }
    }

    private void RemoveRules_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireAdministrator() || GamesList.SelectedItem is not SteamGame game)
        {
            if (GamesList.SelectedItem is not SteamGame)
                MessageBox.Show(this, "Select the game whose GameNet rules you want to remove.", "GameNet Firewall", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (MessageBox.Show(this, $"Remove GameNet Firewall rules for {game.Name}?\nOther firewall rules will not be touched.", "Confirm removal", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        var errors = new List<string>();
        foreach (var executable in _executableScanner.Scan(game))
        {
            var stem = $"{FirewallService.RulePrefix}{game.AppId} - {Sanitize(System.IO.Path.GetFileNameWithoutExtension(executable.Path))}";
            foreach (var suffix in new[] { " - Outbound", " - Inbound" })
            {
                try { _firewall.RemoveRule(stem + suffix); }
                catch (Exception ex) { errors.Add(ex.Message); }
            }
        }

        StatusText.Text = errors.Count == 0
            ? "Removal attempted for detected executable names. Existing files may have changed since rules were created."
            : $"Some rules could not be removed: {string.Join(" | ", errors)}";
        if (errors.Count > 0)
            MessageBox.Show(this, StatusText.Text, "Rule removal", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void ScanDependencies_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _dependencies = _dependencyScanner.Scan();
            DependenciesText.Text = _dependencies.Count == 0
                ? "No Steam or common VPN/network tool executable paths found by this scan."
                : string.Join(Environment.NewLine + Environment.NewLine,
                    _dependencies.Select(item => $"{item.Kind}: {item.Name}{Environment.NewLine}{item.Path}"));
            StatusText.Text = $"Found {_dependencies.Count} Steam / network-tool executable candidate(s). This scan does not change firewall rules.";
        }
        catch (Exception ex)
        {
            ShowError("Dependency scan failed", ex);
        }
    }

    private void AddNetworkTool_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose a VPN, proxy, tunnel client, or network tool executable",
            Filter = "Windows executable (*.exe)|*.exe|All files (*.*)|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != true)
            return;

        var path = Path.GetFullPath(dialog.FileName);
        var existing = _dependencies.FirstOrDefault(item => string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            StatusText.Text = "That executable is already in the dependency list.";
            return;
        }

        _dependencies = _dependencies
            .Append(new NetworkDependency(Path.GetFileNameWithoutExtension(path), path, "User-selected network tool"))
            .OrderBy(item => item.Kind).ThenBy(item => item.Name).ToArray();
        DependenciesText.Text = string.Join(Environment.NewLine + Environment.NewLine,
            _dependencies.Select(item => $"{item.Kind}: {item.Name}{Environment.NewLine}{item.Path}"));
        StatusText.Text = "Added a user-selected network tool. No firewall rules have been changed.";
    }

    private void AddDependencyRules_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireAdministrator())
            return;

        if (_dependencies.Count == 0)
        {
            MessageBox.Show(this, "Scan Steam / Network Tools first. No discovered dependency paths are available.", "GameNet Firewall", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var existing = _dependencies.Where(item => File.Exists(item.Path)).ToArray();
        if (existing.Length == 0)
        {
            MessageBox.Show(this, "No valid executable paths were found. Scan again while Steam or a VPN/network tool is installed/running.", "GameNet Firewall", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var preview = string.Join(Environment.NewLine, existing.Select(item => $"{item.Kind}: {item.Path}"));
        if (MessageBox.Show(this,
            "This will back up the current firewall policy and add OUTBOUND ALLOW rules for these discovered executables:\n\n"
            + preview + "\n\nThis does not enable strict mode or block other apps. Continue?",
            "Confirm dependency rules", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        try
        {
            _lastBackupPath = _firewall.CreateBackup();
            var added = new List<string>();
            foreach (var dependency in existing)
            {
                var name = $"{FirewallService.RulePrefix}Dependency - {Sanitize(Path.GetFileNameWithoutExtension(dependency.Path))} - Outbound";
                _firewall.AddProgramAllowRule(name, dependency.Path, "out");
                added.Add(name);
            }

            StatusText.Text = $"Added {added.Count} dependency outbound rule(s). Backup: {_lastBackupPath}";
            MessageBox.Show(this,
                $"Added outbound allow rules for {added.Count} discovered executable(s).\n\nBackup:\n{_lastBackupPath}\n\nImportant: this does not block other applications. If multiple paths share the same executable name, Windows Firewall may show rules with duplicate names.",
                "Dependency rules added", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ShowError("Could not add dependency rules", ex);
        }
    }

    private void RemoveDependencyRules_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireAdministrator())
            return;

        if (_dependencies.Count == 0)
        {
            MessageBox.Show(this, "Scan Steam / Network Tools first so the app knows which dependency rules to remove.", "GameNet Firewall", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (MessageBox.Show(this, "Remove GameNet Firewall outbound dependency rules matching the currently discovered executable names? Other rules will not be touched.",
            "Confirm dependency rule removal", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        var errors = new List<string>();
        foreach (var dependency in _dependencies)
        {
            var name = $"{FirewallService.RulePrefix}Dependency - {Sanitize(Path.GetFileNameWithoutExtension(dependency.Path))} - Outbound";
            try { _firewall.RemoveRule(name); }
            catch (Exception ex) { errors.Add($"{name}: {ex.Message}"); }
        }

        StatusText.Text = errors.Count == 0
            ? "Removal requested for dependency rules matching the last scan."
            : $"Some dependency rules could not be removed: {string.Join(" | ", errors)}";
        if (errors.Count > 0)
            MessageBox.Show(this, StatusText.Text, "Dependency rule removal", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void RestoreBackup_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireAdministrator())
            return;

        var dialog = new OpenFileDialog
        {
            Title = "Select a Windows Firewall .wfw backup to restore",
            Filter = "Windows Firewall backups (*.wfw)|*.wfw",
            InitialDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameNetFirewall", "Backups"),
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != true)
            return;

        var confirm = MessageBox.Show(this,
            "Restoring a .wfw file imports a FULL Windows Firewall policy and may replace current firewall settings and rules.\n\nOnly continue if this is the backup you intend to restore. This is not a per-game undo.\n\nRestore selected backup?",
            "Restore full firewall policy", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
            return;

        try
        {
            var safetyBackup = _firewall.CreateBackup();
            _firewall.RestoreBackup(dialog.FileName);
            StatusText.Text = $"Firewall policy restored. Pre-restore backup: {safetyBackup}";
            MessageBox.Show(this, $"Policy import completed. A backup of the policy before restore was saved at:\n{safetyBackup}",
                "Firewall policy restored", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ShowError("Firewall restore failed", ex);
        }
    }

    private void ShowProfiles_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireAdministrator())
            return;
        try
        {
            MessageBox.Show(this, _firewall.GetCurrentProfiles(), "Current Windows Firewall profiles", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ShowError("Could not read firewall profiles", ex);
        }
    }

    private bool TryGetSelection(out SteamGame game, out GameExecutable executable)
    {
        game = null!;
        executable = null!;
        if (GamesList.SelectedItem is not SteamGame selectedGame)
        {
            MessageBox.Show(this, "Select a Steam game first.", "GameNet Firewall", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }
        if (ExecutableList.SelectedItem is not GameExecutable selectedExecutable)
        {
            MessageBox.Show(this, "Scan the selected game folder and choose an executable first.", "GameNet Firewall", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }
        game = selectedGame;
        executable = selectedExecutable;
        return true;
    }

    private bool RequireAdministrator()
    {
        if (_firewall.IsAdministrator())
            return true;
        MessageBox.Show(this, "This action requires administrator rights. Close the app, then right-click GameNetFirewall.exe and choose 'Run as administrator'.", "Administrator rights required", MessageBoxButton.OK, MessageBoxImage.Warning);
        return false;
    }

    private static string Sanitize(string value)
    {
        var chars = value.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_').ToArray();
        return new string(chars);
    }

    private void ShowError(string title, Exception ex) =>
        MessageBox.Show(this, ex.Message, title, MessageBoxButton.OK, MessageBoxImage.Error);
}
