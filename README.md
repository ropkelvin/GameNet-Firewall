# GameNet Firewall

A Windows 11 desktop utility in development for creating a focused gaming network profile: Steam plus one selected game, with optional VPN support such as Mudfish.

> **Development status:** Early scaffold. This project currently discovers installed Steam games and displays them in a desktop UI. Firewall enforcement is not implemented yet. Do not assume this build blocks or protects network traffic.

## Goals

- Automatically discover Steam installations and library folders.
- List installed Steam games from local Steam app manifests.
- Let the user select a game and inspect its install location.
- Provide a strict, explicit inbound and outbound firewall profile.
- Support optional VPN use (including Mudfish) without assuming that allowing one VPN executable is sufficient.
- Keep firewall changes reversible with backup, clear status, and a recovery path.
- Build a Windows desktop executable through GitHub Actions.

## Planned safety model

- Never silently replace the user's existing firewall policy.
- Inspect and record the current Windows Firewall profile state before applying changes.
- Apply only app-owned, clearly named rules and remove only those rules during rollback.
- Require administrator approval before changing firewall settings.
- Provide a dry-run/preview before enforcement.
- Keep inbound blocking and outbound allowlisting explicit.
- Review DNS, DHCP, VPN tunnel protocols, Steam helper processes, game launchers, anti-cheat services, and Windows networking as compatibility considerations rather than broadly allowing everything.
- Do not claim that a game is fully isolated merely because its main executable is allowed. Child processes and services may need separate rules.

## Technology

- C# / .NET 8
- WPF desktop UI
- Windows Defender Firewall integration (planned)
- GitHub Actions Windows build

## Build

On Windows with the .NET 8 SDK installed:

```powershell
dotnet restore .\src\GameNetFirewall\GameNetFirewall.csproj
dotnet build .\src\GameNetFirewall\GameNetFirewall.csproj -c Release
dotnet publish .\src\GameNetFirewall\GameNetFirewall.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o .\artifacts\publish
```

The published executable will be in `artifacts/publish`.

## Roadmap

- [x] Initial repository and project scaffold
- [x] Steam installation/library discovery
- [x] Installed-game list from app manifests
- [ ] Resolve game launch executable and helper processes robustly
- [ ] Firewall policy preview and current-policy backup
- [ ] Reversible inbound/outbound rules and recovery workflow
- [ ] Optional Mudfish/VPN profile and tunnel compatibility checks
- [ ] Automated tests and signed release packaging

## Safety notice

This software changes system networking only after a future implementation explicitly adds that capability. Strict firewall policies can interrupt VPNs, DNS, game authentication, voice chat, updates, remote access, and other applications. Test changes locally and keep a recovery method available. This project is not affiliated with Valve, Microsoft, Mudfish, or any game publisher.

## License

No license has been selected yet. All rights reserved unless a license is added to this repository.
