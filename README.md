# GameNet Firewall

A Windows 11 desktop utility for inspecting Steam game executables and creating named Windows Defender Firewall rules.

> **Important status:** This version can scan installed Steam games, find executable candidates, export a Windows Firewall policy backup, inspect firewall profiles, and create/remove app-specific allow rules. **It does not implement strict network isolation or block all other applications.** Do not treat it as a complete gaming firewall yet.

## Current features

- Discover Steam installations, library folders and installed games.
- Search installed games and view their install paths.
- Scan a selected game folder for executable candidates (up to four directory levels).
- Preview the proposed action before applying it.
- Export a .wfw backup of the current Windows Firewall policy.
- Inspect current firewall profile settings.
- Add inbound/outbound allow rules for a selected executable.
- Remove GameNet-named rules for executable candidates in the selected game's current folder.
- Build a self-contained Windows x64 executable through GitHub Actions.

## Important limitations

- **Allow rules are not an allowlist.** On the usual Windows configuration, adding an allow rule for one game does not block other programs.
- This build intentionally does not change the global outbound default policy. Strict outbound blocking can disrupt DNS, DHCP, Steam authentication and updates, Mudfish/VPN tunnels, voice chat, anti-cheat, Windows services, remote access and unrelated apps.
- Steam's own executables, game launchers, anti-cheat services, helper processes, DNS/DHCP and Mudfish are not automatically configured.
- Executable scanning is heuristic. Confirm the selected path is the actual game executable.
- Rule removal finds candidate executables currently on disk. If a game executable was moved or deleted, a rule may need to be removed manually in Windows Defender Firewall with Advanced Security.
- The app does not elevate itself. Close it and choose Run as administrator when a firewall action requires elevation.
- The exported .wfw backup is stored under %LOCALAPPDATA%\GameNetFirewall\Backups. Importing a full firewall backup can replace policy settings; do this only if you intend to restore the whole saved policy.

## Safe usage

1. Select a game and click Scan Game Folder.
2. Confirm the selected executable path.
3. Click Preview Actions before making changes.
4. Run the app as administrator and click Back Up Current Firewall Policy.
5. Use Add Game Allow Rules only if you understand that it adds permissions for that executable but does not block other apps.
6. Use Remove GameNet Rules to remove the app's named rules for the selected game's detected executable candidates.

Keep a separate recovery path available before changing Windows networking settings.

## Build

On Windows with the .NET 8 SDK:

```powershell
dotnet restore .\src\GameNetFirewall\GameNetFirewall.csproj
dotnet build .\src\GameNetFirewall\GameNetFirewall.csproj -c Release
dotnet publish .\src\GameNetFirewall\GameNetFirewall.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o .\artifacts\publish
```

The GitHub Actions workflow uploads the self-contained executable as the GameNetFirewall-win-x64 artifact.

## Roadmap

- [x] Steam installation/library discovery
- [x] Installed-game list and search
- [x] Candidate executable scanner
- [x] Firewall policy export backup and profile inspection
- [x] Preview and named app-specific allow rules
- [x] Remove app-owned rules
- [ ] Steam process and launcher discovery
- [ ] Mudfish service/process and tunnel compatibility inspection
- [ ] Strict outbound allowlisting with explicit safeguards, recovery mode, and testing
- [ ] Automated tests and signed release packaging

## Technology

- C# / .NET 8 / WPF
- Windows netsh advfirewall interface
- GitHub Actions

Not affiliated with Valve, Microsoft, Mudfish, or any game publisher.
