# Packaging Smartie 0.9.0 Beta

Run from the repository root on Windows with the .NET 9 SDK, MAUI workload,
Windows SDK/MSIX tooling and NuGet connectivity.

## Portable (primary)

    powershell -ExecutionPolicy Bypass -File scripts/publish-portable.ps1

The script builds the example plugin and publishes Windows x64 with:

    dotnet publish src/Smartie.Maui/Smartie.Maui.csproj -c Release -f net9.0-windows10.0.19041.0 -r win-x64 --self-contained true -p:PublishProfile=win-x64-portable -p:WindowsAppSDKSelfContained=true -p:SmartieVersion=0.9.0 -p:PublishDir=<fresh-absolute-directory>/

Outputs:
- dist/Smartie-v0.9.0-beta-win-x64-portable.zip
- The adjacent .sha256 checksum
- dist/Smartie-v0.9.0-beta-win-x64-portable-<run-id>/publish/Smartie.exe

Each run uses a new staging directory; existing artifacts are preserved, and the
stable ZIP/checksum names are replaced after a successful publish and content check.
The .NET and Windows App SDK runtimes are bundled. WebView2 remains a prerequisite.
Extract the entire ZIP before launching. “Portable” means no installer; data is
stored in %LOCALAPPDATA%/Smartie, not beside the executable.

## MSIX (secondary)

    powershell -ExecutionPolicy Bypass -File scripts/publish-msix.ps1

Outputs are printed by the script:
- dist/Smartie-v0.9.0-beta-msix-<run-id>/Smartie_0.9.0_x64.msix
- An AppPackages-style subfolder with the original generated package.

The default package is unsigned, for build validation. Before distributing an
installable MSIX, sign it with a trusted certificate matching CN=Henry Zhou.
Enabling developer mode does not replace proper package signing.
The existing MsixPackage launch profile is retained.

Both scripts accept -Version 0.9.0 and -Runtime win-x64. Other architectures are
intentionally rejected. Display/package versions remain numeric; Beta is shown in
About and assembly informational metadata.

## Release safety

Test-ReleaseContent.ps1 rejects database files/sidecars, local config, dotenv files,
private keys/certificates, logs and user upload/cache directories. It runs before
portable ZIP creation. It is not a substitute for reviewing source/configuration
for embedded secrets. Example plugin binaries and curated TestData are intentional.

Do not copy %LOCALAPPDATA%/Smartie into a release. No certificate or private API key
is needed to build the portable distribution. Cloud-provider validation uses your
own credentials; credentials are never supplied with the app.

## Verification

    dotnet test tests/Smartie.Tests/Smartie.Tests.csproj -c Release
    dotnet build src/Smartie.Web/Smartie.Web.csproj -c Release

For an isolated launch, set SMARTIE_DATA_ROOT to an absolute disposable directory
in the process environment before running Smartie.exe. Leave it unset for ordinary
use. Never point it at a real user's library during destructive test scenarios.

Test on a clean Windows x64 VM with WebView2 before promoting Beta to RC. Confirm
first launch, configure/test provider, streaming/stop/edit, imports, themes and
restart. The development machine already has SDK/runtime dependencies, so its
launch alone cannot prove clean-machine readiness. See Release-Verification.md.

## Troubleshooting

- NETSDK1112: restore/publish with -r win-x64 and self-contained enabled; a prior
  framework-only restore is insufficient.
- Missing mspdbcmf.exe: symbol-package warning; the application MSIX can still build.
- Missing WebView2: install the official Microsoft WebView2 runtime.
- No trusted signature: distribute the portable beta or sign the MSIX.
- Preserve failed output for diagnosis; do not recursively delete user data.
