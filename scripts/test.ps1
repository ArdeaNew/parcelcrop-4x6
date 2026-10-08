[CmdletBinding()]
param([string]$RendererPath)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
& (Join-Path $PSScriptRoot 'build.ps1')
$testRunner = Join-Path $repoRoot 'tests\ParcelCrop.Tests\bin\Release\net48\ParcelCrop.Tests.exe'
if ($RendererPath) {
    if (-not (Test-Path -LiteralPath $RendererPath -PathType Leaf)) { throw 'RendererPath must point to mutool.exe.' }
    & $testRunner --renderer (Resolve-Path -LiteralPath $RendererPath).Path
} else {
    & $testRunner
}
if ($LASTEXITCODE -ne 0) { throw 'Regression tests failed.' }
