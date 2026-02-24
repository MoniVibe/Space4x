param(
    [string]$PushBranch = "integration/laptop-salvage-merge-20260223",
    [string]$Space4xRepoPath = "C:\dev\Tri\space4x_ultimate",
    [string]$PuredotsRepoPath = "C:\dev\Tri\puredots_ultimate",
    [string]$LocalParityBranch = "validator/ultimate-checkout",
    [string]$LocalParityUpstreamRef = "",
    [string]$LaptopSpace4xRepoPath = "C:\dev\unity_clean\space4x_ultimate",
    [string]$LaptopPuredotsRepoPath = "C:\dev\puredots_ultimate",
    [string]$LaptopParityBranch = "validator/ultimate-checkout",
    [string]$LaptopParityUpstreamRef = "",
    [string]$LaptopHost = "25.29.69.246",
    [string]$LaptopUser = "shonh",
    [string]$LaptopKeyPath = "",
    [ValidateSet("fail", "stash-allowed")]
    [string]$DirtyPolicy = "fail",
    [string[]]$AllowedDirtyRegex = @(),
    [switch]$AllowMetaDirty
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($PushBranch)) {
    throw "PushBranch is required."
}

if ([string]::IsNullOrWhiteSpace($LocalParityUpstreamRef)) {
    $LocalParityUpstreamRef = "origin/$PushBranch"
}

if ([string]::IsNullOrWhiteSpace($LaptopParityUpstreamRef)) {
    $LaptopParityUpstreamRef = $LocalParityUpstreamRef
}

$syncScript = Join-Path $PSScriptRoot "PushValidationAndSyncParity.ps1"
if (-not (Test-Path $syncScript)) {
    throw "Missing script: $syncScript"
}

function Invoke-BundleSync {
    param(
        [string]$Label,
        [string]$RepoPath,
        [string]$LaptopRepoPath
    )

    Write-Host ("[BundleSync] start label={0} repo={1} laptop={2} ref={3}" -f $Label, $RepoPath, $LaptopRepoPath, $LocalParityUpstreamRef)

    $args = @{
        RepoPath = $RepoPath
        Mode = "iterator"
        SkipPush = $true
        LocalParityBranch = $LocalParityBranch
        LocalParityUpstreamRef = $LocalParityUpstreamRef
        LaptopRepoPath = $LaptopRepoPath
        LaptopParityBranch = $LaptopParityBranch
        LaptopParityUpstreamRef = $LaptopParityUpstreamRef
        LaptopHost = $LaptopHost
        LaptopUser = $LaptopUser
        DirtyPolicy = $DirtyPolicy
        AllowedDirtyRegex = $AllowedDirtyRegex
    }

    if (-not [string]::IsNullOrWhiteSpace($LaptopKeyPath)) {
        $args.LaptopKeyPath = $LaptopKeyPath
    }

    if ($AllowMetaDirty.IsPresent) {
        $args.AllowMetaDirty = $true
    }

    & $syncScript @args
    if ($LASTEXITCODE -ne 0) {
        throw "[BundleSync] failed for $Label (exit=$LASTEXITCODE)."
    }

    Write-Host ("[BundleSync] done label={0}" -f $Label)
}

Invoke-BundleSync -Label "space4x_ultimate" -RepoPath $Space4xRepoPath -LaptopRepoPath $LaptopSpace4xRepoPath
Invoke-BundleSync -Label "puredots_ultimate" -RepoPath $PuredotsRepoPath -LaptopRepoPath $LaptopPuredotsRepoPath

Write-Host ("[BundleSync] complete push_branch={0} local_ref={1} laptop_ref={2}" -f $PushBranch, $LocalParityUpstreamRef, $LaptopParityUpstreamRef)
