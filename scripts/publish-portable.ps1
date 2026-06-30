# Publish Smartie Community Edition — portable Windows x64 ZIP
param(
    [string]$Configuration = "Release",
    [string]$Version = "0.9.0",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $root "dist/Smartie-$Version-portable/publish"
$zipPath = Join-Path $root "dist/Smartie-$Version-portable.zip"
$mauiProject = Join-Path $root "src/Smartie.Maui/Smartie.Maui.csproj"

Push-Location $root
try {
    Write-Host "Building Smartie $Version portable ($Runtime)..." -ForegroundColor Cyan

    dotnet build plugins/Smartie.Plugins.ExamplePlugin/Smartie.Plugins.ExamplePlugin.csproj -c $Configuration
    if ($LASTEXITCODE -ne 0) { throw "Example plugin build failed with exit code $LASTEXITCODE" }

    dotnet publish $mauiProject `
        -c $Configuration `
        -f net9.0-windows10.0.19041.0 `
        -p:PublishProfile=win-x64-portable
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

    $readme = @"
Smartie Community Edition $Version (Portable)
============================================

Run Smartie.exe — no installer required.

Requirements:
- Windows 10 1809+ or Windows 11
- WebView2 Runtime (included in Windows 11; install from Microsoft if missing)

Your data is stored in:
  %LOCALAPPDATA%\Smartie

Folders created on first launch:
  KnowledgeBase, ChatAttachments, Memory, Tasks, Plugins, Logs, Cache, smartie.db

Sample documents: SampleData\
Example plugin: Plugins\ExamplePlugin\

Documentation: Docs\README.md

Uninstall: delete this folder and optionally remove %LOCALAPPDATA%\Smartie
"@
    Set-Content -Path (Join-Path $publishDir "README.txt") -Value $readme -Encoding UTF8

    $forbidden = @("apikeys.json", ".env", ".env.local", "appsettings.Development.json", "secrets.json", "Smartie.Api.exe")
    foreach ($name in $forbidden) {
        Get-ChildItem -Path $publishDir -Recurse -Filter $name -ErrorAction SilentlyContinue | ForEach-Object {
            throw "Packaging safety check failed: forbidden file found — $($_.FullName)"
        }
    }

    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    if (Test-Path $publishDir) {
        Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -Force
    }

    Write-Host ""
    Write-Host "Portable build ready:" -ForegroundColor Green
    Write-Host "  Folder: $publishDir"
    Write-Host "  ZIP:    $zipPath"
    Write-Host "  Run:    $publishDir\Smartie.exe"
}
finally {
    Pop-Location
}
