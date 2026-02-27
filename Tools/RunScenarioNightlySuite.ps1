param(
    [string]$UnityExe = "",
    [string]$RepoPath = "C:\dev\Tri\space4x",
    [string]$SuitePath = "Tools/ScenarioSuites/space4x_nightly_weapons_modules_cohesion_suite.v1.json",
    [string]$OutDir = "Reports/nightly_weapons_modules_cohesion_v1",
    [int]$ScenarioTimeoutSec = 900,
    [int]$CompletionGraceSec = 20,
    [string]$ScenarioIds = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-PathInRepo([string]$root, [string]$path) {
    if ([string]::IsNullOrWhiteSpace($path)) {
        return ""
    }

    if ([System.IO.Path]::IsPathRooted($path)) {
        return [System.IO.Path]::GetFullPath($path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $root $path))
}

function Try-ConvertToDouble($value, [double]$fallback = [double]::NaN) {
    if ($null -eq $value) {
        return $fallback
    }

    $num = 0.0
    if ([double]::TryParse([string]$value, [ref]$num)) {
        return $num
    }

    return $fallback
}

function Collect-ReportMetrics($report) {
    $metrics = @{}

    if ($null -ne $report) {
        $summaryProp = $report.PSObject.Properties["summary"]
        if ($null -ne $summaryProp -and $null -ne $summaryProp.Value) {
            foreach ($prop in $summaryProp.Value.PSObject.Properties) {
                $numeric = Try-ConvertToDouble $prop.Value
                if (-not [double]::IsNaN($numeric)) {
                    $metrics[$prop.Name] = $numeric
                }
            }
        }

        $questionsProp = $report.PSObject.Properties["questions"]
        if ($null -ne $questionsProp -and $null -ne $questionsProp.Value) {
            foreach ($question in @($questionsProp.Value)) {
                if ($null -eq $question) {
                    continue
                }

                $questionId = [string]$question.id
                $questionMetricsProp = $question.PSObject.Properties["metrics"]
                if ($null -eq $questionMetricsProp -or $null -eq $questionMetricsProp.Value) {
                    continue
                }

                foreach ($m in $questionMetricsProp.Value.PSObject.Properties) {
                    $numeric = Try-ConvertToDouble $m.Value
                    if ([double]::IsNaN($numeric)) {
                        continue
                    }

                    if (-not [string]::IsNullOrWhiteSpace($questionId)) {
                        $metrics["$questionId.$($m.Name)"] = $numeric
                    }

                    if (-not $metrics.ContainsKey($m.Name)) {
                        $metrics[$m.Name] = $numeric
                    }
                }
            }
        }
    }

    return $metrics
}

function Get-MetricOrDefault([hashtable]$metrics, [string]$name, [double]$fallback = [double]::NaN) {
    if ($null -eq $metrics -or [string]::IsNullOrWhiteSpace($name)) {
        return $fallback
    }

    if ($metrics.ContainsKey($name)) {
        return [double]$metrics[$name]
    }

    return $fallback
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

function Compare-Values([double]$left, [double]$right, [string]$op) {
    switch ($op) {
        "gt" { return $left -gt $right }
        "gte" { return $left -ge $right }
        "lt" { return $left -lt $right }
        "lte" { return $left -le $right }
        "eq" { return [math]::Abs($left - $right) -le 1e-9 }
        default { return $false }
    }
}

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

$suiteFullPath = Resolve-PathInRepo $RepoPath $SuitePath
if (-not (Test-Path $suiteFullPath)) {
    throw "Suite file not found: $suiteFullPath"
}

$suite = Get-Content $suiteFullPath -Raw | ConvertFrom-Json
$casesProp = $suite.PSObject.Properties["cases"]
if ($null -eq $casesProp -or $null -eq $casesProp.Value) {
    $casesProp = $suite.PSObject.Properties["scenarios"]
}

if ($null -eq $casesProp -or $null -eq $casesProp.Value -or @($casesProp.Value).Count -eq 0) {
    throw "Suite contains no cases/scenarios: $suiteFullPath"
}

$selectedIds = @()
if (-not [string]::IsNullOrWhiteSpace($ScenarioIds)) {
    $selectedIds = @([regex]::Split($ScenarioIds, "[,;]") |
        ForEach-Object { $_.Trim() } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}

$scenarioList = @($casesProp.Value)
if ($selectedIds.Count -gt 0) {
    $scenarioList = @($scenarioList | Where-Object { $selectedIds -contains $_.id })
}

if ($scenarioList.Count -eq 0) {
    throw "No scenarios matched filter."
}

$outRoot = Resolve-PathInRepo $RepoPath $OutDir
$projectTempRoot = Resolve-PathInRepo $RepoPath "Temp"
if ($outRoot.StartsWith($projectTempRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    Write-Host "[RunScenarioNightlySuite] warning: out dir is under project Temp and may be cleaned by Unity between runs."
}

if (-not (Test-Path $outRoot)) {
    New-Item -ItemType Directory -Path $outRoot -Force | Out-Null
}

$suiteId = if ([string]::IsNullOrWhiteSpace($suite.suiteId)) { "space4x.nightly.suite" } else { [string]$suite.suiteId }
$results = New-Object System.Collections.Generic.List[object]

Write-Host "[RunScenarioNightlySuite] suite=$suiteId cases=$($scenarioList.Count) out=$outRoot"

for ($index = 0; $index -lt $scenarioList.Count; $index++) {
    $entry = $scenarioList[$index]
    if ($null -eq $entry -or [string]::IsNullOrWhiteSpace($entry.path)) {
        continue
    }

    $caseId = if ([string]::IsNullOrWhiteSpace($entry.id)) { [System.IO.Path]::GetFileNameWithoutExtension($entry.path) } else { $entry.id.Trim() }
    $caseLabel = if ([string]::IsNullOrWhiteSpace($entry.label)) { $caseId } else { $entry.label.Trim() }
    $scenarioFullPath = Resolve-PathInRepo $RepoPath $entry.path
    if (-not (Test-Path $scenarioFullPath)) {
        Write-Host "[RunScenarioNightlySuite] skip missing scenario: $scenarioFullPath"
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
    $answersPath = Join-Path $runDir "headless_answers.json"

    $questionIds = @()
    try {
        $scenarioDoc = Get-Content -Path $scenarioFullPath -Raw | ConvertFrom-Json
        if ($null -ne $scenarioDoc -and $null -ne $scenarioDoc.scenarioConfig -and $null -ne $scenarioDoc.scenarioConfig.headlessQuestions) {
            $questionIds = @($scenarioDoc.scenarioConfig.headlessQuestions |
                ForEach-Object { [string]$_.id } |
                Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
        }
    }
    catch {
        $questionIds = @()
    }

    $enableMiningProof = $false
    $enableMovementDiag = $false
    foreach ($qid in $questionIds) {
        if ($qid.StartsWith("space4x.q.mining.", [System.StringComparison]::OrdinalIgnoreCase)) {
            $enableMiningProof = $true
        }
        if ($qid.StartsWith("space4x.q.movement.turnrate_bounds", [System.StringComparison]::OrdinalIgnoreCase)) {
            $enableMovementDiag = $true
        }
    }

    [System.Environment]::SetEnvironmentVariable("SPACE4X_SCENARIO_PATH", $scenarioFullPath, "Process")
    [System.Environment]::SetEnvironmentVariable("PUREDOTS_HEADLESS", "1", "Process")
    [System.Environment]::SetEnvironmentVariable("PUREDOTS_FORCE_RENDER", "0", "Process")
    [System.Environment]::SetEnvironmentVariable("PUREDOTS_RENDERING", "0", "Process")
    [System.Environment]::SetEnvironmentVariable("PUREDOTS_EXIT_POLICY", "nevernonzero", "Process")
    [System.Environment]::SetEnvironmentVariable("PUREDOTS_HEADLESS_TIME_PROOF", "0", "Process")
    [System.Environment]::SetEnvironmentVariable("PUREDOTS_HEADLESS_REWIND_PROOF", "0", "Process")
    [System.Environment]::SetEnvironmentVariable("SPACE4X_HEADLESS_MINING_PROOF", $(if ($enableMiningProof) { "1" } else { "0" }), "Process")
    [System.Environment]::SetEnvironmentVariable("SPACE4X_HEADLESS_MOVEMENT_DIAG", $(if ($enableMovementDiag) { "1" } else { "0" }), "Process")
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

    Write-Host "[RunScenarioNightlySuite] start $($index + 1)/$($scenarioList.Count) id=$caseId scenario=$scenarioFullPath"
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

    $answers = $null
    if (Test-Path $answersPath) {
        try {
            $answers = Get-Content $answersPath -Raw | ConvertFrom-Json
        }
        catch {
            $answers = $null
        }
    }

    $requiredFailures = @()
    $statusCounts = @{
        pass = 0
        fail = 0
        warn = 0
        unknown = 0
    }

    if ($null -ne $report -and $null -ne $report.questions) {
        foreach ($q in @($report.questions)) {
            if ($null -eq $q) {
                continue
            }

            $status = [string]$q.status
            if ($statusCounts.ContainsKey($status)) {
                $statusCounts[$status]++
            }
            else {
                $statusCounts["unknown"]++
            }

            if ($q.required -and $status -ne "pass") {
                $requiredFailures += [string]$q.id
            }
        }
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

    $metrics = Collect-ReportMetrics $report
    $score = Get-MetricOrDefault $metrics "score"
    if ([double]::IsNaN($score)) { $score = Get-MetricOrDefault $metrics "space4x.gunnery.capital_range.score" }
    $hitRate = Get-MetricOrDefault $metrics "hit_rate"
    if ([double]::IsNaN($hitRate)) { $hitRate = Get-MetricOrDefault $metrics "space4x.gunnery.capital_range.hit_rate" }
    $shotsFired = Get-MetricOrDefault $metrics "shots_fired"
    if ([double]::IsNaN($shotsFired)) { $shotsFired = Get-MetricOrDefault $metrics "space4x.gunnery.capital_range.shots.fired" }
    $shotsHit = Get-MetricOrDefault $metrics "shots_hit"
    if ([double]::IsNaN($shotsHit)) { $shotsHit = Get-MetricOrDefault $metrics "space4x.gunnery.capital_range.shots.hit" }
    $reactionTime = Get-MetricOrDefault $metrics "reaction_time_s"
    if ([double]::IsNaN($reactionTime)) { $reactionTime = Get-MetricOrDefault $metrics "space4x.gunnery.capital_range.reaction_time_s" }

    $requiredPass = (($null -ne $report) -and $requiredFailures.Count -eq 0)
    $suitePass = if ($gateRequired) { $requiredPass } else { $true }
    $record = [pscustomobject]@{
        id = $caseId
        label = $caseLabel
        scenarioPath = $scenarioFullPath
        tags = @($entry.tags)
        gateRequired = $gateRequired
        timedOut = $timedOut
        killedAfterComplete = $killedAfterComplete
        exitCode = $exitCode
        reportFound = ($null -ne $report)
        answersFound = ($null -ne $answers)
        requiredPass = $requiredPass
        suitePass = $suitePass
        requiredFailureIds = @($requiredFailures)
        questionStatusCounts = $statusCounts
        score = $score
        hitRate = $hitRate
        shotsFired = $shotsFired
        shotsHit = $shotsHit
        reactionTimeS = $reactionTime
        metrics = $metrics
        runDir = $runDir
    }
    $results.Add($record) | Out-Null

    $scoreText = if ([double]::IsNaN($score)) { "n/a" } else { "{0:N3}" -f $score }
    $hitRateText = if ([double]::IsNaN($hitRate)) { "n/a" } else { "{0:N3}" -f $hitRate }
    Write-Host ("[RunScenarioNightlySuite] done id={0} score={1} hitRate={2} requiredPass={3} suitePass={4} timeout={5}" -f $caseId, $scoreText, $hitRateText, $requiredPass, $suitePass, $timedOut)
}

$resultArray = @($results.ToArray())
$runById = @{}
foreach ($run in $resultArray) {
    $runById[[string]$run.id] = $run
}

$comparisonResults = New-Object System.Collections.Generic.List[object]
$comparisonFailuresRequired = 0
$comparisonFailuresOptional = 0
$comparisonsProp = $suite.PSObject.Properties["comparisons"]
if ($null -ne $comparisonsProp -and $null -ne $comparisonsProp.Value) {
    foreach ($comparison in @($comparisonsProp.Value)) {
        if ($null -eq $comparison) {
            continue
        }

        $comparisonId = if ([string]::IsNullOrWhiteSpace($comparison.id)) { "comparison_$($comparisonResults.Count + 1)" } else { [string]$comparison.id }
        $label = if ([string]::IsNullOrWhiteSpace($comparison.label)) { $comparisonId } else { [string]$comparison.label }
        $leftId = [string]$comparison.left
        $rightId = [string]$comparison.right
        $metric = [string]$comparison.metric
        $op = if ([string]::IsNullOrWhiteSpace($comparison.op)) { "gt" } else { [string]$comparison.op }
        $required = $false
        $requiredProp = $comparison.PSObject.Properties["required"]
        if ($null -ne $requiredProp -and $null -ne $requiredProp.Value) {
            try {
                $required = [bool]$requiredProp.Value
            }
            catch {
                $required = $false
            }
        }

        $leftValue = [double]::NaN
        $rightValue = [double]::NaN
        $status = "fail"
        $notes = ""
        $minDelta = Try-ConvertToDouble ($comparison.PSObject.Properties["minDelta"]?.Value)
        $maxDelta = Try-ConvertToDouble ($comparison.PSObject.Properties["maxDelta"]?.Value)

        if (-not $runById.ContainsKey($leftId) -or -not $runById.ContainsKey($rightId)) {
            $status = "skipped"
            $notes = "missing run(s) for filtered suite"
        }
        else {
            $leftRun = $runById[$leftId]
            $rightRun = $runById[$rightId]
            $leftValue = Try-ConvertToDouble ($leftRun.PSObject.Properties[$metric]?.Value)
            $rightValue = Try-ConvertToDouble ($rightRun.PSObject.Properties[$metric]?.Value)

            if ([double]::IsNaN($leftValue) -and $leftRun.metrics) {
                $leftValue = Try-ConvertToDouble ($leftRun.metrics[$metric])
            }
            if ([double]::IsNaN($rightValue) -and $rightRun.metrics) {
                $rightValue = Try-ConvertToDouble ($rightRun.metrics[$metric])
            }

            if ([double]::IsNaN($leftValue) -or [double]::IsNaN($rightValue)) {
                $status = "fail"
                $notes = "missing metric"
            }
            else {
                $pass = Compare-Values -left $leftValue -right $rightValue -op $op

                if (-not [double]::IsNaN($minDelta)) {
                    switch ($op) {
                        "gt" { $pass = $pass -and (($leftValue - $rightValue) -ge $minDelta) }
                        "gte" { $pass = $pass -and (($leftValue - $rightValue) -ge $minDelta) }
                        "lt" { $pass = $pass -and (($rightValue - $leftValue) -ge $minDelta) }
                        "lte" { $pass = $pass -and (($rightValue - $leftValue) -ge $minDelta) }
                        "eq" { $pass = $pass -and ([math]::Abs($leftValue - $rightValue) -le $minDelta) }
                    }
                }

                if (-not [double]::IsNaN($maxDelta)) {
                    $pass = $pass -and ([math]::Abs($leftValue - $rightValue) -le $maxDelta)
                }

                $status = if ($pass) { "pass" } else { "fail" }
            }
        }

        if ($status -ne "pass" -and $status -ne "skipped") {
            if ($required) { $comparisonFailuresRequired++ } else { $comparisonFailuresOptional++ }
        }

        $comparisonResults.Add([pscustomobject]@{
            id = $comparisonId
            label = $label
            left = $leftId
            right = $rightId
            metric = $metric
            op = $op
            minDelta = $minDelta
            maxDelta = $maxDelta
            required = $required
            status = $status
            leftValue = $leftValue
            rightValue = $rightValue
            delta = if ([double]::IsNaN($leftValue) -or [double]::IsNaN($rightValue)) { [double]::NaN } else { $leftValue - $rightValue }
            notes = $notes
        }) | Out-Null
    }
}

$passCount = 0
$failCount = 0
$timeoutCount = 0
foreach ($run in $resultArray) {
    if ($run.suitePass) { $passCount++ } else { $failCount++ }
    if ($run.timedOut) { $timeoutCount++ }
}

$summary = [pscustomobject]@{
    suiteId = $suiteId
    generatedUtc = (Get-Date).ToUniversalTime().ToString("o")
    runCount = $resultArray.Count
    passCount = $passCount
    failCount = $failCount
    timeoutCount = $timeoutCount
    comparisonCount = $comparisonResults.Count
    comparisonFailuresRequired = $comparisonFailuresRequired
    comparisonFailuresOptional = $comparisonFailuresOptional
    overallPass = ($failCount -eq 0 -and $comparisonFailuresRequired -eq 0)
    runs = $resultArray
    comparisons = @($comparisonResults.ToArray())
}

$summaryPath = Join-Path $outRoot "scenario_nightly_suite_result.json"
$summary | ConvertTo-Json -Depth 12 | Set-Content -Path $summaryPath -Encoding ASCII

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("# Scenario Nightly Suite Result")
$lines.Add("")
$lines.Add(("Suite: {0}" -f $suiteId))
$lines.Add(("Generated (UTC): {0}" -f $summary.generatedUtc))
$lines.Add(("Overall pass: {0}" -f $summary.overallPass))
$lines.Add("")
$lines.Add(("Runs: pass={0} fail={1} timeout={2} total={3}" -f $passCount, $failCount, $timeoutCount, $resultArray.Count))
$lines.Add(("Comparisons: required_fail={0} optional_fail={1} total={2}" -f $comparisonFailuresRequired, $comparisonFailuresOptional, $comparisonResults.Count))
$lines.Add("")
$lines.Add("| Case | Gate | Suite Pass | Required Pass | Score | Hit Rate | Reaction (s) | Fail Questions |")
$lines.Add("| --- | :---: | :---: | :---: | ---: | ---: | ---: | --- |")

foreach ($run in $resultArray) {
    $scoreText = if ([double]::IsNaN([double]$run.score)) { "n/a" } else { "{0:N3}" -f [double]$run.score }
    $hitRateText = if ([double]::IsNaN([double]$run.hitRate)) { "n/a" } else { "{0:N3}" -f [double]$run.hitRate }
    $reactionText = if ([double]::IsNaN([double]$run.reactionTimeS)) { "n/a" } else { "{0:N3}" -f [double]$run.reactionTimeS }
    $gateText = if ($run.gateRequired) { "required" } else { "advisory" }
    $failQuestions = if ($run.requiredFailureIds.Count -gt 0) { ($run.requiredFailureIds -join ", ") } else { "-" }
    $lines.Add(("| {0} | {1} | {2} | {3} | {4} | {5} | {6} | {7} |" -f $run.id, $gateText, $run.suitePass, $run.requiredPass, $scoreText, $hitRateText, $reactionText, $failQuestions))
}

if ($comparisonResults.Count -gt 0) {
    $lines.Add("")
    $lines.Add("| Comparison | Required | Status | Metric | Left | Right | Delta |")
    $lines.Add("| --- | :---: | :---: | --- | ---: | ---: | ---: |")
    foreach ($c in $comparisonResults) {
        $leftText = if ([double]::IsNaN([double]$c.leftValue)) { "n/a" } else { "{0:N3}" -f [double]$c.leftValue }
        $rightText = if ([double]::IsNaN([double]$c.rightValue)) { "n/a" } else { "{0:N3}" -f [double]$c.rightValue }
        $deltaText = if ([double]::IsNaN([double]$c.delta)) { "n/a" } else { "{0:N3}" -f [double]$c.delta }
        $lines.Add(("| {0} | {1} | {2} | {3} ({4}) | {5} | {6} | {7} |" -f $c.id, $c.required, $c.status, $c.metric, $c.op, $leftText, $rightText, $deltaText))
    }
}

$markdownPath = Join-Path $outRoot "scenario_nightly_suite_result.md"
$lines | Set-Content -Path $markdownPath -Encoding ASCII

Write-Host "[RunScenarioNightlySuite] summary=$summaryPath"
Write-Host "[RunScenarioNightlySuite] markdown=$markdownPath"

if (-not $summary.overallPass) {
    exit 1
}

exit 0
