[CmdletBinding()]
param([string]$CacheDirectory)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$lockPath = Join-Path $PSScriptRoot 'mupdf.lock.json'
$lock = Get-Content -LiteralPath $lockPath -Raw | ConvertFrom-Json
if (-not $CacheDirectory) { $CacheDirectory = Join-Path $repoRoot '.cache\mupdf' }
$cacheRoot = Join-Path ([IO.Path]::GetFullPath($CacheDirectory)) $lock.version
New-Item -ItemType Directory -Force -Path $cacheRoot | Out-Null

function Assert-Asset($Path, $Asset) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Missing official asset: $($Asset.fileName)" }
    if ((Get-Item -LiteralPath $Path).Length -ne $Asset.size) { throw "Wrong size for $($Asset.fileName)." }
    if ((Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $Asset.sha256) {
        throw "SHA-256 mismatch for $($Asset.fileName)."
    }
}

function Get-OfficialAsset($Asset) {
    $destination = Join-Path $cacheRoot $Asset.fileName
    if (Test-Path -LiteralPath $destination -PathType Leaf) {
        # A modified/incomplete cache fails closed; it is never executed or silently accepted.
        Assert-Asset $destination $Asset
        return $destination
    }
    $download = Join-Path $cacheRoot ($Asset.fileName + '.' + [Guid]::NewGuid().ToString('N') + '.partial')
    Write-Host "Downloading official MuPDF asset: $($Asset.fileName)"
    try {
        $previousProgress = $ProgressPreference
        $ProgressPreference = 'SilentlyContinue'
        try {
            Invoke-WebRequest -UseBasicParsing -Uri $Asset.url -OutFile $download -TimeoutSec 300
        } finally { $ProgressPreference = $previousProgress }
        Assert-Asset $download $Asset
        Move-Item -LiteralPath $download -Destination $destination
    } finally {
        if (Test-Path -LiteralPath $download -PathType Leaf) { Remove-Item -LiteralPath $download }
    }
    return $destination
}

$windowsArchive = Get-OfficialAsset $lock.windows
$sourceArchive = Get-OfficialAsset $lock.source
if (@($lock.sourceNotices).Count -eq 0) { throw 'The locked upstream license inventory is missing.' }

# Fresh directories avoid trusting stale extracted files. Only exact locked paths are extracted.
$restoredRoot = Join-Path $cacheRoot ('restore-' + [Guid]::NewGuid().ToString('N'))
$noticesRoot = Join-Path $restoredRoot 'licenses\MuPDF'
New-Item -ItemType Directory -Force -Path $noticesRoot | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead($windowsArchive)
try {
    foreach ($name in @($lock.renderer.fileName, 'COPYING.txt', 'README.txt', 'CHANGES.txt')) {
        $entry = $zip.GetEntry($lock.windows.root + '/' + $name)
        if ($null -eq $entry) { throw "Official Windows archive is missing $name." }
        $destination = if ($name -eq $lock.renderer.fileName) {
            Join-Path $restoredRoot $name
        } else { Join-Path $noticesRoot $name }
        [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $destination)
    }
} finally { $zip.Dispose() }
$renderer = Join-Path $restoredRoot $lock.renderer.fileName
if ((Get-FileHash -LiteralPath $renderer -Algorithm SHA256).Hash.ToLowerInvariant() -ne $lock.renderer.sha256) {
    throw 'The extracted PDF component does not match the locked official executable.'
}

$sourceStage = Join-Path $restoredRoot 'source-notices'
New-Item -ItemType Directory -Path $sourceStage | Out-Null
$noticeEntries = @($lock.sourceNotices | ForEach-Object { $lock.source.root + '/' + $_ })
& tar.exe -xzf $sourceArchive -C $sourceStage -- @noticeEntries
if ($LASTEXITCODE -ne 0) { throw 'Cannot extract the required upstream license notices.' }
foreach ($relativePath in $lock.sourceNotices) {
    $sourcePath = Join-Path (Join-Path $sourceStage $lock.source.root) $relativePath
    if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) { throw "Missing upstream notice: $relativePath" }
    $destination = Join-Path (Join-Path $noticesRoot 'source') $relativePath
    New-Item -ItemType Directory -Force -Path (Split-Path $destination -Parent) | Out-Null
    Copy-Item -LiteralPath $sourcePath -Destination $destination
}

# mutool writes its version to stderr. Read both pipes directly, then check its exit code.
$start = New-Object Diagnostics.ProcessStartInfo
$start.FileName = $renderer
$start.Arguments = '-v'
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.RedirectStandardOutput = $true
$start.RedirectStandardError = $true
$process = [Diagnostics.Process]::Start($start)
try {
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    if (-not $process.WaitForExit(15000)) { $process.Kill(); throw 'PDF component version check timed out.' }
    $versionText = $stdout.GetAwaiter().GetResult() + $stderr.GetAwaiter().GetResult()
    if ($process.ExitCode -ne 0 -or $versionText.Trim() -ne ('mutool version ' + $lock.version)) {
        throw 'The PDF component is not the expected working MuPDF version.'
    }
} finally { $process.Dispose() }
Write-Host "Verified official MuPDF $($lock.version) and corresponding source."
[pscustomobject]@{
    Version = $lock.version
    RendererPath = $renderer
    RendererSha256 = $lock.renderer.sha256
    NoticesPath = $noticesRoot
    SourceArchivePath = $sourceArchive
    SourceArchiveName = $lock.source.fileName
    SourceSha256 = $lock.source.sha256
    ReleaseUrl = $lock.releaseUrl
}
