param(
    [string]$ProbePath = "",
    [string]$RunSummaryPath = "",
    [string]$InvariantsPath = "",
    [string]$OutDir = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-RepoRoot {
    $scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
    $resolved = Resolve-Path (Join-Path $scriptDir "..")
    if ($resolved -is [string]) { return $resolved }
    return @($resolved)[0].Path
}

function Ensure-Directory {
    param([string]$Path)
    if ([string]::IsNullOrWhiteSpace($Path)) { return }
    if (-not (Test-Path -LiteralPath $Path)) {
        New-Item -ItemType Directory -Path $Path -Force | Out-Null
    }
}

function Find-LatestByPattern {
    param(
        [string[]]$Roots,
        [string[]]$Patterns
    )

    $hits = @()
    foreach ($root in $Roots) {
        if ([string]::IsNullOrWhiteSpace($root)) { continue }
        if (-not (Test-Path -LiteralPath $root)) { continue }
        foreach ($pattern in $Patterns) {
            try {
                $hits += Get-ChildItem -Path $root -Recurse -File -Filter $pattern -ErrorAction SilentlyContinue
            } catch {}
        }
    }
    if (-not $hits -or $hits.Count -eq 0) { return $null }
    return ($hits | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
}

$repoRoot = Resolve-RepoRoot
$triRoot = Split-Path -Parent $repoRoot
$defaultRoots = @(
    (Join-Path $repoRoot "reports"),
    (Join-Path $triRoot "reports")
)

if ([string]::IsNullOrWhiteSpace($OutDir)) {
    $OutDir = Join-Path $repoRoot "reports"
}
Ensure-Directory -Path $OutDir

if ([string]::IsNullOrWhiteSpace($ProbePath)) {
    $ProbePath = Find-LatestByPattern -Roots $defaultRoots -Patterns @("*camera*probe*.jsonl")
}

if ([string]::IsNullOrWhiteSpace($ProbePath) -or -not (Test-Path -LiteralPath $ProbePath)) {
    $missing = [ordered]@{
        ok = $false
        mode1_status = "insufficient_evidence"
        reason = "camera_probe_missing"
        recommendation = "Generate probe evidence first: set SPACE4X_CAMERA_PROBE=1, run mode cycle (1->2->3->1), then rerun this check."
        expected_probe_env = @{
            SPACE4X_CAMERA_PROBE = "1"
            SPACE4X_CAMERA_PROBE_OUT = "C:\\dev\\Tri\\space4x\\reports\\camera_mode1_probe.jsonl"
        }
        searched_roots = $defaultRoots
    }
    $missingJson = $missing | ConvertTo-Json -Depth 8
    $missingJson
    exit 4
}

if ([string]::IsNullOrWhiteSpace($RunSummaryPath)) {
    $RunSummaryPath = Find-LatestByPattern -Roots $defaultRoots -Patterns @("run_summary.json")
}

if ([string]::IsNullOrWhiteSpace($InvariantsPath)) {
    if (-not [string]::IsNullOrWhiteSpace($RunSummaryPath)) {
        $candidate = Join-Path (Split-Path -Parent $RunSummaryPath) "invariants.json"
        if (Test-Path -LiteralPath $candidate) {
            $InvariantsPath = $candidate
        }
    }
    if ([string]::IsNullOrWhiteSpace($InvariantsPath)) {
        $InvariantsPath = Find-LatestByPattern -Roots $defaultRoots -Patterns @("invariants.json")
    }
}

$stamp = (Get-Date).ToUniversalTime().ToString("yyyyMMddTHHmmssfffZ")
$jsonOut = Join-Path $OutDir ("mode1_nuisance_{0}.json" -f $stamp)
$mdOut = Join-Path $OutDir ("mode1_nuisance_{0}.md" -f $stamp)
$filterScript = Join-Path $PSScriptRoot "presentation_nuisance_filter.ps1"
if (-not (Test-Path -LiteralPath $filterScript)) {
    throw "presentation_nuisance_filter.ps1 not found at $filterScript"
}

$args = @(
    "-NoProfile",
    "-File", $filterScript,
    "-CameraProbePath", $ProbePath,
    "-OutJsonPath", $jsonOut,
    "-OutMarkdownPath", $mdOut
)
if (-not [string]::IsNullOrWhiteSpace($RunSummaryPath)) {
    $args += @("-RunSummaryPath", $RunSummaryPath)
}
if (-not [string]::IsNullOrWhiteSpace($InvariantsPath)) {
    $args += @("-InvariantsPath", $InvariantsPath)
}

$raw = & pwsh @args
if ($LASTEXITCODE -ne 0) {
    throw "presentation_nuisance_filter.ps1 failed with exit code $LASTEXITCODE"
}

$result = $raw | ConvertFrom-Json -AsHashtable -Depth 100
$sampleCount = 0
if ($result.Contains("camera") -and $result.camera -and $result.camera.Contains("sample_count")) {
    $sampleCount = [int]$result.camera.sample_count
}

$mode1Status = "pass"
$ok = $true
$recommendation = "Mode 1 nuisance checks are within thresholds."
$exitCode = 0
if ($sampleCount -lt 20) {
    $mode1Status = "insufficient_evidence"
    $ok = $false
    $recommendation = "Camera probe sample count is too low; capture a longer 1->2->3->1 repro and rerun."
    $exitCode = 4
}
elseif ([string]$result.verdict -eq "red") {
    $mode1Status = "fail"
    $ok = $false
    $recommendation = "Fix Tier 1 camera issues before manual feel pass."
    $exitCode = 2
}
elseif ([string]$result.verdict -eq "yellow") {
    $mode1Status = "warn"
    $ok = $true
    $recommendation = "Proceed with targeted visual review for warned camera/movement areas."
    $exitCode = 0
}

$envelope = [ordered]@{
    ok = $ok
    mode1_status = $mode1Status
    filter_verdict = [string]$result.verdict
    recommendation = $recommendation
    probe_path = $ProbePath
    run_summary_path = $RunSummaryPath
    invariants_path = $InvariantsPath
    output_json_path = $jsonOut
    output_markdown_path = $mdOut
}

$envelopeJson = $envelope | ConvertTo-Json -Depth 8
$envelopeJson
exit $exitCode
