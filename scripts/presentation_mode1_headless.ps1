param(
    [string]$UnityExe = "",
    [string]$ProjectPath = "",
    [string]$OutDir = "",
    [string]$TestFilter = "Space4X.Tests.PlayMode.Space4XMode1CameraFollowContractTests",
    [int]$TimeoutSec = 900,
    [switch]$AllowRunningUnity
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

function Resolve-UnityExe {
    param([string]$Requested)

    $candidates = @()
    if (-not [string]::IsNullOrWhiteSpace($Requested)) { $candidates += $Requested }
    if (-not [string]::IsNullOrWhiteSpace($env:TRI_UNITY_EXE)) { $candidates += $env:TRI_UNITY_EXE }
    if (-not [string]::IsNullOrWhiteSpace($env:UNITY_EXE)) { $candidates += $env:UNITY_EXE }
    if (-not [string]::IsNullOrWhiteSpace($env:UNITY_WIN)) { $candidates += $env:UNITY_WIN }
    $candidates += "C:\Program Files\Unity\Hub\Editor\6000.3.1f1\Editor\Unity.exe"

    foreach ($candidate in $candidates) {
        if ([string]::IsNullOrWhiteSpace($candidate)) { continue }
        if (Test-Path -LiteralPath $candidate) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }
    return ""
}

function Parse-TestResults {
    param([string]$Path)

    $summary = [ordered]@{
        found = $false
        total = 0
        passed = 0
        failed = 0
        inconclusive = 0
        skipped = 0
        result = ""
    }

    if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path -LiteralPath $Path)) {
        return $summary
    }

    try {
        [xml]$xml = Get-Content -LiteralPath $Path -Raw
        $run = $xml."test-run"
        if ($null -eq $run) {
            return $summary
        }

        $summary.found = $true
        $summary.total = [int]$run.total
        $summary.passed = [int]$run.passed
        $summary.failed = [int]$run.failed
        $summary.inconclusive = [int]$run.inconclusive
        $summary.skipped = [int]$run.skipped
        $summary.result = [string]$run.result
    } catch {
        $summary.found = $false
    }

    return $summary
}

function Get-RunningUnityInstances {
    $rows = @()
    try {
        $rows = @(Get-Process -Name Unity -ErrorAction SilentlyContinue)
    } catch {
        $rows = @()
    }
    return $rows
}

$repoRoot = Resolve-RepoRoot
if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $ProjectPath = $repoRoot
}
if ([string]::IsNullOrWhiteSpace($OutDir)) {
    $stamp = (Get-Date).ToUniversalTime().ToString("yyyyMMddTHHmmssfffZ")
    $OutDir = Join-Path $repoRoot ("reports\presentation_mode1_headless_{0}" -f $stamp)
}
Ensure-Directory -Path $OutDir

$unityExePath = Resolve-UnityExe -Requested $UnityExe
if ([string]::IsNullOrWhiteSpace($unityExePath)) {
    $missing = [ordered]@{
        ok = $false
        status = "error"
        reason = "unity_exe_missing"
        recommendation = "Set TRI_UNITY_EXE (or UNITY_EXE/UNITY_WIN) to Unity.exe and rerun."
    }
    $missingJson = $missing | ConvertTo-Json -Depth 8
    $missingJson
    exit 5
}

$runningUnity = Get-RunningUnityInstances
if (-not $AllowRunningUnity -and $runningUnity.Count -gt 0) {
    $blocked = [ordered]@{
        ok = $false
        status = "blocked"
        reason = "unity_already_running"
        recommendation = "Close Unity Editor instances (or use a clean runner), then rerun. Override with -AllowRunningUnity only if you know this lane is isolated."
        running_unity_count = $runningUnity.Count
    }
    $blockedJson = $blocked | ConvertTo-Json -Depth 8
    $blockedJson
    exit 6
}

$testResultsPath = Join-Path $OutDir "mode1_playmode_results.xml"
$unityLogPath = Join-Path $OutDir "mode1_playmode_editor.log"
$probePath = Join-Path $OutDir "mode1_camera_probe.jsonl"
$envelopePath = Join-Path $OutDir "mode1_headless_envelope.json"

if (Test-Path -LiteralPath $probePath) {
    Remove-Item -LiteralPath $probePath -Force -ErrorAction SilentlyContinue
}

$priorProbeEnabled = $env:SPACE4X_CAMERA_PROBE
$priorProbeOut = $env:SPACE4X_CAMERA_PROBE_OUT
$env:SPACE4X_CAMERA_PROBE = "1"
$env:SPACE4X_CAMERA_PROBE_OUT = $probePath

$unityArgs = @(
    "-batchmode",
    "-nographics",
    "-quit",
    "-projectPath", $ProjectPath,
    "-runTests",
    "-testPlatform", "playmode",
    "-testFilter", $TestFilter,
    "-testResults", $testResultsPath,
    "-logFile", $unityLogPath
)

$timedOut = $false
$unityExitCode = -1
try {
    $proc = Start-Process -FilePath $unityExePath -ArgumentList $unityArgs -PassThru -WindowStyle Hidden
    try {
        Wait-Process -Id $proc.Id -Timeout $TimeoutSec -ErrorAction Stop
    } catch {
        $timedOut = $true
        Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
    }

    $proc.Refresh()
    if (-not $timedOut) {
        $unityExitCode = $proc.ExitCode
    } else {
        $unityExitCode = 124
    }
} finally {
    $env:SPACE4X_CAMERA_PROBE = $priorProbeEnabled
    $env:SPACE4X_CAMERA_PROBE_OUT = $priorProbeOut
}

$testSummary = Parse-TestResults -Path $testResultsPath
$testFailed = $false
if ($timedOut) {
    $testFailed = $true
} elseif ($unityExitCode -ne 0) {
    $testFailed = $true
} elseif (-not $testSummary.found) {
    $testFailed = $true
} elseif ($testSummary.found -and $testSummary.failed -gt 0) {
    $testFailed = $true
}

$mode1Script = Join-Path $PSScriptRoot "presentation_mode1_check.ps1"
$mode1Raw = $null
$mode1Exit = 0
$mode1 = $null
if (Test-Path -LiteralPath $mode1Script) {
    $mode1Raw = & $mode1Script -ProbePath $probePath -OutDir $OutDir
    $mode1Exit = $LASTEXITCODE
    try {
        $mode1 = $mode1Raw | ConvertFrom-Json -AsHashtable -Depth 100
    } catch {
        $mode1 = $null
    }
}

$status = "pass"
$ok = $true
$recommendation = "Mode 1 headless contract passed."
$exitCode = 0

if ($timedOut) {
    $status = "error"
    $ok = $false
    $recommendation = "Unity timed out; inspect mode1_playmode_editor.log and rerun."
    $exitCode = 5
}
elseif ($testFailed) {
    $status = "fail"
    $ok = $false
    $recommendation = "PlayMode contract test failed; inspect test XML/log, then rerun."
    $exitCode = 2
}
elseif ($null -eq $mode1) {
    $status = "error"
    $ok = $false
    $recommendation = "Mode1 checker output could not be parsed."
    $exitCode = 5
}
else {
    $mode1Status = [string]$mode1.mode1_status
    if ($mode1Status -eq "fail") {
        $status = "fail"
        $ok = $false
        $recommendation = [string]$mode1.recommendation
        $exitCode = 2
    }
    elseif ($mode1Status -eq "insufficient_evidence") {
        $status = "insufficient_evidence"
        $ok = $false
        $recommendation = [string]$mode1.recommendation
        $exitCode = 4
    }
    elseif ($mode1Status -eq "warn") {
        $status = "warn"
        $ok = $true
        $recommendation = [string]$mode1.recommendation
        $exitCode = 0
    }
}

$envelope = [ordered]@{
    ok = $ok
    status = $status
    recommendation = $recommendation
    unity_exe = $unityExePath
    project_path = $ProjectPath
    test_filter = $TestFilter
    timed_out = $timedOut
    unity_exit_code = $unityExitCode
    test_results = $testSummary
    probe_path = $probePath
    mode1 = $mode1
    artifacts = [ordered]@{
        envelope_json = $envelopePath
        playmode_results_xml = $testResultsPath
        unity_log = $unityLogPath
    }
}

$json = $envelope | ConvertTo-Json -Depth 10
Set-Content -LiteralPath $envelopePath -Value $json -Encoding utf8
$json
exit $exitCode
