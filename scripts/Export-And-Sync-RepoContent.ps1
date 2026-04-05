param(
    [string]$UnityExe,
    [string]$UnityProjectPath,
    [string]$WorkspaceRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$ProfileRoot
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($UnityExe)) {
    throw "Provide -UnityExe with the full path to Unity.exe."
}

if ([string]::IsNullOrWhiteSpace($UnityProjectPath)) {
    throw "Provide -UnityProjectPath with the full path to the Unity content project."
}

if ([string]::IsNullOrWhiteSpace($ProfileRoot)) {
    throw "Provide -ProfileRoot with the full path to the target Thunderstore content plugin folder."
}

$syncScript = Join-Path $WorkspaceRoot "scripts\\Sync-RepoContentProfile.ps1"

if (-not (Test-Path -LiteralPath $UnityExe)) {
    throw "Unity executable not found: $UnityExe"
}

if (-not (Test-Path -LiteralPath $UnityProjectPath)) {
    throw "Unity project not found: $UnityProjectPath"
}

if (-not (Test-Path -LiteralPath $syncScript)) {
    throw "Sync script not found: $syncScript"
}

Write-Host "Exporting REPOLib content package..." -ForegroundColor Cyan
& $UnityExe `
    -batchmode `
    -quit `
    -projectPath $UnityProjectPath `
    -executeMethod RepoDeltaForceModExportBridge.ExportDefault `
    -logFile -

if ($LASTEXITCODE -ne 0) {
    throw "Unity batch export failed with exit code $LASTEXITCODE"
}

Write-Host ""
Write-Host "Syncing exported package into Thunderstore profile..." -ForegroundColor Cyan
& $syncScript -WorkspaceRoot $WorkspaceRoot -ProfileRoot $ProfileRoot

if ($LASTEXITCODE -ne 0) {
    throw "Profile sync failed with exit code $LASTEXITCODE"
}

Write-Host ""
Write-Host "Export and sync completed." -ForegroundColor Green
