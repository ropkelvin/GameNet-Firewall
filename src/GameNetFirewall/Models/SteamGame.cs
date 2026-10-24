namespace GameNetFirewall.Models;

public sealed record SteamGame(uint AppId, string Name, string InstallDirectory, string ManifestPath)
{
    public override string ToString() => Name;
}
