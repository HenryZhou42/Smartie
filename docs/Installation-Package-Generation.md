# Installation Package Generation

Step-by-step guide to build **Smartie Community Edition** installation artifacts on Windows.

| Package | Format | Best for |
|---------|--------|----------|
| **Portable** | ZIP folder + `Smartie.exe` | GitHub releases, USB, no admin |
| **MSIX** | Windows app package | Start menu, clean uninstall |

Current release line: **0.9.0 RC** · Publisher **Henry Zhou** · Identity **Smartie.Community**

For troubleshooting, data paths, and manifest details see [Packaging.md](Packaging.md).

---

## 1. Prerequisites

Install once on the build machine:

```powershell
# .NET 9 SDK — https://dotnet.microsoft.com/download
dotnet --version

# MAUI workload (admin PowerShell)
dotnet workload install maui
```

Requirements:

- Windows 10 1809+ or Windows 11 (x64)
- Visual Studio 2022 with “.NET Multi-platform App UI development” (optional but recommended)
- WebView2 Runtime (for running the built app; included on Windows 11)

Clone the repo:

```powershell
git clone https://github.com/smartie-ai/smartie.git
cd smartie
```

---

## 2. One-command release (recommended)

From the repository root, build **both** portable ZIP and MSIX:

```powershell
.\scripts\publish-release.ps1 -Version 0.9.0
```

This will:

1. Run the test suite (`dotnet test`)
2. Build the example plugin
3. Publish portable `Smartie.exe` and create a ZIP
4. Publish the MSIX package

### Options

```powershell
# Portable ZIP only
.\scripts\publish-release.ps1 -Version 0.9.0 -SkipMsix

# MSIX only
.\scripts\publish-release.ps1 -Version 0.9.0 -SkipPortable
```

---

## 3. Portable ZIP package

### Generate

```powershell
.\scripts\publish-portable.ps1 -Version 0.9.0
```

Or with the publish profile:

```powershell
dotnet publish src/Smartie.Maui/Smartie.Maui.csproj -p:PublishProfile=win-x64-portable
```

### Output

```
dist/
├── Smartie-0.9.0-portable/
│   └── publish/
│       ├── Smartie.exe          ← main executable
│       ├── Smartie.dll
│       ├── SampleData/          ← onboarding sample documents
│       ├── Plugins/ExamplePlugin/
│       ├── Docs/
│       └── README.txt
└── Smartie-0.9.0-portable.zip ← attach to GitHub Release
```

### Distribute

1. Upload `Smartie-0.9.0-portable.zip` to GitHub Releases
2. User extracts anywhere and runs `Smartie.exe`

### Uninstall (portable)

- Delete the extracted folder
- Optionally delete `%LOCALAPPDATA%\Smartie` to remove all local data

---

## 4. MSIX installer package

### Generate (unsigned — dev / sideload)

```powershell
.\scripts\publish-msix.ps1 -Version 0.9.0
```

Or:

```powershell
dotnet publish src/Smartie.Maui/Smartie.Maui.csproj -p:PublishProfile=win-x64-msix
```

### Generate (signed — production)

```powershell
.\scripts\publish-msix.ps1 -Version 0.9.0 `
    -Sign `
    -CertificatePath "C:\certs\smartie.pfx" `
    -CertificatePassword "your-password"
```

### Output

```
dist/Smartie-0.9.0-msix/
└── *.msix                       ← Windows installation package
```

Manifest: `src/Smartie.Maui/Platforms/Windows/Package.appxmanifest`

| Field | Value |
|-------|-------|
| Display name | Smartie |
| Publisher | CN=Henry Zhou |
| Version | 0.9.0.0 |
| Architecture | x64 |

### Install

Unsigned (requires **Developer mode** or enterprise sideloading):

```powershell
Add-AppxPackage -Path "dist\Smartie-0.9.0-msix\*.msix"
```

### Uninstall

Settings → Apps → Smartie → Uninstall, or:

```powershell
Get-AppxPackage *Smartie* | Remove-AppxPackage
```

---

## 5. Release configuration

Build settings are centralized in **`Directory.Build.props`** at the repo root:

| Setting | Value |
|---------|-------|
| Configuration | Release |
| Target framework | `net9.0-windows10.0.19041.0` |
| Runtime | `win-x64` |
| Self-contained | Yes |
| Executable name | `Smartie.exe` |

Product strings (About page, API) live in **`src/Smartie.Contracts/ProductMetadata.cs`** — keep in sync when bumping version.

### Bump version checklist

1. Update `SmartieVersion` in `Directory.Build.props`
2. Update `ProductMetadata.cs` (Version, BuildNumber)
3. Update `Package.appxmanifest` `Version` attribute (four-part, e.g. `0.9.0.0`)
4. Run publish scripts with the new `-Version` argument

---

## 6. What gets packaged

**Included:**

- Self-contained .NET runtime + MAUI host
- Embedded local API (SQLite, encrypted settings)
- `SampleData/` sample markdown files
- `Plugins/ExamplePlugin/` (manifest + DLL)
- Documentation (`Docs/README.md`, `Docs/LICENSE`)

**Never included** (blocked by script + project settings):

- `apikeys.json`, `.env`, `.env.local`
- `appsettings.Development.json`, `secrets.json`
- User database, Knowledge Base uploads, logs, cache

---

## 7. Verify the package

After generating, confirm before publishing:

```powershell
# Portable
Test-Path dist\Smartie-0.9.0-portable\publish\Smartie.exe
Test-Path dist\Smartie-0.9.0-portable\publish\SampleData\Smartie_Test_Document.md
Test-Path dist\Smartie-0.9.0-portable\publish\Plugins\ExamplePlugin\manifest.json
```

Manual smoke test:

- [ ] `Smartie.exe` launches
- [ ] Welcome wizard appears on first run
- [ ] `%LOCALAPPDATA%\Smartie\smartie.db` is created
- [ ] Settings and theme persist after restart
- [ ] About shows **0.9.0 (RC)** and build date
- [ ] No secret files inside the publish folder

---

## 8. GitHub Release workflow

1. Bump version (section 5)
2. Run `.\scripts\publish-release.ps1 -Version 0.9.0`
3. Add screenshots to `screenshots/` (see `screenshots/README.md`)
4. Create a GitHub Release tagged `v0.9.0`
5. Attach:
   - `dist/Smartie-0.9.0-portable.zip`
   - MSIX from `dist/Smartie-0.9.0-msix/` (if built)
6. Paste release notes (features, providers, CE limitations)

---

## 9. Script reference

| Script | Purpose |
|--------|---------|
| `scripts/publish-release.ps1` | Tests + portable + MSIX |
| `scripts/publish-portable.ps1` | Portable folder + ZIP |
| `scripts/publish-msix.ps1` | MSIX package |

| Publish profile | Path |
|-----------------|------|
| Portable | `src/Smartie.Maui/Properties/PublishProfiles/win-x64-portable.pubxml` |
| MSIX | `src/Smartie.Maui/Properties/PublishProfiles/win-x64-msix.pubxml` |

---

## 10. Common issues

| Problem | Solution |
|---------|----------|
| Visual Studio publish fails with generic error / `NETSDK1150` | Fixed in repo: Maui sets `ValidateExecutableReferencesMatchSelfContained=false` because the API runs in-process. Retry **Publish → win-x64-msix**, or run `.\scripts\publish-msix.ps1`. |
| Visual Studio publish fails / `project.assets.json` doesn't have a target for `net9.0` | Fixed in repo: shared libraries under `src/` also target `net9.0-windows10.0.19041.0`. Run `dotnet restore -r win-x64` once after pulling. |
| Visual Studio publish fails / `WindowsAppSDKSelfContained requires a supported Windows architecture` | Fixed in repo: dev builds use `WindowsAppSDKSelfContained=false`; publish profiles set Platform **x64** and `WindowsAppSDKSelfContained=true`. Reload the project and publish with **win-x64-msix**. |
| Visual Studio publish fails (generic) after MSIX build | Clean and retry: `dotnet clean src/Smartie.Maui/Smartie.Maui.csproj -c Release` then publish again. Or use `.\scripts\publish-msix.ps1` (bypasses VS Web Publish UI). |
| `Smartie.Api.exe` appears in publish output | Fixed: `Smartie.Api` sets `IsPublishable=false` and `UseAppHost=false` (embedded API only). Run **Clean** before republishing so MSIX picks up the change. |
| MSIX not in `dist/` root | The `.msix` file is under `src/Smartie.Maui/bin/Release/.../AppPackages/.../`; scripts search recursively in `dist/Smartie-*-msix/`. |
| `dotnet workload install maui` fails | Run PowerShell as Administrator |
| Publish fails on duplicate `appsettings.json` | Api `appsettings.json` is excluded from publish; use latest repo |
| MSIX install blocked | Enable sideloading or sign the package |
| Sample import empty in portable build | Ensure `SampleData/` exists next to `Smartie.exe` |
| WebView2 missing on target PC | Install [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) |

More detail: [Packaging.md](Packaging.md)
