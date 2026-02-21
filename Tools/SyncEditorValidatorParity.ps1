param(
    [string]$RepoPath = "C:\dev\Tri\space4x",
    [string]$TargetBranch = "validator/ultimate-checkout",
    [string]$UpstreamRef = "origin/main"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Test-Path $RepoPath)) {
    throw "Repo path not found: $RepoPath"
}

Push-Location $RepoPath
try {
    $status = git status --porcelain
    if (-not [string]::IsNullOrWhiteSpace($status)) {
        throw "Working tree is not clean. Commit/stash first."
    }

    git fetch --all --prune

    $branchExists = (git branch --list $TargetBranch)
    if ([string]::IsNullOrWhiteSpace($branchExists)) {
        git switch -c $TargetBranch $UpstreamRef
    } else {
        git switch $TargetBranch
        git merge --ff-only $UpstreamRef
    }

    $head = (git rev-parse --short HEAD).Trim()
    $upstreamHead = (git rev-parse --short $UpstreamRef).Trim()
    Write-Host "Parity sync complete."
    Write-Host "branch   : $TargetBranch"
    Write-Host "head     : $head"
    Write-Host "upstream : $UpstreamRef ($upstreamHead)"
}
finally {
    Pop-Location
}
