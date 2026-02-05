param(
    [string]$RepoRoot = (Get-Location).Path,
    [string]$OutFile = "",
    [switch]$IncludePackages
)

$resolvedRoot = (Resolve-Path $RepoRoot).Path
$scenarioRoots = @()

$assetScenarios = Join-Path $resolvedRoot "Assets\\Scenarios"
if (Test-Path $assetScenarios) {
    $scenarioRoots += $assetScenarios
}

if ($IncludePackages) {
    $packagesRoot = Join-Path $resolvedRoot "Packages"
    if (Test-Path $packagesRoot) {
        Get-ChildItem -Path $packagesRoot -Directory | ForEach-Object {
            $candidate = Join-Path $_.FullName "Scenarios"
            if (Test-Path $candidate) {
                $scenarioRoots += $candidate
            }
        }
    }
}

function Get-ScenarioId([string]$path) {
    try {
        $content = Get-Content -Path $path -Raw -ErrorAction Stop
    } catch {
        return $null
    }
    if ($content -match '"scenarioId"\s*:\s*"([^"]+)"') {
        return $Matches[1]
    }
    return $null
}

$items = New-Object System.Collections.Generic.List[object]
$missing = 0

foreach ($root in $scenarioRoots) {
    Get-ChildItem -Path $root -Filter *.json -Recurse | ForEach-Object {
        if ($_.FullName -match '\\Templates\\') { return }
        if ($_.Name -eq 'README.json') { return }

        $scenarioId = Get-ScenarioId $_.FullName
        if (-not $scenarioId) { $missing++ }
        $relative = $_.FullName.Substring($resolvedRoot.Length + 1)

        $items.Add([pscustomobject]@{
            scenario_id = $scenarioId
            scenario_rel = $relative
        }) | Out-Null
    }
}

if ($missing -gt 0) {
    Write-Warning ("{0} scenario file(s) missing scenarioId." -f $missing)
}

if ([string]::IsNullOrWhiteSpace($OutFile)) {
    $items | Sort-Object scenario_id, scenario_rel | Format-Table -AutoSize
    return
}

$outDir = Split-Path -Parent $OutFile
if (-not [string]::IsNullOrWhiteSpace($outDir)) {
    New-Item -ItemType Directory -Force -Path $outDir | Out-Null
}

$items | ConvertTo-Json -Depth 4 | Set-Content -Path $OutFile
