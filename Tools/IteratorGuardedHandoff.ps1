param(
    [string]$RepoPath = "C:\dev\Tri\space4x",
    [string]$PuredotsRepoPath = "C:\dev\Tri\puredots",
    [string]$Remote = "origin",
    [string]$PushBranch = "",
    [string]$UnityExe = "",
    [string]$AwarenessNote = "",
    [string]$GuardReportPath = "",
    [string]$CompileLogPath = "",
    [string]$LocalParityBranch = "validator/ultimate-checkout",
    [string]$LocalParityUpstreamRef = "",
    [string]$LaptopHost = "25.29.69.246",
    [string]$LaptopUser = "shonh",
    [string]$LaptopRepoPath = "C:\dev\unity_clean\space4x",
    [string]$LaptopParityBranch = "validator/ultimate-checkout",
    [string]$LaptopParityUpstreamRef = "",
    [string]$LaptopKeyPath = "",
    [ValidateSet("fail", "stash-allowed")]
    [string]$DirtyPolicy = "fail",
    [string[]]$AllowedDirtyRegex = @(),
    [switch]$AllowMetaDirty,
    [switch]$SkipCompilePreflight,
    [switch]$SkipPrAwareness,
    [switch]$AllowDirty,
    [switch]$SkipLocalParity,
    [switch]$SkipLaptopParity
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Test-Path $RepoPath)) {
    throw "Repo path not found: $RepoPath"
}

$guardScript = Join-Path $RepoPath "Tools\IteratorBedrockGuard.ps1"
$syncScript = Join-Path $RepoPath "Tools\PushValidationAndSyncParity.ps1"

if (-not (Test-Path $guardScript)) {
    throw "Missing guard script: $guardScript"
}
if (-not (Test-Path $syncScript)) {
    throw "Missing parity sync script: $syncScript"
}

Push-Location $RepoPath
try {
    if ([string]::IsNullOrWhiteSpace($PushBranch)) {
        $PushBranch = (git rev-parse --abbrev-ref HEAD).Trim()
    }
}
finally {
    Pop-Location
}

$guardArgs = @{
    RepoPath = $RepoPath
    PuredotsRepoPath = $PuredotsRepoPath
    Remote = $Remote
    AwarenessNote = $AwarenessNote
    ReportPath = $GuardReportPath
    CompileLogPath = $CompileLogPath
}
if (-not [string]::IsNullOrWhiteSpace($UnityExe)) {
    $guardArgs.UnityExe = $UnityExe
}
if ($SkipCompilePreflight.IsPresent) {
    $guardArgs.SkipCompilePreflight = $true
}
if ($SkipPrAwareness.IsPresent) {
    $guardArgs.SkipPrAwareness = $true
}
if ($AllowDirty.IsPresent) {
    $guardArgs.AllowDirty = $true
}

& $guardScript @guardArgs
$guardExit = $LASTEXITCODE
if ($guardExit -ne 0) {
    Write-Host ("[IteratorGuardedHandoff] guard failed (exit={0}). Push/sync aborted." -f $guardExit)
    exit $guardExit
}

$syncArgs = @{
    RepoPath = $RepoPath
    Mode = "iterator"
    Remote = $Remote
    PushBranch = $PushBranch
    LocalParityBranch = $LocalParityBranch
    LocalParityUpstreamRef = $LocalParityUpstreamRef
    LaptopHost = $LaptopHost
    LaptopUser = $LaptopUser
    LaptopRepoPath = $LaptopRepoPath
    LaptopParityBranch = $LaptopParityBranch
    LaptopParityUpstreamRef = $LaptopParityUpstreamRef
    LaptopKeyPath = $LaptopKeyPath
    DirtyPolicy = $DirtyPolicy
    AllowedDirtyRegex = $AllowedDirtyRegex
}
if ($AllowMetaDirty.IsPresent) {
    $syncArgs.AllowMetaDirty = $true
}
if ($SkipLocalParity.IsPresent) {
    $syncArgs.SkipLocalParity = $true
}
if ($SkipLaptopParity.IsPresent) {
    $syncArgs.SkipLaptopParity = $true
}

& $syncScript @syncArgs
exit $LASTEXITCODE
