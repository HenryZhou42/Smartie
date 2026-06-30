# Build all Smartie Community Edition release artifacts
param(
    [string]$Version = "0.9.0",
    [string]$Configuration = "Release",
    [switch]$SkipMsix,
    [switch]$SkipPortable
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

Push-Location $root
try {
    Write-Host "=== Smartie Community Edition $Version Release ===" -ForegroundColor Cyan
    dotnet test tests/Smartie.Tests/Smartie.Tests.csproj -c $Configuration --no-restore:$false

    if (-not $SkipPortable) {
        & "$PSScriptRoot/publish-portable.ps1" -Configuration $Configuration -Version $Version
    }

    if (-not $SkipMsix) {
        & "$PSScriptRoot/publish-msix.ps1" -Configuration $Configuration -Version $Version
    }

    Write-Host ""
    Write-Host "Release artifacts under dist/" -ForegroundColor Green
}
finally {
    Pop-Location
}
