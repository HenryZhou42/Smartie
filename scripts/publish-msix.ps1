# Publish Smartie Community Edition — MSIX package (Windows x64)
param(
    [string]$Configuration = "Release",
    [string]$Version = "0.9.0",
    [string]$Runtime = "win-x64",
    [switch]$Sign,
    [string]$CertificatePath = "",
    [string]$CertificatePassword = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$outDir = Join-Path $root "dist/Smartie-$Version-msix"
$mauiProject = Join-Path $root "src/Smartie.Maui/Smartie.Maui.csproj"

Push-Location $root
try {
    Write-Host "Building Smartie $Version MSIX ($Runtime)..." -ForegroundColor Cyan

    dotnet build plugins/Smartie.Plugins.ExamplePlugin/Smartie.Plugins.ExamplePlugin.csproj -c $Configuration
    if ($LASTEXITCODE -ne 0) { throw "Example plugin build failed with exit code $LASTEXITCODE" }

    $props = @(
        "-c", $Configuration,
        "-f", "net9.0-windows10.0.19041.0",
        "-p:PublishProfile=win-x64-msix"
    )

    if ($Sign -and $CertificatePath) {
        $props += @(
            "-p:AppxPackageSigningEnabled=true",
            "-p:PackageCertificateKeyFile=$CertificatePath"
        )
        if ($CertificatePassword) {
            $props += "-p:PackageCertificatePassword=$CertificatePassword"
        }
    }
    else {
        Write-Host "Note: package is unsigned. Enable sideloading or sign with -Sign -CertificatePath for distribution." -ForegroundColor Yellow
    }

    dotnet publish $mauiProject @props
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

    $searchRoots = @(
        $outDir,
        (Join-Path $root "src/Smartie.Maui/bin/Release/net9.0-windows10.0.19041.0/win-x64/AppPackages"),
        (Join-Path $root "src/Smartie.Maui/bin/Any CPU/Release/net9.0-windows10.0.19041.0/win-x64/AppPackages")
    )

    $msix = $null
    foreach ($searchRoot in $searchRoots) {
        if (-not (Test-Path $searchRoot)) { continue }
        $msix = Get-ChildItem -Path $searchRoot -Recurse -Filter "*.msix" -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1
        if ($msix) { break }
    }

    if ($msix) {
        $distMsix = Join-Path $outDir "Smartie_$($Version)_x64.msix"
        New-Item -ItemType Directory -Force -Path $outDir | Out-Null
        $sourceFull = [System.IO.Path]::GetFullPath($msix.FullName)
        $destFull = [System.IO.Path]::GetFullPath($distMsix)
        if ($sourceFull -ne $destFull) {
            Copy-Item -Path $msix.FullName -Destination $distMsix -Force
        }

        Write-Host ""
        Write-Host "MSIX package ready:" -ForegroundColor Green
        Write-Host "  $destFull"
        Write-Host ""
        Write-Host "Install (unsigned / sideloading enabled):"
        Write-Host "  Add-AppxPackage -Path `"$destFull`""
    }
    else {
        throw "Publish completed but no .msix was found under dist/ or AppPackages/."
    }
}
finally {
    Pop-Location
}
