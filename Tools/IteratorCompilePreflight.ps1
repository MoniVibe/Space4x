param(
    [string]$UnityExe = "",
    [string]$RepoPath = "C:\dev\Tri\space4x_ultimate",
    [string]$LogPath = "",
    [int]$TimeoutSec = 360,
    [switch]$DisableAssemblyUpdater
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($UnityExe)) {
    $resolverScript = Join-Path $PSScriptRoot "ResolveUnityEditor.ps1"
    if (-not (Test-Path $resolverScript)) {
        throw "Missing Unity resolver script: $resolverScript"
    }

    try {
        $resolved = & $resolverScript -RepoPath $RepoPath -Quiet
        if ($LASTEXITCODE -ne 0) {
            throw "Resolver exited with code $LASTEXITCODE."
        }

        $resolvedLines = @($resolved | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
        if ($resolvedLines.Count -gt 0) {
            $UnityExe = ($resolvedLines | Select-Object -Last 1).Trim()
        }
    }
    catch {
        throw "Missing Unity path and auto-resolution failed. Pass -UnityExe <path-to-Unity.exe> or set UNITY_EXE. Details: $($_.Exception.Message)"
    }
}

if ([string]::IsNullOrWhiteSpace($UnityExe)) {
    throw "Missing Unity path. Pass -UnityExe <path-to-Unity.exe>, set UNITY_EXE, or ensure ProjectSettings/ProjectVersion.txt points to an installed editor."
}

if (-not (Test-Path $UnityExe)) {
    throw "Unity executable not found: $UnityExe"
}

if (-not (Test-Path $RepoPath)) {
    throw "Repo path not found: $RepoPath"
}

if ([string]::IsNullOrWhiteSpace($LogPath)) {
    $LogPath = Join-Path $RepoPath "Temp\iterator_compile_preflight.log"
}

$logDir = Split-Path -Parent $LogPath
if (-not (Test-Path $logDir)) {
    New-Item -ItemType Directory -Path $logDir -Force | Out-Null
}

Write-Host "[IteratorCompilePreflight] repo=$RepoPath"
Write-Host "[IteratorCompilePreflight] unity=$UnityExe"
Write-Host "[IteratorCompilePreflight] log=$LogPath"

$arguments = @(
    "-batchmode",
    "-nographics",
    "-projectPath", $RepoPath,
    "-quit",
    "-logFile", $LogPath
)

if ($DisableAssemblyUpdater) {
    $arguments += "-disable-assembly-updater"
    Write-Host "[IteratorCompilePreflight] assembly_updater=disabled"
}

$process = Start-Process -FilePath $UnityExe -ArgumentList $arguments -PassThru -NoNewWindow
if (-not $process.WaitForExit($TimeoutSec * 1000)) {
    try {
        $process.Kill()
    }
    catch {
    }

    throw "Unity compile preflight timed out after $TimeoutSec seconds."
}

$exitCode = $process.ExitCode
if (-not (Test-Path $LogPath)) {
    throw "Unity did not produce a log at expected path: $LogPath"
}

$logLines = @(Get-Content -Path $LogPath)
$csErrors = @($logLines | Select-String -Pattern "error\s+CS\d+")
$genericCompileErrors = @($logLines | Select-String -Pattern ":\s*error\s")
$compilationFailed = @($logLines | Select-String -Pattern "Compilation failed")
$scriptsHaveErrors = @($logLines | Select-String -Pattern "Scripts have compiler errors")

$allHits = @($csErrors + $genericCompileErrors + $compilationFailed + $scriptsHaveErrors | Select-Object -Unique)
$hasCompileErrors = $allHits.Count -gt 0
$failed = ($exitCode -ne 0) -or $hasCompileErrors

Write-Host "[IteratorCompilePreflight] unity_exit=$exitCode cs_errors=$($csErrors.Count) compile_error_lines=$($genericCompileErrors.Count) compilation_failed_markers=$($compilationFailed.Count)"

if ($failed) {
    Write-Host "[IteratorCompilePreflight] FAIL"
    $preview = @($allHits | Select-Object -First 20)
    if ($preview.Count -gt 0) {
        Write-Host "[IteratorCompilePreflight] first_error_lines:"
        foreach ($hit in $preview) {
            Write-Host $hit.Line
        }
    }
    else {
        $tail = @($logLines | Select-Object -Last 20)
        if ($tail.Count -gt 0) {
            Write-Host "[IteratorCompilePreflight] log_tail:"
            foreach ($line in $tail) {
                Write-Host $line
            }
        }
    }

    exit 1
}

Write-Host "[IteratorCompilePreflight] PASS"
exit 0
