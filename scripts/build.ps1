[CmdletBinding()]
param([ValidateSet('Debug', 'Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
dotnet build (Join-Path $repoRoot 'ParcelCrop.sln') --configuration $Configuration
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
