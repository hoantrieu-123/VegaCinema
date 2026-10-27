[CmdletBinding()]
param(
    [string]$DestinationPath
)

$ErrorActionPreference = 'Stop'
$projectRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
if ([string]::IsNullOrWhiteSpace($DestinationPath)) {
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $DestinationPath = Join-Path $projectRoot "exports\VegaCinema-source-$stamp.zip"
}
if (![System.IO.Path]::IsPathRooted($DestinationPath)) {
    $DestinationPath = Join-Path $projectRoot $DestinationPath
}
$archivePath = [System.IO.Path]::GetFullPath($DestinationPath)
if (Test-Path -LiteralPath $archivePath) {
    throw "Destination already exists. Choose a new ZIP filename: $archivePath"
}
if ([System.IO.Path]::GetExtension($archivePath) -ne '.zip') {
    throw 'Destination must have the .zip extension.'
}

# Never walk the running database, private settings, caches or build output.
$excludedDirectories = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($name in @('.git', '.vs', '.idea', '.local', '.nuget', '.packages', '.tools', '.aws', '.codex',
    'bin', 'obj', 'TestResults', 'node_modules', '__pycache__', 'exports', 'artifacts')) {
    [void]$excludedDirectories.Add($name)
}

function Get-ProjectSourceFiles([string]$Directory) {
    foreach ($item in Get-ChildItem -LiteralPath $Directory -Force) {
        # Do not follow junctions or symbolic links outside the project.
        if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) { continue }
        if ($item.PSIsContainer) {
            if (!$excludedDirectories.Contains($item.Name)) { Get-ProjectSourceFiles $item.FullName }
            continue
        }
        if ($item.FullName -eq $archivePath) { continue }
        if ($item.Name -in @('appsettings.Local.json', 'secrets.json', '.env')) { continue }
        if ($item.Name -like '.env.*' -and $item.Name -ne '.env.example') { continue }
        if ($item.Name -match '(?i)\.(user|suo|log|zip|pfx|p12|pem|key|db|sqlite|sqlite3)(-wal|-shm|-journal)?$') { continue }
        $item
    }
}

# Collect the source before creating the archive, including uncommitted changes.
$files = @(Get-ProjectSourceFiles $projectRoot | Sort-Object FullName)
if ($files.Count -eq 0) { throw 'No project source files found.' }
$archiveDirectory = Split-Path -Parent $archivePath
New-Item -ItemType Directory -Path $archiveDirectory -Force | Out-Null
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$stream = [System.IO.File]::Open($archivePath, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
$archive = $null
$completed = $false
try {
    $archive = [System.IO.Compression.ZipArchive]::new($stream, [System.IO.Compression.ZipArchiveMode]::Create, $false, [System.Text.Encoding]::UTF8)
    foreach ($file in $files) {
        $relativePath = $file.FullName.Substring($projectRoot.Length + 1).Replace('\', '/')
        [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $archive, $file.FullName, "VegaCinema/$relativePath", [System.IO.Compression.CompressionLevel]::Optimal)
    }
    $archive.Dispose()
    $archive = $null
    $completed = $true
} finally {
    if ($null -ne $archive) { $archive.Dispose() }
    $stream.Dispose()
    # Only remove the incomplete file exclusively created by this invocation.
    if (!$completed) { Remove-Item -LiteralPath $archivePath -Force }
}
$result = Get-Item -LiteralPath $archivePath
Write-Output "ZIP created: $archivePath"
Write-Output ("Files: {0}; size: {1:N2} MB" -f $files.Count, ($result.Length / 1MB))
Write-Output 'Includes current source, migrations, movie posters, scripts and documentation.'
Write-Output 'Excludes the live database, passwords, Git history, package caches and build output.'
