param(
    [string]$UnityExe = "",
    [string]$RepoPath = "C:\dev\Tri\space4x",
    [string]$SuitePath = "Assets/Scenarios/space4x_capital_shooting_range_suite.v1.json",
    [string]$OutDir = "Reports/capital_range_suite_v1",
    [int]$ScenarioTimeoutSec = 900,
    [int]$CompletionGraceSec = 20,
    [string]$ScenarioIds = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($UnityExe)) {
    $resolver = Join-Path $PSScriptRoot "ResolveUnityEditor.ps1"
    if (-not (Test-Path $resolver)) {
        throw "Missing Unity resolver script: $resolver"
    }

    $resolved = & $resolver -RepoPath $RepoPath -Quiet
    if ($LASTEXITCODE -ne 0) {
        throw "Unity resolver failed with exit code $LASTEXITCODE"
    }

    $lines = @($resolved | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($lines.Count -eq 0) {
        throw "Unity resolver returned no path."
    }

    $UnityExe = ($lines | Select-Object -Last 1).Trim()
}

if (-not (Test-Path $UnityExe)) {
    throw "Unity executable not found: $UnityExe"
}

if (-not (Test-Path $RepoPath)) {
    throw "Repo path not found: $RepoPath"
}

function Resolve-PathInRepo([string]$root, [string]$path) {
    if ([string]::IsNullOrWhiteSpace($path)) {
        return ""
    }

    if ([System.IO.Path]::IsPathRooted($path)) {
        return [System.IO.Path]::GetFullPath($path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $root $path))
}

function Get-MetricValue($metrics, [string]$name, [double]$fallback = -1.0) {
    if ($null -eq $metrics) {
        return $fallback
    }

    $prop = $metrics.PSObject.Properties[$name]
    if ($null -eq $prop -or $null -eq $prop.Value) {
        return $fallback
    }

    try {
        return [double]$prop.Value
    }
    catch {
        return $fallback
    }
}

function Get-Question($report, [string]$id) {
    if ($null -eq $report -or $null -eq $report.questions) {
        return $null
    }

    return @($report.questions | Where-Object { $_.id -eq $id } | Select-Object -First 1)[0]
}

function Get-QuestionMetrics($question) {
    if ($null -eq $question) {
        return $null
    }

    $prop = $question.PSObject.Properties["metrics"]
    if ($null -eq $prop) {
        return $null
    }

    return $prop.Value
}

function Test-TerminalProgressState($progress, [string]$reportPath) {
    if ($null -eq $progress) {
        return $false
    }

    $phase = [string]$progress.phase
    if ([string]::IsNullOrWhiteSpace($phase)) {
        return $false
    }

    if ($phase -eq "complete") {
        return $true
    }

    if ($phase -eq "shutdown") {
        if (Test-Path $reportPath) {
            return $true
        }

        $checkpoint = [string]$progress.checkpoint
        if (-not [string]::IsNullOrWhiteSpace($checkpoint)) {
            if ($checkpoint -eq "exit_request" -or
                $checkpoint -eq "headless_exit_request" -or
                $checkpoint -eq "quit_requested") {
                return $true
            }
        }
    }

    return $false
}

$suiteFullPath = Resolve-PathInRepo $RepoPath $SuitePath
if (-not (Test-Path $suiteFullPath)) {
    throw "Suite file not found: $suiteFullPath"
}

$suite = Get-Content $suiteFullPath -Raw | ConvertFrom-Json
if ($null -eq $suite -or $null -eq $suite.scenarios -or $suite.scenarios.Count -eq 0) {
    throw "Suite contains no scenarios: $suiteFullPath"
}

$selectedIds = @()
if (-not [string]::IsNullOrWhiteSpace($ScenarioIds)) {
    $selectedIds = @([regex]::Split($ScenarioIds, "[,;]") | ForEach-Object { $_.Trim() } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}

$scenarioList = @($suite.scenarios)
if ($selectedIds.Count -gt 0) {
    $scenarioList = @($scenarioList | Where-Object { $selectedIds -contains $_.id })
}

if ($scenarioList.Count -eq 0) {
    throw "No scenarios matched filter."
}

$outRoot = Resolve-PathInRepo $RepoPath $OutDir
$projectTempRoot = Resolve-PathInRepo $RepoPath "Temp"
if ($outRoot.StartsWith($projectTempRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    Write-Host "[RunCapitalRangeSuite] warning: out dir is under project Temp and may be cleaned by Unity between runs."
}
if (-not (Test-Path $outRoot)) {
    New-Item -ItemType Directory -Path $outRoot -Force | Out-Null
}

$results = New-Object System.Collections.Generic.List[object]
$suiteId = if ([string]::IsNullOrWhiteSpace($suite.suiteId)) { "space4x.capital_shooting_range.suite.v1" } else { $suite.suiteId }

Write-Host "[RunCapitalRangeSuite] suite=$suiteId cases=$($scenarioList.Count) out=$outRoot"

for ($index = 0; $index -lt $scenarioList.Count; $index++) {
    $entry = $scenarioList[$index]
    if ($null -eq $entry -or [string]::IsNullOrWhiteSpace($entry.path)) {
        continue
    }

    $caseId = if ([string]::IsNullOrWhiteSpace($entry.id)) { [System.IO.Path]::GetFileNameWithoutExtension($entry.path) } else { $entry.id.Trim() }
    $caseLabel = if ([string]::IsNullOrWhiteSpace($entry.label)) { $caseId } else { $entry.label.Trim() }
    $scenarioFullPath = Resolve-PathInRepo $RepoPath $entry.path
    if (-not (Test-Path $scenarioFullPath)) {
        Write-Host "[RunCapitalRangeSuite] skip missing scenario: $scenarioFullPath"
        continue
    }

    $runDir = Join-Path $outRoot $caseId
    if (-not (Test-Path $runDir)) {
        New-Item -ItemType Directory -Path $runDir -Force | Out-Null
    }

    $logPath = Join-Path $runDir "unity.log"
    $telemetryPath = Join-Path $runDir "telemetry.ndjson"
    $progressPath = Join-Path $runDir "progress.json"
    $reportPath = Join-Path $runDir "operator_report.json"

    [System.Environment]::SetEnvironmentVariable("SPACE4X_SCENARIO_PATH", $scenarioFullPath, "Process")
    [System.Environment]::SetEnvironmentVariable("PUREDOTS_HEADLESS", "1", "Process")
    [System.Environment]::SetEnvironmentVariable("PUREDOTS_FORCE_RENDER", "0", "Process")
    [System.Environment]::SetEnvironmentVariable("PUREDOTS_RENDERING", "0", "Process")
    [System.Environment]::SetEnvironmentVariable("PUREDOTS_EXIT_POLICY", "nevernonzero", "Process")
    [System.Environment]::SetEnvironmentVariable("PUREDOTS_HEADLESS_TIME_PROOF", "0", "Process")
    [System.Environment]::SetEnvironmentVariable("PUREDOTS_HEADLESS_REWIND_PROOF", "0", "Process")
    [System.Environment]::SetEnvironmentVariable("SPACE4X_HEADLESS_MINING_PROOF", "0", "Process")
    [System.Environment]::SetEnvironmentVariable("SPACE4X_HEADLESS_MOVEMENT_DIAG", "0", "Process")
    [System.Environment]::SetEnvironmentVariable("SPACE4X_HEADLESS_SCENARIO_QUIT_DISABLE", "", "Process")
    [System.Environment]::SetEnvironmentVariable("PUREDOTS_TELEMETRY_ENABLE", "1", "Process")
    [System.Environment]::SetEnvironmentVariable("PUREDOTS_TELEMETRY_PATH", $telemetryPath, "Process")
    [System.Environment]::SetEnvironmentVariable("PUREDOTS_TELEMETRY_MAX_BYTES", "8388608", "Process")
    [System.Environment]::SetEnvironmentVariable("PUREDOTS_PERF_TELEMETRY_PATH", "NUL", "Process")

    $arguments = @(
        "-batchmode",
        "-nographics",
        "-projectPath", $RepoPath,
        "-executeMethod", "Space4X.Editor.Diagnostics.Space4XCapitalRangeBatchRunner.Run",
        "--scenario", $scenarioFullPath,
        "--outDir", $runDir,
        "--timeoutSec", [string]$ScenarioTimeoutSec,
        "-logFile", $logPath
    )

    Write-Host "[RunCapitalRangeSuite] start $($index + 1)/$($scenarioList.Count) id=$caseId scenario=$scenarioFullPath"
    $proc = Start-Process -FilePath $UnityExe -ArgumentList $arguments -PassThru -NoNewWindow
    $startTime = Get-Date
    $timedOut = $false
    $killedAfterComplete = $false
    $completeSeenAt = $null

    while (-not $proc.HasExited) {
        $elapsed = (Get-Date) - $startTime
        if ($elapsed.TotalSeconds -ge $ScenarioTimeoutSec) {
            try {
                Stop-Process -Id $proc.Id -Force
                $proc | Wait-Process -Timeout 10 -ErrorAction SilentlyContinue
                $proc.Refresh()
            }
            catch {
            }

            $timedOut = $true
            break
        }

        if (Test-Path $progressPath) {
            try {
                $progress = Get-Content $progressPath -Raw | ConvertFrom-Json
                if (Test-TerminalProgressState -progress $progress -reportPath $reportPath) {
                    if ($null -eq $completeSeenAt) {
                        $completeSeenAt = Get-Date
                    }
                    elseif (((Get-Date) - $completeSeenAt).TotalSeconds -ge $CompletionGraceSec) {
                        try {
                            Stop-Process -Id $proc.Id -Force
                            $proc | Wait-Process -Timeout 10 -ErrorAction SilentlyContinue
                            $proc.Refresh()
                        }
                        catch {
                        }

                        $killedAfterComplete = $true
                        break
                    }
                }
            }
            catch {
            }
        }

        Start-Sleep -Seconds 2
        $proc.Refresh()
    }

    $exitCode = $null
    if ($proc.HasExited) {
        $exitCode = $proc.ExitCode
    }

    $report = $null
    if (Test-Path $reportPath) {
        try {
            $report = Get-Content $reportPath -Raw | ConvertFrom-Json
        }
        catch {
            $report = $null
        }
    }

    $qScore = Get-Question $report "space4x.q.gunnery.capital_range.score"
    $qHitRate = Get-Question $report "space4x.q.gunnery.capital_range.hit_rate"
    $qReaction = Get-Question $report "space4x.q.gunnery.capital_range.reaction_time"
    $scoreMetrics = Get-QuestionMetrics $qScore
    $hitRateMetrics = Get-QuestionMetrics $qHitRate
    $reactionMetrics = Get-QuestionMetrics $qReaction
    $score = Get-MetricValue $scoreMetrics "score" -1
    $hitRate = Get-MetricValue $hitRateMetrics "hit_rate" -1
    $shotsFired = Get-MetricValue $hitRateMetrics "shots_fired" -1
    $shotsHit = Get-MetricValue $hitRateMetrics "shots_hit" -1
    $reactionTime = Get-MetricValue $reactionMetrics "reaction_time_s" -1
    $requiredFailures = @()
    if ($null -ne $report -and $null -ne $report.questions) {
        $requiredFailures = @($report.questions | Where-Object { $_.required -and $_.status -ne "pass" })
    }

    $gateRequired = $true
    $gateProp = $entry.PSObject.Properties["gateRequired"]
    if ($null -ne $gateProp -and $null -ne $gateProp.Value) {
        try {
            $gateRequired = [bool]$gateProp.Value
        }
        catch {
            $gateRequired = $true
        }
    }

    $requiredPass = (($null -ne $report) -and $requiredFailures.Count -eq 0)
    $suitePass = if ($gateRequired) { $requiredPass } else { $true }

    $record = [pscustomobject]@{
        id = $caseId
        label = $caseLabel
        scenarioPath = $scenarioFullPath
        tags = @($entry.tags)
        gateRequired = $gateRequired
        exitCode = $exitCode
        timedOut = $timedOut
        killedAfterComplete = $killedAfterComplete
        reportFound = ($null -ne $report)
        requiredPass = $requiredPass
        suitePass = $suitePass
        score = $score
        hitRate = $hitRate
        shotsFired = $shotsFired
        shotsHit = $shotsHit
        reactionTimeS = $reactionTime
        runDir = $runDir
    }
    $results.Add($record) | Out-Null

    Write-Host ("[RunCapitalRangeSuite] done id={0} score={1:N2} hitRate={2:N3} fired={3:N0} requiredPass={4} suitePass={5} timeout={6}" -f $caseId, $score, $hitRate, $shotsFired, $record.requiredPass, $record.suitePass, $timedOut)
}

$resultArray = @($results.ToArray())
$ranked = @($resultArray | Where-Object { $_.reportFound } | Sort-Object -Property @{ Expression = "score"; Descending = $true }, @{ Expression = "hitRate"; Descending = $true })
$ranking = New-Object System.Collections.Generic.List[object]
for ($i = 0; $i -lt $ranked.Count; $i++) {
    $r = $ranked[$i]
    $ranking.Add([pscustomobject]@{
        rank = $i + 1
        id = $r.id
        label = $r.label
        score = $r.score
        hitRate = $r.hitRate
        shotsFired = $r.shotsFired
        shotsHit = $r.shotsHit
        reactionTimeS = $r.reactionTimeS
    }) | Out-Null
}

$passCount = 0
$failCount = 0
$timeoutCount = 0
for ($i = 0; $i -lt $resultArray.Count; $i++) {
    $run = $resultArray[$i]
    if ($run.suitePass) {
        $passCount++
    }
    else {
        $failCount++
    }
    if ($run.timedOut) {
        $timeoutCount++
    }
}

$summaryTable = [ordered]@{}
$summaryTable["suiteId"] = $suiteId
$summaryTable["generatedUtc"] = (Get-Date).ToUniversalTime().ToString("o")
$summaryTable["runCount"] = $resultArray.Count
$summaryTable["passCount"] = $passCount
$summaryTable["failCount"] = $failCount
$summaryTable["timeoutCount"] = $timeoutCount
$summaryTable["runs"] = $resultArray
$summaryTable["rankingByScore"] = @($ranking.ToArray())
$summary = [pscustomobject]$summaryTable

$summaryPath = Join-Path $outRoot "capital_range_suite_result.json"
$summary | ConvertTo-Json -Depth 10 | Set-Content -Path $summaryPath -Encoding ASCII

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("# Capital Range Suite Result")
$lines.Add("")
$lines.Add(("Suite: {0}" -f $suiteId))
$lines.Add(("Generated (UTC): {0}" -f $summary.generatedUtc))
$lines.Add("")
$lines.Add("| Rank | Case | Score | Hit Rate | Shots | Reaction (s) | Required Pass | Gate | Suite Pass |")
$lines.Add("| --- | --- | ---: | ---: | ---: | ---: | :---: | :---: | :---: |")
for ($i = 0; $i -lt $ranking.Count; $i++) {
    $row = $ranking[$i]
    $run = @($resultArray | Where-Object { $_.id -eq $row.id } | Select-Object -First 1)[0]
    $gateText = if ($run.gateRequired) { "required" } else { "advisory" }
    $lines.Add(("| {0} | {1} | {2:N2} | {3:N3} | {4:N0}/{5:N0} | {6:N2} | {7} | {8} | {9} |" -f $row.rank, $row.id, $row.score, $row.hitRate, $row.shotsHit, $row.shotsFired, $row.reactionTimeS, $run.requiredPass, $gateText, $run.suitePass))
}

$markdownPath = Join-Path $outRoot "capital_range_suite_result.md"
$lines | Set-Content -Path $markdownPath -Encoding ASCII

Write-Host "[RunCapitalRangeSuite] summary=$summaryPath"
Write-Host "[RunCapitalRangeSuite] ranking=$markdownPath"

if ($summary.failCount -gt 0) {
    exit 1
}

exit 0
