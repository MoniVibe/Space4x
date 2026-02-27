param(
    [string]$RepoPath = "C:\dev\Tri\space4x",
    [string]$UnityExe = "",
    [ValidateSet("PlayMode", "EditMode")]
    [string]$TestPlatform = "PlayMode",
    [string]$TestFilter = "",
    [string]$AssemblyNames = "",
    [string]$LogPath = "",
    [string]$ResultsPath = "",
    [int]$TimeoutSec = 1800,
    [switch]$DisableAssemblyUpdater,
    [switch]$IgnoreProjectLock,
    [switch]$AllowZeroTests,
    [switch]$AllowHeadlessProcessExit
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Test-UnityProcessUsingRepoPath {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return $false
    }

    $normalized = [System.IO.Path]::GetFullPath($Path).TrimEnd('\').ToLowerInvariant()
    try {
        $unityProcesses = Get-CimInstance -ClassName Win32_Process -Filter "Name = 'Unity.exe'" -ErrorAction Stop
    }
    catch {
        return $false
    }

    foreach ($proc in $unityProcesses) {
        $cmd = [string]$proc.CommandLine
        if ([string]::IsNullOrWhiteSpace($cmd)) {
            continue
        }

        if ($cmd.ToLowerInvariant().Contains($normalized)) {
            return $true
        }
    }

    return $false
}

if (-not (Test-Path $RepoPath)) {
    throw "Repo path not found: $RepoPath"
}

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
if ([string]::IsNullOrWhiteSpace($LogPath)) {
    $LogPath = Join-Path $RepoPath ("Temp\unity_cli_tests_{0}_{1}.log" -f $TestPlatform.ToLowerInvariant(), $timestamp)
}
if ([string]::IsNullOrWhiteSpace($ResultsPath)) {
    $ResultsPath = Join-Path $RepoPath ("Temp\unity_cli_tests_{0}_{1}.xml" -f $TestPlatform.ToLowerInvariant(), $timestamp)
}

$logDir = Split-Path -Parent $LogPath
if (-not (Test-Path $logDir)) {
    New-Item -ItemType Directory -Path $logDir -Force | Out-Null
}

$resultsDir = Split-Path -Parent $ResultsPath
if (-not (Test-Path $resultsDir)) {
    New-Item -ItemType Directory -Path $resultsDir -Force | Out-Null
}

$lockPath = Join-Path $RepoPath "Temp\UnityLockfile"
if ((Test-Path $lockPath) -and -not $IgnoreProjectLock.IsPresent) {
    if (Test-UnityProcessUsingRepoPath -Path $RepoPath) {
        throw "Unity lock detected at '$lockPath'. Use a separate CLI lane repo path or pass -IgnoreProjectLock."
    }

    Write-Host "[RunUnityCliTests] WARN stale UnityLockfile detected with no matching Unity.exe process: $lockPath"
}

if ([string]::IsNullOrWhiteSpace($UnityExe)) {
    $resolverScript = Join-Path $PSScriptRoot "ResolveUnityEditor.ps1"
    if (-not (Test-Path $resolverScript)) {
        throw "Missing Unity resolver script: $resolverScript"
    }

    $resolved = & $resolverScript -RepoPath $RepoPath -Quiet
    if ($LASTEXITCODE -ne 0) {
        throw "Unity resolver failed with exit code $LASTEXITCODE."
    }

    $resolvedLines = @($resolved | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($resolvedLines.Count -eq 0) {
        throw "Unity resolver returned no path."
    }

    $UnityExe = ($resolvedLines | Select-Object -Last 1).Trim()
}

if (-not (Test-Path $UnityExe)) {
    throw "Unity executable not found: $UnityExe"
}

Write-Host "[RunUnityCliTests] repo=$RepoPath"
Write-Host "[RunUnityCliTests] unity=$UnityExe"
Write-Host "[RunUnityCliTests] platform=$TestPlatform"
Write-Host "[RunUnityCliTests] filter=$TestFilter"
Write-Host "[RunUnityCliTests] assemblies=$AssemblyNames"
Write-Host "[RunUnityCliTests] log=$LogPath"
Write-Host "[RunUnityCliTests] results=$ResultsPath"

$effectiveFilter = $TestFilter
if (-not [string]::IsNullOrWhiteSpace($TestFilter)) {
    $regexMetaChars = @('^', '$', '*', '+', '?', '[', ']', '(', ')', '|', '\', '.')
    $looksRegex = $false
    foreach ($c in $regexMetaChars) {
        if ($TestFilter.Contains($c)) {
            $looksRegex = $true
            break
        }
    }

    if (-not $looksRegex) {
        $effectiveFilter = ".*" + [regex]::Escape($TestFilter) + ".*"
    }
}

if (-not [string]::IsNullOrWhiteSpace($effectiveFilter)) {
    Write-Host "[RunUnityCliTests] effective_filter=$effectiveFilter"
}

$arguments = @(
    "-batchmode",
    "-nographics",
    "-projectPath", $RepoPath,
    "-runTests",
    "-testPlatform", $TestPlatform,
    "-logFile", $LogPath,
    "-testResults", $ResultsPath
)

if (-not [string]::IsNullOrWhiteSpace($effectiveFilter)) {
    $arguments += @("-testFilter", $effectiveFilter)
}

if (-not [string]::IsNullOrWhiteSpace($AssemblyNames)) {
    $arguments += @("-assemblyNames", $AssemblyNames)
}

if ($DisableAssemblyUpdater.IsPresent) {
    $arguments += "-disable-assembly-updater"
}

$headlessSafeMode = $TestPlatform.Equals("PlayMode", [System.StringComparison]::OrdinalIgnoreCase) -and -not $AllowHeadlessProcessExit.IsPresent
$headlessEnvOverrides = @{}
if ($headlessSafeMode) {
    # Keep PlayMode runs alive long enough for Unity Test Runner to emit XML.
    $headlessEnvOverrides["PUREDOTS_EXIT_POLICY"] = "nevernonzero"
    $headlessEnvOverrides["PUREDOTS_HEADLESS_EXIT_ON_RESULT"] = "0"
    $headlessEnvOverrides["PUREDOTS_HEADLESS_EXIT_IMMEDIATE"] = "0"
    $headlessEnvOverrides["PUREDOTS_HEADLESS_TIME_PROOF_EXIT"] = "0"
    $headlessEnvOverrides["PUREDOTS_HEADLESS_REWIND_PROOF_EXIT"] = "0"
}

if ($headlessEnvOverrides.Count -gt 0) {
    $pairs = $headlessEnvOverrides.GetEnumerator() | Sort-Object Name | ForEach-Object { "{0}={1}" -f $_.Key, $_.Value }
    Write-Host ("[RunUnityCliTests] headless_safe_env={0}" -f ($pairs -join ";"))
}

$envBackup = @{}
foreach ($pair in $headlessEnvOverrides.GetEnumerator()) {
    $name = [string]$pair.Key
    $envBackup[$name] = [System.Environment]::GetEnvironmentVariable($name, "Process")
    [System.Environment]::SetEnvironmentVariable($name, [string]$pair.Value, "Process")
}

$process = $null
try {
    $process = Start-Process -FilePath $UnityExe -ArgumentList $arguments -PassThru -NoNewWindow
}
finally {
    foreach ($pair in $envBackup.GetEnumerator()) {
        [System.Environment]::SetEnvironmentVariable([string]$pair.Key, $pair.Value, "Process")
    }
}

$timedOut = $false
if (-not $process.WaitForExit($TimeoutSec * 1000)) {
    $timedOut = $true
    Write-Host "[RunUnityCliTests] WARN process timeout after $TimeoutSec seconds."
    try {
        if (-not $process.HasExited) {
            $process.Kill()
        }
    }
    catch {
    }
    Start-Sleep -Milliseconds 300
}

if (-not (Test-Path $LogPath)) {
    throw "Unity test run did not produce log: $LogPath"
}

$logLines = @(Get-Content -Path $LogPath)
$compileMarkers = @(
    $logLines | Select-String -Pattern "error\s+CS\d+"
    $logLines | Select-String -Pattern ":\s*error\s"
    $logLines | Select-String -Pattern "Compilation failed"
    $logLines | Select-String -Pattern "Scripts have compiler errors"
)
$compileMarkers = @($compileMarkers | Where-Object { $_ -ne $null } | Select-Object -Unique)

if ($compileMarkers.Count -gt 0) {
    Write-Host "[RunUnityCliTests] FAIL compile errors detected."
    foreach ($line in ($compileMarkers | Select-Object -First 25)) {
        Write-Host $line.Line
    }
    exit 2
}

if (-not (Test-Path $ResultsPath)) {
    Write-Host "[RunUnityCliTests] FAIL results file not found."
    if ($timedOut) {
        Write-Host "[RunUnityCliTests] hint: process timed out before results were written."
    }
    else {
        Write-Host "[RunUnityCliTests] hint: first-time import may have consumed the batch lane without running tests."
    }
    Write-Host "[RunUnityCliTests] log_tail:"
    foreach ($line in ($logLines | Select-Object -Last 30)) {
        Write-Host $line
    }
    exit 3
}

[xml]$resultsXml = Get-Content -Path $ResultsPath
$root = $resultsXml.SelectSingleNode("/test-run")
if ($null -eq $root) {
    throw "Unexpected test results format in '$ResultsPath' (missing /test-run)."
}

$result = $root.GetAttribute("result")
$total = [int]($root.GetAttribute("total"))
$passed = [int]($root.GetAttribute("passed"))
$failed = [int]($root.GetAttribute("failed"))
$skipped = [int]($root.GetAttribute("skipped"))

Write-Host ("[RunUnityCliTests] summary result={0} total={1} passed={2} failed={3} skipped={4}" -f $result, $total, $passed, $failed, $skipped)
if ($timedOut) {
    Write-Host "[RunUnityCliTests] WARN process timed out but parsed persisted test results."
}

if ($total -eq 0 -and -not $AllowZeroTests.IsPresent) {
    Write-Host "[RunUnityCliTests] FAIL zero tests executed."
    exit 4
}

if ($failed -gt 0 -or -not $result.Equals("Passed", [System.StringComparison]::OrdinalIgnoreCase)) {
    Write-Host "[RunUnityCliTests] FAIL test failures detected."
    exit 1
}

Write-Host "[RunUnityCliTests] PASS"
exit 0
