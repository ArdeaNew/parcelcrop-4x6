[CmdletBinding()]
param([ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version, [string]$RendererCacheDirectory)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
if (-not $Version) {
    [xml]$buildProperties = Get-Content -LiteralPath (Join-Path $repoRoot 'Directory.Build.props') -Raw
    $Version = [string]$buildProperties.Project.PropertyGroup.Version
}
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Project version must be major.minor.patch.' }
if ($env:GITHUB_REF_TYPE -eq 'tag' -and $env:GITHUB_REF_NAME -ne "v$Version") {
    throw 'Release tag must match the project version.'
}
$renderer = & (Join-Path $PSScriptRoot 'restore-renderer.ps1') -CacheDirectory $RendererCacheDirectory
if (-not $renderer.RendererPath -or -not $renderer.SourceArchivePath) { throw 'A complete verified PDF component is required for packaging.' }
& (Join-Path $PSScriptRoot 'test.ps1') -RendererPath $renderer.RendererPath
$binaryRoot = Join-Path $repoRoot 'src\ParcelCrop\bin\Release\net48'
$exe = Join-Path $binaryRoot 'ParcelCrop.exe'
$actualVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($exe).FileVersion
if ($actualVersion -ne "$Version.0") { throw "Requested version $Version differs from executable version $actualVersion." }
$artifactRoot = Join-Path $repoRoot 'artifacts'
$packageName = "ParcelCrop-4x6-$Version-win-x64"
# Each run has a fresh staging directory; no recursive deletion of user paths.
$stage = Join-Path $artifactRoot ("stage-" + [Guid]::NewGuid().ToString('N'))
$packageRoot = Join-Path $stage $packageName
New-Item -ItemType Directory -Force -Path $packageRoot | Out-Null
Copy-Item -LiteralPath $exe,(Join-Path $binaryRoot 'ParcelCrop.exe.config') -Destination $packageRoot
Copy-Item -LiteralPath $renderer.RendererPath -Destination (Join-Path $packageRoot 'mutool.exe')
New-Item -ItemType Directory -Path (Join-Path $packageRoot 'licenses') | Out-Null
Copy-Item -LiteralPath $renderer.NoticesPath -Destination (Join-Path $packageRoot 'licenses\MuPDF') -Recurse
$sourceDownload = "https://github.com/ArdeaNew/parcelcrop-4x6/releases/download/v$Version/$($renderer.SourceArchiveName)"
@"
MuPDF $($renderer.Version) - unmodified official Windows executable

Upstream release: $($renderer.ReleaseUrl)
License: GNU Affero General Public License, version 3 or later.
See COPYING.txt, README.txt and the source/ notice files in this directory.

The complete corresponding upstream source is supplied with this ParcelCrop release:
$sourceDownload
Release page: https://github.com/ArdeaNew/parcelcrop-4x6/releases/tag/v$Version

mutool.exe SHA-256: $($renderer.RendererSha256)
$($renderer.SourceArchiveName) SHA-256: $($renderer.SourceSha256)

The upstream source archive includes third-party sources and Windows build files.
Windows build: platform/win32/mupdf.sln, Release|x64, Visual Studio v142 and Windows SDK 10.0.
"@ | Set-Content -LiteralPath (Join-Path $packageRoot 'licenses\MuPDF\SOURCE.txt') -Encoding utf8
Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE'),(Join-Path $repoRoot 'README.md'),(Join-Path $repoRoot 'README.en.md'),(Join-Path $repoRoot 'THIRD_PARTY_NOTICES.md'),(Join-Path $repoRoot 'CHANGELOG.md'),(Join-Path $repoRoot 'CONTRIBUTING.md'),(Join-Path $repoRoot 'SECURITY.md') -Destination $packageRoot
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs') -Destination $packageRoot -Recurse
New-Item -ItemType Directory -Path (Join-Path $packageRoot 'tests') | Out-Null
Copy-Item -LiteralPath (Join-Path $repoRoot 'tests\README.md') -Destination (Join-Path $packageRoot 'tests\README.md')
$candidateArchive = Join-Path $stage "$packageName.zip"
Compress-Archive -LiteralPath $packageRoot -DestinationPath $candidateArchive
$candidateSourceArchive = Join-Path $stage $renderer.SourceArchiveName
Copy-Item -LiteralPath $renderer.SourceArchivePath -Destination $candidateSourceArchive
if ((Get-FileHash -LiteralPath $candidateSourceArchive -Algorithm SHA256).Hash.ToLowerInvariant() -ne $renderer.SourceSha256) {
    throw 'The corresponding source artifact failed verification.'
}

# Exercise the extracted artifact in a Unicode/space path, without an environment override.
$verifyName = 'verify-' + [char]0x89e3 + [char]0x538b + ' ' + [Guid]::NewGuid().ToString('N')
$verifyRoot = Join-Path $stage $verifyName
[IO.Compression.ZipFile]::ExtractToDirectory($candidateArchive, $verifyRoot)
$unpacked = Join-Path $verifyRoot $packageName
$packedRenderer = Join-Path $unpacked 'mutool.exe'
foreach ($required in @('ParcelCrop.exe', 'ParcelCrop.exe.config', 'mutool.exe', 'licenses\MuPDF\SOURCE.txt', 'licenses\MuPDF\COPYING.txt')) {
    if (-not (Test-Path -LiteralPath (Join-Path $unpacked $required) -PathType Leaf)) { throw "Incomplete portable archive: $required" }
}
if ((Get-FileHash -LiteralPath $packedRenderer -Algorithm SHA256).Hash.ToLowerInvariant() -ne $renderer.RendererSha256) {
    throw 'The renderer inside the portable archive failed verification.'
}
function Invoke-PackedRenderer([string]$Arguments) {
    $start = New-Object Diagnostics.ProcessStartInfo
    $start.FileName = $packedRenderer
    $start.Arguments = $Arguments
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $process = [Diagnostics.Process]::Start($start)
    try {
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(30000)) { $process.Kill(); throw 'The packaged renderer smoke test timed out.' }
        $output = $stdout.GetAwaiter().GetResult()
        $errors = $stderr.GetAwaiter().GetResult()
        if ($process.ExitCode -ne 0) { throw "The packaged renderer failed: $errors" }
        return ($output + $errors).Trim()
    } finally { $process.Dispose() }
}
$packedVersion = Invoke-PackedRenderer '-v'
if ($packedVersion -ne ('mutool version ' + $renderer.Version)) { throw 'The packaged renderer has the wrong version.' }

# This generated rectangle PDF contains no user data and is not added to the ZIP.
$smokePdf = Join-Path $verifyRoot 'package-smoke.pdf'
$smokePng = Join-Path $verifyRoot 'package-smoke.png'
$content = "0 0 0 rg`n20 20 200 300 re f`n"
$objects = @(
    '<< /Type /Catalog /Pages 2 0 R >>',
    '<< /Type /Pages /Count 1 /Kids [3 0 R] >>',
    '<< /Type /Page /Parent 2 0 R /MediaBox [0 0 288 432] /Contents 4 0 R /Resources << >> >>',
    ("<< /Length " + [Text.Encoding]::ASCII.GetByteCount($content) + " >>`nstream`n" + $content + 'endstream')
)
$pdf = "%PDF-1.4`n"
$offsets = New-Object 'System.Collections.Generic.List[int]'
for ($i = 0; $i -lt $objects.Count; $i++) {
    $offsets.Add([Text.Encoding]::ASCII.GetByteCount($pdf))
    $pdf += ($i + 1).ToString() + " 0 obj`n" + $objects[$i] + "`nendobj`n"
}
$xref = [Text.Encoding]::ASCII.GetByteCount($pdf)
$pdf += "xref`n0 5`n0000000000 65535 f `n"
foreach ($offset in $offsets) { $pdf += $offset.ToString('D10') + " 00000 n `n" }
$pdf += "trailer`n<< /Size 5 /Root 1 0 R >>`nstartxref`n$xref`n%%EOF`n"
[IO.File]::WriteAllText($smokePdf, $pdf, [Text.Encoding]::ASCII)
$pageCount = Invoke-PackedRenderer ('show "' + $smokePdf + '" trailer/Root/Pages/Count')
if ($pageCount -ne '1') { throw 'The packaged renderer could not read the generated one-page PDF.' }
$null = Invoke-PackedRenderer ('draw -q -F png -c rgb -r 300 -w 5000 -h 5000 -o "' + $smokePng + '" "' + $smokePdf + '" 1')
Add-Type -AssemblyName System.Drawing
$rendered = [Drawing.Bitmap]::FromFile($smokePng)
try {
    if ($rendered.Width -ne 1200 -or $rendered.Height -ne 1800) { throw 'The packaged renderer produced unexpected image dimensions.' }
    $center = $rendered.GetPixel(600, 900)
    if ($center.R -gt 20 -or $center.G -gt 20 -or $center.B -gt 20) { throw 'The packaged renderer lost the synthetic label content.' }
} finally { $rendered.Dispose() }

$checksum = (Get-FileHash -LiteralPath $candidateArchive -Algorithm SHA256).Hash.ToLowerInvariant()
"$checksum  $packageName.zip" | Set-Content -LiteralPath "$candidateArchive.sha256" -Encoding ascii
"$($renderer.SourceSha256)  $($renderer.SourceArchiveName)" | Set-Content -LiteralPath "$candidateSourceArchive.sha256" -Encoding ascii
@("$checksum  $packageName.zip", "$($renderer.SourceSha256)  $($renderer.SourceArchiveName)") |
    Set-Content -LiteralPath (Join-Path $stage 'SHA256SUMS.txt') -Encoding ascii

# Validation failures leave only this run's staging files. Publish the complete checked set
# afterwards; keep prior artifacts as same-volume backups until every replacement succeeds.
$publication = @(
    "$packageName.zip",
    "$packageName.zip.sha256",
    $renderer.SourceArchiveName,
    ($renderer.SourceArchiveName + '.sha256'),
    'SHA256SUMS.txt'
) | ForEach-Object {
    $destination = Join-Path $artifactRoot $_
    if (Test-Path -LiteralPath $destination -PathType Container) { throw "Artifact destination is a directory: $_" }
    [pscustomobject]@{
        Candidate = Join-Path $stage $_
        Destination = $destination
        Backup = Join-Path $stage ('previous-' + $_)
        HadPrevious = Test-Path -LiteralPath $destination -PathType Leaf
    }
}
$published = New-Object 'System.Collections.Generic.List[object]'
try {
    foreach ($item in $publication) {
        if ($item.HadPrevious) {
            [IO.File]::Replace($item.Candidate, $item.Destination, $item.Backup)
        } else {
            [IO.File]::Move($item.Candidate, $item.Destination)
        }
        $published.Add($item)
    }
} catch {
    $publicationError = $_
    for ($i = $published.Count - 1; $i -ge 0; $i--) {
        $item = $published[$i]
        if ($item.HadPrevious) {
            [IO.File]::Replace($item.Backup, $item.Destination, $item.Candidate)
        } else {
            Remove-Item -LiteralPath $item.Destination
        }
    }
    throw $publicationError
}
$archive = Join-Path $artifactRoot "$packageName.zip"
$sourceArchive = Join-Path $artifactRoot $renderer.SourceArchiveName
Write-Host 'Portable archive verified: bundled renderer starts, reads and renders a PDF after extraction.'
Write-Output $archive
Write-Output "$archive.sha256"
Write-Output $sourceArchive
