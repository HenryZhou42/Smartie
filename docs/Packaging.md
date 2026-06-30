# Packaging Smartie Community Edition

> **Quick start:** [Installation-Package-Generation.md](Installation-Package-Generation.md) — step-by-step commands to generate portable ZIP and MSIX packages.

Smartie Community Edition ships as a **Windows desktop** app (.NET MAUI Blazor Hybrid). Two primary distribution formats are supported:

| Format | Use case |
|--------|----------|
| **Portable ZIP** | GitHub releases, quick sideload, no installer |
| **MSIX** | Start menu + taskbar integration, clean uninstall |

Version **0.9.0 RC** · Publisher **Henry Zhou** · Package identity **Smartie.Community**

---

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- MAUI workload: `dotnet workload install maui`
- Windows 10 1809+ or Windows 11 (x64)
- WebView2 (included on Windows 11)

For **signed MSIX** (recommended for broad distribution):

- Code signing certificate (`.pfx`) trusted on target machines

---

## Release configuration

Release builds use `Directory.Build.props` for shared metadata:

- Configuration: **Release**
- Target: **Windows** (`net9.0-windows10.0.19041.0`)
- Architecture: **x64** (`win-x64`)
- Output executable: **Smartie.exe** (assembly name `Smartie`)

Product metadata lives in `src/Smartie.Contracts/ProductMetadata.cs` (keep in sync with `Directory.Build.props`).

---

## Build everything (recommended)

From the repository root:

```powershell
.\scripts\publish-release.ps1 -Version 0.9.0
```

This runs tests, then produces portable and MSIX artifacts under `dist/`.

Options:

```powershell
.\scripts\publish-release.ps1 -Version 0.9.0 -SkipMsix      # portable only
.\scripts\publish-release.ps1 -Version 0.9.0 -SkipPortable  # MSIX only
```

---

## Portable ZIP release

```powershell
.\scripts\publish-portable.ps1 -Version 0.9.0
```

**Output:**

```
dist/
  Smartie-0.9.0-portable/
    publish/
      Smartie.exe
      Smartie.dll
      Resources/
      SampleData/
      Plugins/ExamplePlugin/
      Docs/
      README.txt
  Smartie-0.9.0-portable.zip
```

Or use the publish profile:

```powershell
dotnet publish src/Smartie.Maui/Smartie.Maui.csproj -p:PublishProfile=win-x64-portable
```

### Install (portable)

1. Extract `Smartie-0.9.0-portable.zip` to any folder (e.g. `C:\Apps\Smartie`)
2. Run `Smartie.exe`
3. Complete the welcome wizard (optional sample import)

### Uninstall (portable)

1. Delete the extracted folder
2. Optionally delete `%LOCALAPPDATA%\Smartie` to remove all local data

---

## MSIX package

```powershell
.\scripts\publish-msix.ps1 -Version 0.9.0
```

Signed build:

```powershell
.\scripts\publish-msix.ps1 -Version 0.9.0 -Sign -CertificatePath "cert.pfx" -CertificatePassword "password"
```

Or:

```powershell
dotnet publish src/Smartie.Maui/Smartie.Maui.csproj -p:PublishProfile=win-x64-msix
```

**Output:** `dist/Smartie-0.9.0-msix/` (contains `.msix` or AppPackages folder)

Manifest: `src/Smartie.Maui/Platforms/Windows/Package.appxmanifest`

| Field | Value |
|-------|-------|
| Display name | Smartie |
| Publisher | CN=Henry Zhou |
| Version | 0.9.0.0 |
| Processor | x64 |
| Capability | `runFullTrust` (local desktop app) |

### Install (MSIX)

Unsigned (developer sideloading enabled):

```powershell
Add-AppxPackage -Path "dist\Smartie-0.9.0-msix\*.msix"
```

### Uninstall (MSIX)

Settings → Apps → Smartie → Uninstall, or:

```powershell
Get-AppxPackage *Smartie* | Remove-AppxPackage
```

---

## What is packaged

Included in release builds:

- `Smartie.exe` + .NET runtime (self-contained)
- Blazor UI + embedded local API
- `SampleData/` (onboarding sample markdown files)
- `Plugins/ExamplePlugin/` (manifest + example DLL)
- `Docs/README.md`, `Docs/LICENSE`

**Never packaged** (enforced by script + csproj):

- `apikeys.json`, `.env`, `.env.local`
- `appsettings.Development.json`, `secrets.json`
- User SQLite database, Knowledge Base uploads, chat attachments, logs, cache

---

## Local application data

On first launch Smartie creates:

```
%LOCALAPPDATA%\Smartie\
├── smartie.db          # SQLite (conversations, settings, encrypted keys)
├── KnowledgeBase\      # uploaded documents
├── ChatAttachments\
├── Memory\
├── Tasks\
├── Plugins\            # user-installed plugins
├── Logs\
├── Cache\
└── AutomationExports\
```

Legacy `%LOCALAPPDATA%\Smartie\Documents` is migrated to `KnowledgeBase` automatically.

---

## First launch

1. Folders and database are created automatically
2. Welcome wizard runs (if not completed)
3. Optional sample document import from `SampleData/`
4. Example plugin seeded to `%LOCALAPPDATA%\Smartie\Plugins\ExamplePlugin\`

---

## Icons

Source SVG icons: `src/Smartie.Maui/Resources/AppIcon/`

MAUI generates PNG tiles and Windows icon assets at build time. After changing SVGs:

```powershell
dotnet build src/Smartie.Maui/Smartie.Maui.csproj -c Release
```

Verify: taskbar icon, Start menu tile (MSIX), window title **Smartie Community Edition**.

---

## Installer verification checklist

After building a release candidate:

- [ ] `Smartie.exe` launches without errors
- [ ] `%LOCALAPPDATA%\Smartie\smartie.db` created
- [ ] Settings persist after restart
- [ ] Theme / appearance persists
- [ ] Knowledge Base upload works
- [ ] Chat streams responses (with configured provider)
- [ ] Memory, Tasks, Files pages load
- [ ] `%LOCALAPPDATA%\Smartie\Plugins` exists
- [ ] No `apikeys.json` / `.env` in publish folder
- [ ] About page shows version **0.9.0 (RC)**, build date, edition

---

## Troubleshooting

| Issue | Fix |
|-------|-----|
| WebView2 missing | Install [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) |
| MSIX blocked | Enable sideloading or sign the package |
| Sample import empty | Ensure `SampleData/` exists next to `Smartie.exe` |
| Plugin example missing | Rebuild with Release; check `Plugins/ExamplePlugin/manifest.json` |
| Port in use | MAUI picks next free port automatically; check logs in `%LOCALAPPDATA%\Smartie\Logs` |

---

## GitHub release

1. Run `.\scripts\publish-release.ps1 -Version 0.9.0`
2. Attach `Smartie-0.9.0-portable.zip` and MSIX to GitHub Release
3. Add screenshots from `screenshots/` (see `screenshots/README.md`)

---

## Future

Traditional WiX/Setup installer may be added later. Community Edition remains **local-only** with no cloud dependencies.
