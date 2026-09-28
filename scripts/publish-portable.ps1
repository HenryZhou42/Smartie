# Build a fresh, self-contained Windows x64 portable distribution.
param(
    [ValidateSet("Release")][string]$Configuration = "Release",
    [ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version = "0.9.0",
    [ValidateSet("win-x64")][string]$Runtime = "win-x64"
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$releaseName = "Smartie-v$Version-beta-$Runtime-portable"
# A fresh staging folder prevents previous output/user files entering a new release.
$publishDir = Join-Path $root "dist/$releaseName-$([Guid]::NewGuid().ToString('N').Substring(0,8))/publish"
$zipPath = Join-Path $root "dist/$releaseName.zip"
Push-Location $root
try {
    dotnet build plugins/Smartie.Plugins.ExamplePlugin/Smartie.Plugins.ExamplePlugin.csproj -c $Configuration
    if ($LASTEXITCODE -ne 0) { throw "Example plugin build failed." }
    dotnet publish src/Smartie.Maui/Smartie.Maui.csproj -c $Configuration -f net9.0-windows10.0.19041.0 -r $Runtime --self-contained true -p:PublishProfile=win-x64-portable -p:WindowsAppSDKSelfContained=true "-p:SmartieVersion=$Version" "-p:PublishDir=$publishDir/"
    if ($LASTEXITCODE -ne 0) { throw "Portable publish failed." }
    foreach ($required in @("Smartie.exe", "coreclr.dll", "hostfxr.dll", "wwwroot/index.html")) {
        if (!(Test-Path -LiteralPath (Join-Path $publishDir $required))) { throw "Required portable file missing: $required" }
    }
    @"
Smartie Community Edition $Version Beta (Windows x64)
Run Smartie.exe. Requires Windows 10 1809+ or Windows 11 and Microsoft WebView2 Runtime.
The .NET and Windows App SDK runtimes are bundled. WebView2 is a separate prerequisite.
Open Settings, save your provider key/model, select Use this, then Test active provider.
Cloud chat sends prompts/context to your provider; embeddings use Google Gemini.
Local data: %LOCALAPPDATA%\Smartie. API keys use Windows current-user DPAPI.
See Docs/README.md and Docs/Packaging.md. Unsigned builds may show SmartScreen warnings.
Uninstall: delete the extracted app folder. Delete local data only if you intend to lose it.
"@ | Set-Content -LiteralPath (Join-Path $publishDir "README.txt") -Encoding utf8
    & (Join-Path $PSScriptRoot "Test-ReleaseContent.ps1") -Path $publishDir
    Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -Force
    Get-FileHash -LiteralPath $zipPath -Algorithm SHA256 |
        ForEach-Object { "$($_.Hash)  $([IO.Path]::GetFileName($zipPath))" } |
        Set-Content -LiteralPath "$zipPath.sha256"
    Write-Host "Portable folder: $publishDir"
    Write-Host "Portable ZIP: $zipPath"
}
finally { Pop-Location }
