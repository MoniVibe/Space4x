param(
    [string]$TriRoot = "C:\dev\Tri",
    [string]$ConsolePath = "C:\dev\Tri\console.md",
    [int]$TailLines = 5000
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Section([string]$name) {
    Write-Host ""
    Write-Host "=== $name ==="
}

function Get-GitInfo([string]$repoPath) {
    if (-not (Test-Path $repoPath)) {
        return $null
    }

    try {
        $branch = (git -C $repoPath rev-parse --abbrev-ref HEAD).Trim()
        $head = (git -C $repoPath rev-parse --short HEAD).Trim()
        $status = (git -C $repoPath status --short --branch | Select-Object -First 1).Trim()
        $originMain = (git -C $repoPath rev-parse --short origin/main 2>$null).Trim()
        return [pscustomobject]@{
            Repo = $repoPath
            Branch = $branch
            Head = $head
            OriginMain = $originMain
            Status = $status
        }
    } catch {
        return $null
    }
}

$space4xRepo = Join-Path $TriRoot "space4x"
$puredotsRepo = Join-Path $TriRoot "puredots"
if (-not (Test-Path $puredotsRepo)) {
    $puredotsRepo = Join-Path $space4xRepo "PureDOTS"
}

Write-Section "Git Refs"
$space4x = Get-GitInfo $space4xRepo
$puredots = Get-GitInfo $puredotsRepo

if ($null -ne $space4x) {
    Write-Host "space4x repo      : $($space4x.Repo)"
    Write-Host "space4x branch    : $($space4x.Branch)"
    Write-Host "space4x head      : $($space4x.Head)"
    Write-Host "space4x origin/main: $($space4x.OriginMain)"
    Write-Host "space4x status    : $($space4x.Status)"
} else {
    Write-Host "space4x repo not found or git unavailable."
}

if ($null -ne $puredots) {
    Write-Host "puredots repo     : $($puredots.Repo)"
    Write-Host "puredots branch   : $($puredots.Branch)"
    Write-Host "puredots head     : $($puredots.Head)"
    Write-Host "puredots origin/main: $($puredots.OriginMain)"
    Write-Host "puredots status   : $($puredots.Status)"
} else {
    Write-Host "puredots repo not found or git unavailable."
}

Write-Section "Scenario Env"
$mode = [Environment]::GetEnvironmentVariable("SPACE4X_MODE")
$scenarioPathEnv = [Environment]::GetEnvironmentVariable("SPACE4X_SCENARIO_PATH")
Write-Host "SPACE4X_MODE        : $mode"
Write-Host "SPACE4X_SCENARIO_PATH: $scenarioPathEnv"
if (-not [string]::IsNullOrWhiteSpace($scenarioPathEnv)) {
    if (Test-Path $scenarioPathEnv) {
        Write-Host "Scenario path exists : YES ($scenarioPathEnv)"
    } else {
        Write-Host "Scenario path exists : NO  ($scenarioPathEnv)"
    }
}

Write-Section "Canonical Scenario Files"
$corePath = Join-Path $space4xRepo "Assets\Scenarios\space4x_fleetcrawl_core_micro.json"
$smokePath = Join-Path $space4xRepo "Assets\Scenarios\space4x_smoke.json"
Write-Host "fleetcrawl core : $(if (Test-Path $corePath) { 'present' } else { 'missing' }) ($corePath)"
Write-Host "smoke           : $(if (Test-Path $smokePath) { 'present' } else { 'missing' }) ($smokePath)"

Write-Section "Console Signal"
if (Test-Path $ConsolePath) {
    $patterns = @(
        "\[Space4XScenarioRef\]",
        "\[Space4XRunStart\]",
        "\[Space4XRunStartScenarioSelector\]",
        "\[Space4XSmokeScenarioSelector\]",
        "\[Space4XMiningScenario\]",
        "ObjectDisposedException",
        "NullReferenceException",
        "InvalidOperationException"
    )

    $consoleTail = Get-Content -Path $ConsolePath -Tail $TailLines
    $hits = @($consoleTail | Select-String -Pattern $patterns | Select-Object -Last 30)
    if ($hits.Count -eq 0) {
        Write-Host "No scenario/error signal found in $ConsolePath."
    } else {
        $hits | ForEach-Object { Write-Host $_.Line }
    }
} else {
    Write-Host "Console file not found: $ConsolePath"
}

Write-Section "Done"
Write-Host "Scenario reference check complete."
