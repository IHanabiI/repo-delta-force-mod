param(
    [string]$WorkspaceRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$ProfileRoot
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ProfileRoot)) {
    throw "Provide -ProfileRoot with the full path to the target Thunderstore content plugin folder."
}

$sourceBundle = Join-Path $WorkspaceRoot "repolib-export\\package\\RepoDeltaForceMod.repobundle"
$sourceManifest = Join-Path $WorkspaceRoot "repolib-export\\package\\manifest.json"
$targetBundle = Join-Path $ProfileRoot "RepoDeltaForceMod.repobundle"
$targetManifest = Join-Path $ProfileRoot "manifest.json"

if (-not (Test-Path -LiteralPath $sourceBundle)) {
    throw "Source bundle not found: $sourceBundle"
}

if (-not (Test-Path -LiteralPath $sourceManifest)) {
    throw "Source manifest not found: $sourceManifest"
}

if (-not (Test-Path -LiteralPath $ProfileRoot)) {
    throw "Thunderstore content profile folder not found: $ProfileRoot"
}

function Get-FileSummary {
    param([string]$Path)

    $item = Get-Item -LiteralPath $Path
    $hash = Get-FileHash -LiteralPath $Path -Algorithm SHA256

    [pscustomobject]@{
        Path = $item.FullName
        Length = $item.Length
        LastWriteTime = $item.LastWriteTime
        SHA256 = $hash.Hash
    }
}

$before = @()
if (Test-Path -LiteralPath $targetBundle) {
    $before += Get-FileSummary -Path $targetBundle
}
if (Test-Path -LiteralPath $targetManifest) {
    $before += Get-FileSummary -Path $targetManifest
}

Copy-Item -LiteralPath $sourceBundle -Destination $targetBundle -Force
Copy-Item -LiteralPath $sourceManifest -Destination $targetManifest -Force

$after = @(
    Get-FileSummary -Path $targetBundle
    Get-FileSummary -Path $targetManifest
)

Write-Host "Synced REPOLib content package to Thunderstore profile." -ForegroundColor Green

if ($before.Count -gt 0) {
    Write-Host ""
    Write-Host "Previous target files:" -ForegroundColor Yellow
    $before | Format-Table -AutoSize
}

Write-Host ""
Write-Host "Current target files:" -ForegroundColor Cyan
$after | Format-Table -AutoSize
