# Smartie icon assets

| File | Purpose |
|------|---------|
| `img/smartie.png` | Canonical brand icon (MauiIcon + splash source) |
| `smartie.ico` | Multi-resolution Windows icon for unpackaged `Smartie.exe` / taskbar |
| `appicon.svg` / `appiconfg.svg` | Legacy default .NET MAUI template icons (unused) |

## How icons are applied

1. **`MauiIcon`** in `Smartie.Maui.csproj` uses `img/smartie.png` → generates Windows tile PNGs (`smartieLogo.png`, etc.) for MSIX / `Package.appxmanifest`.
2. **`ApplicationIcon`** embeds `smartie.ico` into the Win32 `Smartie.exe` resource (Explorer file icon).
3. **`WindowIconHelper`** (Windows) sets the running window/taskbar icon from `smartie.ico` next to the EXE (important for unpackaged portable builds).

## Refresh after changing the PNG

```powershell
# Optional: regenerate smartie.ico from the PNG (sizes 16–256)
# then:
dotnet clean src/Smartie.Maui/Smartie.Maui.csproj -c Release
Remove-Item -Recurse -Force src/Smartie.Maui/bin, src/Smartie.Maui/obj -ErrorAction SilentlyContinue
dotnet publish src/Smartie.Maui/Smartie.Maui.csproj -c Release -p:PublishProfile=win-x64-portable
```

Verify: Explorer icon on `dist\...\publish\Smartie.exe`, then run the EXE and check the taskbar.
