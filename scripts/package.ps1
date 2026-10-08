[CmdletBinding()]
param([ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version)
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
& (Join-Path $PSScriptRoot 'test.ps1')
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
Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE'),(Join-Path $repoRoot 'README.md'),(Join-Path $repoRoot 'README.en.md'),(Join-Path $repoRoot 'THIRD_PARTY_NOTICES.md'),(Join-Path $repoRoot 'CHANGELOG.md'),(Join-Path $repoRoot 'CONTRIBUTING.md'),(Join-Path $repoRoot 'SECURITY.md') -Destination $packageRoot
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs') -Destination $packageRoot -Recurse
New-Item -ItemType Directory -Path (Join-Path $packageRoot 'tests') | Out-Null
Copy-Item -LiteralPath (Join-Path $repoRoot 'tests\README.md') -Destination (Join-Path $packageRoot 'tests\README.md')
$archive = Join-Path $artifactRoot "$packageName.zip"
Compress-Archive -LiteralPath $packageRoot -DestinationPath $archive -Force
$checksum = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
"$checksum  $packageName.zip" | Set-Content -LiteralPath "$archive.sha256" -Encoding ascii
Write-Output $archive
Write-Output "$archive.sha256"
