# Smartie icon assets

MAUI generates platform icons (PNG tiles, `.ico` for Windows) from the source SVG files at build time.

| File | Purpose |
|------|---------|
| `appicon.svg` | Background (Smartie purple `#7C5CFF`) |
| `appiconfg.svg` | Foreground mark |

Built outputs appear under `obj/` and in MSIX packages as `appiconLogo.png`, store logos, and splash assets.

To refresh icons after editing SVGs:

```powershell
dotnet build src/Smartie.Maui/Smartie.Maui.csproj -c Release
```

For manual PNG/ICO exports, use any SVG tool and place optional assets here — the csproj uses SVG as the canonical source.
