# GameNet Firewall

A Windows 11 desktop utility for inspecting Steam games and preparing named Windows Defender Firewall rules.

> **Development status:** This app scans Steam libraries, finds game executable candidates, discovers likely Steam/Mudfish executable paths, backs up the Windows Firewall policy, inspects profiles, and can create/remove app-specific allow rules. **It does not yet enforce a strict allowlist or block all other applications.**

## Current features

- Discover Steam installations, library folders and installed games.
- Search games and view their install paths.
- Scan a selected game folder for executable candidates (up to four directory levels).
- Discover Steam client paths and likely running/installed Mudfish executable paths.
- Preview the selected game's proposed rules.
- Export a .wfw backup of the current Windows Firewall policy.
- Inspect current firewall profile settings.
- Add inbound/outbound allow rules for a selected executable.
- Remove GameNet-named rules for detected executables in the selected game's current folder.
- Restore a selected full .wfw firewall backup after an explicit warning and an automatic pre-restore backup.
- Build a self-contained Windows x64 executable through GitHub Actions.

## Important limitations

- **Allow rules are not an allowlist.** On a typical Windows configuration, adding an allow rule for one game does not block other programs.
- This build intentionally does not change the global outbound default policy. Strict outbound blocking can disrupt DNS, DHCP, Steam authentication/updates, Mudfish/VPN tunnels, voice chat, anti-cheat, Windows services, remote access and unrelated apps.
- Steam/Mudfish discovery is heuristic. It does not guarantee detection of every service, helper, tunnel driver, or launcher. Found paths are shown for inspection only and are not automatically allowed.
- Game executable scanning is heuristic. Confirm the selected path is the actual game executable.
- Rule removal scans the game's current folder for executable candidates. If a game executable was moved or deleted, remove its old rule manually in Windows Defender Firewall with Advanced Security.
- The app does not elevate itself. Close it and choose **Run as administrator** when a firewall action requires elevation.
- A .wfw import restores a **full Windows Firewall policy**, not just GameNet rules. The app creates a fresh pre-restore backup first, but only restore a file when you intend to replace the current policy.
- The app is not yet a complete gaming firewall. Do not rely on it to prevent other apps from connecting.

## Safe usage

1. Select a game and click **Scan Game Folder**.
2. Confirm the actual game executable.
3. Click **Preview Actions** before making changes.
4. Run as administrator and back up the current firewall policy.
5. Use **Scan Steam / Mudfish Processes** to inspect detected dependency paths. This does not create rules.
6. Add app-specific allow rules only if you understand they do not block other apps.
7. Keep a separate recovery path available before changing Windows networking settings.

## Build

On Windows with the .NET 8 SDK:

```powershell
dotnet restore .\src\GameNetFirewall\GameNetFirewall.csproj
dotnet build .\src\GameNetFirewall\GameNetFirewall.csproj -c Release
dotnet publish .\src\GameNetFirewall\GameNetFirewall.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o .\artifacts\publish
```

The GitHub Actions workflow uploads the self-contained Windows x64 artifact named GameNetFirewall-win-x64.

## Roadmap

- [x] Steam installation/library discovery
- [x] Installed-game list and search
- [x] Candidate game executable scanner
- [x] Steam/Mudfish executable discovery (best-effort)
- [x] Firewall policy backup and profile inspection
- [x] Preview and named app-specific allow rules
- [x] Remove app-owned rules and guarded full-policy restore
- [ ] Identify Steam services, launchers, anti-cheat and Mudfish tunnel dependencies comprehensively
- [ ] Strict outbound allowlisting with explicit safeguards, tested recovery mode and clear dependency handling
- [ ] Automated tests, VM testing and signed release packaging

## Technology

- C# / .NET 8 / WPF
- Windows netsh advfirewall interface
- GitHub Actions

Not affiliated with Valve, Microsoft, Mudfish, or any game publisher.
