param([Parameter(Mandatory)][string]$Path)
$ErrorActionPreference = "Stop"
$resolved = (Resolve-Path -LiteralPath $Path).Path
if (Test-Path -LiteralPath $resolved -PathType Leaf) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($resolved)
    try { $entries = @($archive.Entries | ForEach-Object { $_.FullName }) }
    finally { $archive.Dispose() }
} else {
    $entries = @(Get-ChildItem -LiteralPath $resolved -Recurse -Force -File |
        ForEach-Object { [IO.Path]::GetRelativePath($resolved, $_.FullName) })
}
foreach ($relative in $entries) {
    $name = [IO.Path]::GetFileName($relative)
    if ($name -in @("apikeys.json", "secrets.json", "appsettings.Development.json", "Smartie.Api.exe") -or
        $name -like ".env*" -or $name -like "appsettings.*.local.json" -or
        $name -match '\.(db|sqlite|sqlite3)(-wal|-shm)?$|\.(pfx|p12|pem|key|log)$' -or
        $relative -match '(^|[/\\])(KnowledgeBase|ChatAttachments|Logs|Cache|AutomationExports|\.git)([/\\]|$)') {
        throw "Release content rejected: $relative"
    }
}
Write-Host "Release content check passed ($($entries.Count) entries)."
