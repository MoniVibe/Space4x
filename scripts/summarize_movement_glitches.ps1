param(
    [Parameter(Mandatory = $true)]
    [string]$Path
)

if (-not (Test-Path -LiteralPath $Path)) {
    Write-Error "File not found: $Path"
    exit 1
}

$records = New-Object System.Collections.Generic.List[object]
Get-Content -LiteralPath $Path | ForEach-Object {
    $line = $_.Trim()
    if ([string]::IsNullOrWhiteSpace($line)) {
        return
    }

    try {
        $obj = $line | ConvertFrom-Json
        $records.Add($obj)
    }
    catch {
        # Ignore malformed lines so one bad event does not break summary.
    }
}

if ($records.Count -eq 0) {
    Write-Host "No readable glitch records in $Path"
    exit 0
}

Write-Host "Records: $($records.Count)"
Write-Host ""
Write-Host "By kind/source:"

$records |
    Group-Object kind, likely_source |
    Sort-Object Count -Descending |
    Select-Object -First 12 |
    ForEach-Object {
        $sample = $_.Group | Select-Object -First 1
        "{0,5}  {1}  ({2})" -f $_.Count, $sample.kind, $sample.likely_source
    } |
    Write-Host

Write-Host ""
Write-Host "Top gap offenders:"
$records |
    Sort-Object {[double]($_.ltw_gap)} -Descending |
    Select-Object -First 8 timestamp_utc, kind, likely_source, entity, ltw_gap, local_step, world_step, tick, speed_multiplier |
    Format-Table -AutoSize

Write-Host ""
Write-Host "Top jump offenders:"
$records |
    Sort-Object {[double]($_.local_step)} -Descending |
    Select-Object -First 8 timestamp_utc, kind, likely_source, entity, local_step, expected_step, tick_delta, input_backlog, speed_multiplier |
    Format-Table -AutoSize
