param(
    [string]$EditorRepoPath = "C:\dev\Tri\space4x_ultimate",
    [string]$CliRepoPath = "C:\dev\Tri\space4x",
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
    [switch]$AllowHeadlessProcessExit,
    [switch]$SkipParityCheck,
    [switch]$AllowCommitDrift,
    [switch]$AllowEditorVersionDrift,
    [switch]$AllowManifestDrift,
    [switch]$AllowPackagesLockDrift
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$assertScript = Join-Path $PSScriptRoot "AssertUnityLaneParity.ps1"
$runTestsScript = Join-Path $PSScriptRoot "RunUnityCliTests.ps1"

if (-not (Test-Path $runTestsScript)) {
    throw "Missing script: $runTestsScript"
}

if (-not $SkipParityCheck.IsPresent) {
    if (-not (Test-Path $assertScript)) {
        throw "Missing script: $assertScript"
    }

    $parityArgs = @{
        EditorRepoPath = $EditorRepoPath
        CliRepoPath = $CliRepoPath
    }

    if ($AllowCommitDrift.IsPresent) {
        $parityArgs.AllowCommitDrift = $true
    }
    if ($AllowEditorVersionDrift.IsPresent) {
        $parityArgs.AllowEditorVersionDrift = $true
    }
    if ($AllowManifestDrift.IsPresent) {
        $parityArgs.AllowManifestDrift = $true
    }
    if ($AllowPackagesLockDrift.IsPresent) {
        $parityArgs.AllowPackagesLockDrift = $true
    }

    & $assertScript @parityArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Lane parity gate failed. Sync your CLI lane or pass the specific drift override."
    }
}

$runArgs = @{
    RepoPath = $CliRepoPath
    TestPlatform = $TestPlatform
    TestFilter = $TestFilter
    AssemblyNames = $AssemblyNames
    TimeoutSec = $TimeoutSec
}

if (-not [string]::IsNullOrWhiteSpace($UnityExe)) {
    $runArgs.UnityExe = $UnityExe
}
if (-not [string]::IsNullOrWhiteSpace($LogPath)) {
    $runArgs.LogPath = $LogPath
}
if (-not [string]::IsNullOrWhiteSpace($ResultsPath)) {
    $runArgs.ResultsPath = $ResultsPath
}
if ($DisableAssemblyUpdater.IsPresent) {
    $runArgs.DisableAssemblyUpdater = $true
}
if ($IgnoreProjectLock.IsPresent) {
    $runArgs.IgnoreProjectLock = $true
}
if ($AllowZeroTests.IsPresent) {
    $runArgs.AllowZeroTests = $true
}
if ($AllowHeadlessProcessExit.IsPresent) {
    $runArgs.AllowHeadlessProcessExit = $true
}

& $runTestsScript @runArgs
exit $LASTEXITCODE
