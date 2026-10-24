using System.Windows;
using System.Windows.Controls;
using GameNetFirewall.Models;
using GameNetFirewall.Services;

namespace GameNetFirewall;

public partial class MainWindow : Window
{
    private readonly SteamLibraryScanner _scanner = new();
    private IReadOnlyList<SteamGame> _allGames = Array.Empty<SteamGame>();

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
            return;
        }

        SelectedGameName.Text = game.Name;
        SelectedGamePath.Text = $"Install folder:\n{game.InstallDirectory}\n\nApp ID: {game.AppId}\n\n"
            + (Directory.Exists(game.InstallDirectory)
                ? "Install folder exists. Executable detection is a future milestone."
                : "Install folder was not found. The game may have moved or the manifest may be stale.");
    }
}
