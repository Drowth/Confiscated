param([Parameter(Mandatory=$true)][string]$BuildRoot)
$ErrorActionPreference = 'Stop'
$releaseRoot = (Resolve-Path -LiteralPath $BuildRoot).Path
$playerFolder = Join-Path $releaseRoot 'Windows'
if ((Get-Content -LiteralPath (Join-Path $releaseRoot 'BuildReport.txt') -TotalCount 1) -ne 'Succeeded') {
    throw 'Only successful store builds can be packaged.'
}
foreach ($required in @('Confiscated.exe', 'UnityPlayer.dll', 'Confiscated_Data', 'README.txt')) {
    if (-not (Test-Path -LiteralPath (Join-Path $playerFolder $required))) { throw "Missing $required" }
}
$files = @(Get-ChildItem -LiteralPath $playerFolder -File -Recurse -Force)
$unwanted = @($files | Where-Object { $_.FullName -match '(?i)(DoNotShip|[\\/]\.git[\\/]|[\\/]steam_appid\.txt$|\.pdb$)' })
if ($unwanted.Count) { throw "Development files found in upload folder: $($unwanted.FullName -join ', ')" }
$leftovers = @($files | Where-Object { $_.Name -match '(?i)(claude|codex|agents\.md|skill\.md|prompt|\.log$|\.cs$|^\.env)' })
if ($leftovers.Count) { throw "Development leftovers found in player: $($leftovers.Name -join ', ')" }
$packedLeftovers = Select-String -LiteralPath (Join-Path $releaseRoot 'PackedAssets.tsv') -Pattern '(?i)(PerformanceTestRun(Info|Settings)\.json|[\\/](CLAUDE|AGENTS|SKILL)\.md|[\\/]\.codex[\\/])'
if ($packedLeftovers) { throw 'Development metadata was packed into player resources.' }
$archive = Join-Path $releaseRoot 'Confiscated-Windows.zip'
if (Test-Path -LiteralPath $archive) { throw "Archive already exists: $archive" }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($playerFolder, $archive, [System.IO.Compression.CompressionLevel]::Optimal, $false)

# Read every compressed entry back and compare its contents to the source file.
$zip = [System.IO.Compression.ZipFile]::OpenRead($archive)
try {
    $entries = @($zip.Entries | Where-Object { $_.Name })
    if ($entries.Count -ne $files.Count) { throw 'Archive file count mismatch.' }
    foreach ($entry in $entries) {
        $source = Join-Path $playerFolder ($entry.FullName.Replace('/', [IO.Path]::DirectorySeparatorChar))
        $expected = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
        $stream = $entry.Open()
        $sha = [Security.Cryptography.SHA256]::Create()
        try { $actual = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') }
        finally { $stream.Dispose(); $sha.Dispose() }
        if ($actual -ne $expected) { throw "Archive verification failed: $($entry.FullName)" }
    }
} finally { $zip.Dispose() }
$hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
"$hash  Confiscated-Windows.zip" | Set-Content -LiteralPath (Join-Path $releaseRoot 'SHA256SUMS.txt')
$installed = ($files | Measure-Object -Property Length -Sum).Sum
$download = (Get-Item -LiteralPath $archive).Length
"Verified $($files.Count) files. Installed: $installed bytes. ZIP: $download bytes. SHA256: $hash" |
    Tee-Object -FilePath (Join-Path $releaseRoot 'PackageValidation.txt')
