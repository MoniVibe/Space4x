param(
    [string]$RunSummaryPath = "",
    [string]$InvariantsPath = "",
    [string]$MovementMetricsPath = "",
    [string]$CameraProbePath = "",
    [string]$ThresholdsPath = "",
    [string]$OutJsonPath = "",
    [string]$OutMarkdownPath = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Read-JsonFile {
    param([string]$Path)
    if ([string]::IsNullOrWhiteSpace($Path)) { return $null }
    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json -AsHashtable -Depth 100
}

function Ensure-DirectoryForFile {
    param([string]$Path)
    if ([string]::IsNullOrWhiteSpace($Path)) { return }
    $dir = Split-Path -Parent $Path
    if ([string]::IsNullOrWhiteSpace($dir)) { return }
    if (-not (Test-Path -LiteralPath $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
}

function Coerce-DoubleOrNull {
    param([object]$Value)
    if ($null -eq $Value) { return $null }
    if ($Value -is [double] -or $Value -is [single] -or $Value -is [decimal] -or
        $Value -is [int16] -or $Value -is [int32] -or $Value -is [int64] -or
        $Value -is [uint16] -or $Value -is [uint32] -or $Value -is [uint64]) {
        return [double]$Value
    }

    $text = [string]$Value
    if ([string]::IsNullOrWhiteSpace($text)) { return $null }
    $parsed = 0.0
    if ([double]::TryParse($text, [System.Globalization.NumberStyles]::Float, [System.Globalization.CultureInfo]::InvariantCulture, [ref]$parsed)) {
        return $parsed
    }
    if ([double]::TryParse($text, [ref]$parsed)) {
        return $parsed
    }
    return $null
}

function Coerce-BoolOrNull {
    param([object]$Value)
    if ($null -eq $Value) { return $null }
    if ($Value -is [bool]) { return [bool]$Value }
    $text = ([string]$Value).Trim().ToLowerInvariant()
    if ($text -in @("1", "true", "yes", "y", "on")) { return $true }
    if ($text -in @("0", "false", "no", "n", "off")) { return $false }
    return $null
}

function Get-PathValue {
    param(
        [object]$Source,
        [string]$Path
    )

    if ($null -eq $Source -or [string]::IsNullOrWhiteSpace($Path)) { return $null }
    $cursor = $Source
    $segments = $Path.Split(".")
    foreach ($segment in $segments) {
        if ($null -eq $cursor) { return $null }
        if ($cursor -is [System.Collections.IDictionary]) {
            if (-not $cursor.Contains($segment)) { return $null }
            $cursor = $cursor[$segment]
            continue
        }
        if ($cursor -is [System.Collections.IList]) {
            $index = 0
            if (-not [int]::TryParse($segment, [ref]$index)) { return $null }
            if ($index -lt 0 -or $index -ge $cursor.Count) { return $null }
            $cursor = $cursor[$index]
            continue
        }
        return $null
    }
    return $cursor
}

function Build-MetricIndex {
    param(
        [object]$Source,
        [hashtable]$Index
    )

    if ($null -eq $Source) { return }

    if ($Source -is [System.Collections.IDictionary]) {
        foreach ($key in @("name", "metric", "id", "key")) {
            if (-not $Source.Contains($key)) { continue }
            $metricName = [string]$Source[$key]
            if ([string]::IsNullOrWhiteSpace($metricName)) { continue }
            foreach ($valueKey in @("value", "mean", "latest", "avg", "count")) {
                if (-not $Source.Contains($valueKey)) { continue }
                $numeric = Coerce-DoubleOrNull -Value $Source[$valueKey]
                if ($null -ne $numeric) {
                    $Index[$metricName] = $numeric
                    break
                }
            }
        }

        foreach ($entry in $Source.GetEnumerator()) {
            $key = [string]$entry.Key
            $value = $entry.Value
            $numeric = Coerce-DoubleOrNull -Value $value
            if ($null -ne $numeric -and $key.Contains(".")) {
                $Index[$key] = $numeric
            }
            Build-MetricIndex -Source $value -Index $Index
        }
        return
    }

    if ($Source -is [System.Collections.IList] -and -not ($Source -is [string])) {
        foreach ($item in $Source) {
            Build-MetricIndex -Source $item -Index $Index
        }
    }
}

function Resolve-MetricValue {
    param(
        [hashtable]$MetricIndex,
        [string[]]$Names
    )

    foreach ($name in $Names) {
        if ($MetricIndex.Contains($name)) {
            return $MetricIndex[$name]
        }
    }
    return $null
}

function Get-Percentile {
    param(
        [double[]]$Values,
        [double]$Percentile
    )

    if ($null -eq $Values -or $Values.Count -eq 0) { return $null }
    $sorted = @($Values | Sort-Object)
    if ($sorted.Count -eq 1) { return [double]$sorted[0] }
    $clamped = [Math]::Min(1.0, [Math]::Max(0.0, $Percentile))
    $index = [int][Math]::Floor(($sorted.Count - 1) * $clamped)
    return [double]$sorted[$index]
}

$defaultThresholds = @{
    tier1 = @{
        movement = @{
            max_naninf = 0.0
            max_stuck = 0.0
            max_teleport = 0.0
            min_carrier_samples = 1.0
            max_carrier_time_to_target_s = 95.0
            max_carrier_turn_time_s = 9.0
        }
        camera = @{
            min_samples = 20.0
            max_unaligned_ratio = 0.02
            max_spin_deg_per_s = 140.0
            max_yaw_delta_p95_deg = 50.0
        }
    }
    tier2 = @{
        movement = @{
            max_heading_oscillation_score = 4.0
            max_approach_mode_flip_rate = 2.5
            max_carrier_overshoot_distance = 35.0
            max_carrier_settle_time_s = 14.0
        }
        camera = @{
            max_unaligned_ratio_warn = 0.005
            max_spin_deg_per_s_warn = 90.0
            max_yaw_delta_p95_deg_warn = 30.0
        }
    }
}

if ([string]::IsNullOrWhiteSpace($ThresholdsPath)) {
    $ThresholdsPath = Join-Path $PSScriptRoot "..\Docs\Presentation\Space4X_Presentation_Nuisance_Thresholds.json"
}

$thresholdOverrides = Read-JsonFile -Path $ThresholdsPath
if ($thresholdOverrides) {
    $defaultThresholds = $thresholdOverrides
}

function Get-Threshold {
    param(
        [string]$Path,
        [double]$Fallback
    )

    $value = Get-PathValue -Source $defaultThresholds -Path $Path
    $numeric = Coerce-DoubleOrNull -Value $value
    if ($null -eq $numeric) { return $Fallback }
    return [double]$numeric
}

$runSummary = Read-JsonFile -Path $RunSummaryPath
$invariants = Read-JsonFile -Path $InvariantsPath
$movementMetrics = Read-JsonFile -Path $MovementMetricsPath

$metricIndex = @{}
Build-MetricIndex -Source $runSummary -Index $metricIndex
Build-MetricIndex -Source $invariants -Index $metricIndex
Build-MetricIndex -Source $movementMetrics -Index $metricIndex

$tier1Failures = New-Object System.Collections.Generic.List[string]
$tier2Warnings = New-Object System.Collections.Generic.List[string]
$evidence = New-Object System.Collections.Generic.List[string]

if ($RunSummaryPath) { [void]$evidence.Add("run_summary:$RunSummaryPath") }
if ($InvariantsPath) { [void]$evidence.Add("invariants:$InvariantsPath") }
if ($MovementMetricsPath) { [void]$evidence.Add("movement_metrics:$MovementMetricsPath") }
if ($CameraProbePath) { [void]$evidence.Add("camera_probe:$CameraProbePath") }
if ($ThresholdsPath) { [void]$evidence.Add("thresholds:$ThresholdsPath") }

$exitCode = Coerce-DoubleOrNull -Value (Get-PathValue -Source $runSummary -Path "exit_code")
if ($null -ne $exitCode -and $exitCode -ne 0) {
    [void]$tier1Failures.Add("run_summary.exit_code is $exitCode (expected 0).")
}

$exitReason = Get-PathValue -Source $runSummary -Path "exit_reason"
if ($exitReason -and [string]$exitReason -ne "SUCCESS") {
    [void]$tier1Failures.Add("run_summary.exit_reason is '$exitReason' (expected SUCCESS).")
}

$failingInvariants = Get-PathValue -Source $runSummary -Path "failing_invariants"
if ($failingInvariants -is [System.Collections.IList] -and $failingInvariants.Count -gt 0) {
    [void]$tier1Failures.Add("run_summary reports $($failingInvariants.Count) failing invariants.")
}

$invariantEntries = Get-PathValue -Source $invariants -Path "invariants"
if ($invariantEntries -is [System.Collections.IList]) {
    foreach ($entry in $invariantEntries) {
        if (-not ($entry -is [System.Collections.IDictionary])) { continue }
        $passed = Coerce-BoolOrNull -Value (Get-PathValue -Source $entry -Path "passed")
        if ($null -eq $passed) {
            $status = [string](Get-PathValue -Source $entry -Path "status")
            if ($status) { $passed = ($status.Trim().ToLowerInvariant() -in @("pass", "passed", "ok", "success")) }
        }
        if ($passed -eq $false) {
            $name = [string](Get-PathValue -Source $entry -Path "name")
            if (-not $name) { $name = [string](Get-PathValue -Source $entry -Path "id") }
            if (-not $name) { $name = "<unnamed>" }
            [void]$tier1Failures.Add("invariants.json marks '$name' as failed.")
        }
    }
}

$naninf = Resolve-MetricValue -MetricIndex $metricIndex -Names @("space4x.movement.naninf")
$stuck = Resolve-MetricValue -MetricIndex $metricIndex -Names @("space4x.movement.stuck")
$teleport = Resolve-MetricValue -MetricIndex $metricIndex -Names @("space4x.movement.teleport")
$carrierCount = Resolve-MetricValue -MetricIndex $metricIndex -Names @(
    "space4x.movement.observe.final.carrier.count",
    "space4x.movement.observe.carrier.count"
)
$carrierTimeToTarget = Resolve-MetricValue -MetricIndex $metricIndex -Names @(
    "space4x.movement.observe.final.carrier.time_to_target_s",
    "space4x.movement.observe.carrier.time_to_target_s"
)
$carrierTurnTime = Resolve-MetricValue -MetricIndex $metricIndex -Names @(
    "space4x.movement.observe.final.carrier.turn_time_s",
    "space4x.movement.observe.carrier.turn_time_s"
)
$carrierOvershoot = Resolve-MetricValue -MetricIndex $metricIndex -Names @(
    "space4x.movement.observe.final.carrier.overshoot_distance",
    "space4x.movement.observe.carrier.overshoot_distance"
)
$carrierSettleTime = Resolve-MetricValue -MetricIndex $metricIndex -Names @(
    "space4x.movement.observe.final.carrier.settle_time_s",
    "space4x.movement.observe.carrier.settle_time_s"
)
$headingOscillationScore = Resolve-MetricValue -MetricIndex $metricIndex -Names @("s4x.heading_oscillation_score")
$approachFlipRate = Resolve-MetricValue -MetricIndex $metricIndex -Names @("s4x.approach_mode_flip_rate")

if ($null -ne $naninf -and $naninf -gt (Get-Threshold -Path "tier1.movement.max_naninf" -Fallback 0.0)) {
    [void]$tier1Failures.Add("space4x.movement.naninf=$naninf exceeds threshold.")
}
if ($null -ne $stuck -and $stuck -gt (Get-Threshold -Path "tier1.movement.max_stuck" -Fallback 0.0)) {
    [void]$tier1Failures.Add("space4x.movement.stuck=$stuck exceeds threshold.")
}
if ($null -ne $teleport -and $teleport -gt (Get-Threshold -Path "tier1.movement.max_teleport" -Fallback 0.0)) {
    [void]$tier1Failures.Add("space4x.movement.teleport=$teleport exceeds threshold.")
}
if ($null -ne $carrierCount -and $carrierCount -lt (Get-Threshold -Path "tier1.movement.min_carrier_samples" -Fallback 1.0)) {
    [void]$tier1Failures.Add("carrier observe sample count is $carrierCount (expected >= 1).")
}
if ($null -ne $carrierTimeToTarget -and $carrierTimeToTarget -gt (Get-Threshold -Path "tier1.movement.max_carrier_time_to_target_s" -Fallback 95.0)) {
    [void]$tier1Failures.Add("carrier time_to_target_s=$carrierTimeToTarget exceeds tier1 threshold.")
}
if ($null -ne $carrierTurnTime -and $carrierTurnTime -gt (Get-Threshold -Path "tier1.movement.max_carrier_turn_time_s" -Fallback 9.0)) {
    [void]$tier1Failures.Add("carrier turn_time_s=$carrierTurnTime exceeds tier1 threshold.")
}

if ($null -ne $headingOscillationScore -and $headingOscillationScore -gt (Get-Threshold -Path "tier2.movement.max_heading_oscillation_score" -Fallback 4.0)) {
    [void]$tier2Warnings.Add("heading oscillation score is elevated ($headingOscillationScore).")
}
if ($null -ne $approachFlipRate -and $approachFlipRate -gt (Get-Threshold -Path "tier2.movement.max_approach_mode_flip_rate" -Fallback 2.5)) {
    [void]$tier2Warnings.Add("approach mode flip rate is elevated ($approachFlipRate).")
}
if ($null -ne $carrierOvershoot -and $carrierOvershoot -gt (Get-Threshold -Path "tier2.movement.max_carrier_overshoot_distance" -Fallback 35.0)) {
    [void]$tier2Warnings.Add("carrier overshoot distance is high ($carrierOvershoot).")
}
if ($null -ne $carrierSettleTime -and $carrierSettleTime -gt (Get-Threshold -Path "tier2.movement.max_carrier_settle_time_s" -Fallback 14.0)) {
    [void]$tier2Warnings.Add("carrier settle time is high ($carrierSettleTime).")
}

$cameraStats = [ordered]@{
    sample_count = 0
    eligible_sample_count = 0
    unaligned_count = 0
    unaligned_ratio = $null
    max_spin_deg_per_s = $null
    yaw_delta_p95_deg = $null
}

if (-not [string]::IsNullOrWhiteSpace($CameraProbePath) -and (Test-Path -LiteralPath $CameraProbePath)) {
    $lines = Get-Content -LiteralPath $CameraProbePath
    $yawValues = New-Object System.Collections.Generic.List[double]
    $spinValues = New-Object System.Collections.Generic.List[double]
    foreach ($line in $lines) {
        $trimmed = $line.Trim()
        if (-not $trimmed) { continue }
        $sample = $null
        try {
            $sample = $trimmed | ConvertFrom-Json -AsHashtable -Depth 32
        } catch {
            continue
        }

        $cameraStats.sample_count += 1
        $hasControlled = Coerce-BoolOrNull -Value (Get-PathValue -Source $sample -Path "has_controlled")
        $hasTarget = Coerce-BoolOrNull -Value (Get-PathValue -Source $sample -Path "has_target")
        if ($hasControlled -eq $true -and $hasTarget -eq $true) {
            $cameraStats.eligible_sample_count += 1
            $aligned = Coerce-BoolOrNull -Value (Get-PathValue -Source $sample -Path "aligned")
            if ($aligned -eq $false) {
                $cameraStats.unaligned_count += 1
            }
        }

        $yaw = Coerce-DoubleOrNull -Value (Get-PathValue -Source $sample -Path "yaw_delta_deg")
        if ($null -ne $yaw -and $yaw -ge 0) {
            [void]$yawValues.Add([Math]::Abs($yaw))
        }

        $spin = Coerce-DoubleOrNull -Value (Get-PathValue -Source $sample -Path "camera_spin_deg_s")
        if ($null -ne $spin) {
            [void]$spinValues.Add([Math]::Abs($spin))
        }
    }

    if ($cameraStats.eligible_sample_count -gt 0) {
        $cameraStats.unaligned_ratio = $cameraStats.unaligned_count / [double]$cameraStats.eligible_sample_count
    }
    if ($spinValues.Count -gt 0) {
        $cameraStats.max_spin_deg_per_s = ($spinValues | Measure-Object -Maximum).Maximum
    }
    if ($yawValues.Count -gt 0) {
        $cameraStats.yaw_delta_p95_deg = Get-Percentile -Values $yawValues.ToArray() -Percentile 0.95
    }

    if ($cameraStats.sample_count -lt (Get-Threshold -Path "tier1.camera.min_samples" -Fallback 20.0)) {
        [void]$tier2Warnings.Add("camera probe sample count is low ($($cameraStats.sample_count)); confidence reduced.")
    }

    if ($null -ne $cameraStats.unaligned_ratio) {
        if ($cameraStats.unaligned_ratio -gt (Get-Threshold -Path "tier1.camera.max_unaligned_ratio" -Fallback 0.02)) {
            [void]$tier1Failures.Add("camera unaligned ratio is $([Math]::Round($cameraStats.unaligned_ratio, 4)), above tier1 threshold.")
        } elseif ($cameraStats.unaligned_ratio -gt (Get-Threshold -Path "tier2.camera.max_unaligned_ratio_warn" -Fallback 0.005)) {
            [void]$tier2Warnings.Add("camera unaligned ratio is elevated ($([Math]::Round($cameraStats.unaligned_ratio, 4))).")
        }
    }

    if ($null -ne $cameraStats.max_spin_deg_per_s) {
        if ($cameraStats.max_spin_deg_per_s -gt (Get-Threshold -Path "tier1.camera.max_spin_deg_per_s" -Fallback 140.0)) {
            [void]$tier1Failures.Add("camera spin speed peaks at $([Math]::Round($cameraStats.max_spin_deg_per_s, 2)) deg/s.")
        } elseif ($cameraStats.max_spin_deg_per_s -gt (Get-Threshold -Path "tier2.camera.max_spin_deg_per_s_warn" -Fallback 90.0)) {
            [void]$tier2Warnings.Add("camera spin speed is elevated ($([Math]::Round($cameraStats.max_spin_deg_per_s, 2)) deg/s).")
        }
    }

    if ($null -ne $cameraStats.yaw_delta_p95_deg) {
        if ($cameraStats.yaw_delta_p95_deg -gt (Get-Threshold -Path "tier1.camera.max_yaw_delta_p95_deg" -Fallback 50.0)) {
            [void]$tier1Failures.Add("camera yaw delta p95 is $([Math]::Round($cameraStats.yaw_delta_p95_deg, 2)) deg (tier1 breach).")
        } elseif ($cameraStats.yaw_delta_p95_deg -gt (Get-Threshold -Path "tier2.camera.max_yaw_delta_p95_deg_warn" -Fallback 30.0)) {
            [void]$tier2Warnings.Add("camera yaw delta p95 is high ($([Math]::Round($cameraStats.yaw_delta_p95_deg, 2)) deg).")
        }
    }
}
elseif (-not [string]::IsNullOrWhiteSpace($CameraProbePath)) {
    [void]$tier2Warnings.Add("camera probe path not found ($CameraProbePath); camera tier checks skipped.")
}

$verdict = if ($tier1Failures.Count -gt 0) { "red" } elseif ($tier2Warnings.Count -gt 0) { "yellow" } else { "green" }
$recommendation = switch ($verdict) {
    "red" { "Block visual review. Fix Tier 1 issues first, then rerun nuisance filter." }
    "yellow" { "Proceed with focused visual review only on warned areas; capture deltas before tuning." }
    default { "Proceed to human feel pass; nuisance checks are stable." }
}

$output = [ordered]@{
    generated_at_utc = (Get-Date).ToUniversalTime().ToString("o")
    verdict = $verdict
    recommendation = $recommendation
    tier1 = [ordered]@{
        status = if ($tier1Failures.Count -gt 0) { "fail" } else { "pass" }
        failures = @($tier1Failures)
    }
    tier2 = [ordered]@{
        status = if ($tier2Warnings.Count -gt 0) { "warn" } else { "pass" }
        warnings = @($tier2Warnings)
    }
    tier3 = [ordered]@{
        status = "human_required"
        manual_checks = @(
            "Visual feel/readability pass (motion drama, composition, target clarity).",
            "Input feel pass (responsiveness, comfort, perceived control).",
            "Aesthetic coherence pass (fleet identity, contrast, and polish)."
        )
    }
    camera = $cameraStats
    movement_snapshot = [ordered]@{
        naninf = $naninf
        stuck = $stuck
        teleport = $teleport
        carrier_count = $carrierCount
        carrier_time_to_target_s = $carrierTimeToTarget
        carrier_turn_time_s = $carrierTurnTime
        carrier_overshoot_distance = $carrierOvershoot
        carrier_settle_time_s = $carrierSettleTime
        heading_oscillation_score = $headingOscillationScore
        approach_mode_flip_rate = $approachFlipRate
    }
    evidence = @($evidence)
}

$markdownLines = @(
    "# Space4X Presentation Nuisance Filter",
    "",
    "- verdict: $verdict",
    "- recommendation: $recommendation",
    "",
    "## Tier 1",
    "- status: $($output.tier1.status)"
)

if ($tier1Failures.Count -gt 0) {
    foreach ($failure in $tier1Failures) {
        $markdownLines += "- fail: $failure"
    }
} else {
    $markdownLines += "- fail: none"
}

$markdownLines += ""
$markdownLines += "## Tier 2"
$markdownLines += "- status: $($output.tier2.status)"
if ($tier2Warnings.Count -gt 0) {
    foreach ($warning in $tier2Warnings) {
        $markdownLines += "- warn: $warning"
    }
} else {
    $markdownLines += "- warn: none"
}

$markdownLines += ""
$markdownLines += "## Tier 3"
$markdownLines += "- status: human_required"
foreach ($manual in $output.tier3.manual_checks) {
    $markdownLines += "- manual: $manual"
}

$markdownLines += ""
$markdownLines += "## Camera Snapshot"
$markdownLines += "- sample_count: $($cameraStats.sample_count)"
$markdownLines += "- eligible_sample_count: $($cameraStats.eligible_sample_count)"
$markdownLines += "- unaligned_ratio: $($cameraStats.unaligned_ratio)"
$markdownLines += "- max_spin_deg_per_s: $($cameraStats.max_spin_deg_per_s)"
$markdownLines += "- yaw_delta_p95_deg: $($cameraStats.yaw_delta_p95_deg)"

$jsonText = $output | ConvertTo-Json -Depth 10
$mdText = ($markdownLines -join "`n") + "`n"

if (-not [string]::IsNullOrWhiteSpace($OutJsonPath)) {
    Ensure-DirectoryForFile -Path $OutJsonPath
    Set-Content -LiteralPath $OutJsonPath -Value $jsonText -Encoding utf8
}
if (-not [string]::IsNullOrWhiteSpace($OutMarkdownPath)) {
    Ensure-DirectoryForFile -Path $OutMarkdownPath
    Set-Content -LiteralPath $OutMarkdownPath -Value $mdText -Encoding utf8
}

$jsonText
