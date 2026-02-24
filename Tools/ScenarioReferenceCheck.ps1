param(
    [string]$TriRoot = "C:\dev\Tri",
    [string]$ConsolePath = "C:\dev\Tri\console.md",
    [int]$TailLines = 5000
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Section([string]$name) {
    Write-Host ""
    Write-Host "=== $name ==="
}

function Get-GitInfo([string]$repoPath) {
    if (-not (Test-Path $repoPath)) {
        return $null
    }

    try {
        $branch = (git -C $repoPath rev-parse --abbrev-ref HEAD).Trim()
        $head = (git -C $repoPath rev-parse --short HEAD).Trim()
        $status = (git -C $repoPath status --short --branch | Select-Object -First 1).Trim()
        $originMain = (git -C $repoPath rev-parse --short origin/main 2>$null).Trim()
        return [pscustomobject]@{
            Repo = $repoPath
            Branch = $branch
            Head = $head
            OriginMain = $originMain
            Status = $status
        }
    } catch {
        return $null
    }
}

$space4xRepo = Join-Path $TriRoot "space4x_ultimate"
if (-not (Test-Path $space4xRepo)) {
    $space4xRepo = Join-Path $TriRoot "space4x"
}

$puredotsRepo = Join-Path $TriRoot "puredots_ultimate"
if (-not (Test-Path $puredotsRepo)) {
    $puredotsRepo = Join-Path $TriRoot "puredots"
}
if (-not (Test-Path $puredotsRepo)) {
    $puredotsRepo = Join-Path $space4xRepo "PureDOTS"
}

Write-Section "Git Refs"
$space4x = Get-GitInfo $space4xRepo
$puredots = Get-GitInfo $puredotsRepo

if ($null -ne $space4x) {
    Write-Host "space4x repo      : $($space4x.Repo)"
    Write-Host "space4x branch    : $($space4x.Branch)"
    Write-Host "space4x head      : $($space4x.Head)"
    Write-Host "space4x origin/main: $($space4x.OriginMain)"
    Write-Host "space4x status    : $($space4x.Status)"
} else {
    Write-Host "space4x repo not found or git unavailable."
}

if ($null -ne $puredots) {
    Write-Host "puredots repo     : $($puredots.Repo)"
    Write-Host "puredots branch   : $($puredots.Branch)"
    Write-Host "puredots head     : $($puredots.Head)"
    Write-Host "puredots origin/main: $($puredots.OriginMain)"
    Write-Host "puredots status   : $($puredots.Status)"
} else {
    Write-Host "puredots repo not found or git unavailable."
}

Write-Section "Scenario Env"
$mode = [Environment]::GetEnvironmentVariable("SPACE4X_MODE")
$scenarioPathEnv = [Environment]::GetEnvironmentVariable("SPACE4X_SCENARIO_PATH")
Write-Host "SPACE4X_MODE        : $mode"
Write-Host "SPACE4X_SCENARIO_PATH: $scenarioPathEnv"
if (-not [string]::IsNullOrWhiteSpace($scenarioPathEnv)) {
    if (Test-Path $scenarioPathEnv) {
        Write-Host "Scenario path exists : YES ($scenarioPathEnv)"
    } else {
        Write-Host "Scenario path exists : NO  ($scenarioPathEnv)"
    }
}

Write-Section "Canonical Scenario Files"
$corePath = Join-Path $space4xRepo "Assets\Scenarios\space4x_fleetcrawl_core_micro.json"
$smokePath = Join-Path $space4xRepo "Assets\Scenarios\space4x_smoke.json"
Write-Host "fleetcrawl core : $(if (Test-Path $corePath) { 'present' } else { 'missing' }) ($corePath)"
Write-Host "smoke           : $(if (Test-Path $smokePath) { 'present' } else { 'missing' }) ($smokePath)"

Write-Section "Bootstrap Assets"
$scenarioUrp = Join-Path $space4xRepo "Assets\Resources\Rendering\ScenarioURP.asset"
$scenarioUrpMeta = Join-Path $space4xRepo "Assets\Resources\Rendering\ScenarioURP.asset.meta"
$scenarioRenderer = Join-Path $space4xRepo "Assets\Resources\Rendering\ScenarioURP_Renderer.asset"
$scenarioRendererMeta = Join-Path $space4xRepo "Assets\Resources\Rendering\ScenarioURP_Renderer.asset.meta"
Write-Host "ScenarioURP.asset         : $(if (Test-Path $scenarioUrp) { 'present' } else { 'missing' }) ($scenarioUrp)"
Write-Host "ScenarioURP.asset.meta    : $(if (Test-Path $scenarioUrpMeta) { 'present' } else { 'missing' }) ($scenarioUrpMeta)"
Write-Host "ScenarioURP_Renderer.asset: $(if (Test-Path $scenarioRenderer) { 'present' } else { 'missing' }) ($scenarioRenderer)"
Write-Host "ScenarioURP_Renderer.meta : $(if (Test-Path $scenarioRendererMeta) { 'present' } else { 'missing' }) ($scenarioRendererMeta)"

Write-Section "Smoke Scene SRP Fallback"
$smokeScenePath = Join-Path $space4xRepo "Assets\Scenes\TRI_Space4X_Smoke.unity"
if ((Test-Path $smokeScenePath) -and (Test-Path $scenarioUrpMeta)) {
    $sceneLine = Select-String -Path $smokeScenePath -Pattern "fallbackAsset:" | Select-Object -First 1
    $fallbackGuid = $null
    if ($sceneLine -and $sceneLine.Line -match "guid: ([0-9a-f]{32})") {
        $fallbackGuid = $Matches[1]
    }
    $metaGuid = (Select-String -Path $scenarioUrpMeta -Pattern "^guid:" | Select-Object -First 1).Line
    $metaGuid = $metaGuid -replace "guid:\s*", ""
    Write-Host "Smoke fallbackAsset guid: $fallbackGuid"
    Write-Host "ScenarioURP.meta guid  : $metaGuid"
    if ($fallbackGuid -and $metaGuid -and ($fallbackGuid -ne $metaGuid)) {
        Write-Host "WARNING: Smoke scene fallbackAsset guid does not match ScenarioURP.asset.meta"
    }
} else {
    Write-Host "Smoke scene or ScenarioURP meta missing; cannot verify fallback."
}

Write-Section "Render Catalog Parity"
$catalogData = Join-Path $space4xRepo "Assets\Data\Space4XRenderCatalog_v2.asset"
$catalogResource = Join-Path $space4xRepo "Assets\Resources\Space4XRenderCatalog_v2.asset"
if ((Test-Path $catalogData) -and (Test-Path $catalogResource)) {
    $dataContent = Get-Content -Path $catalogData -Raw
    $resourceContent = Get-Content -Path $catalogResource -Raw
    $identical = $dataContent -eq $resourceContent
    Write-Host "RenderCatalog Data/Resources identical: $identical"
} else {
    Write-Host "RenderCatalog asset missing (data or resources)."
}

Write-Section "FleetCrawl Scenario Flags"
if (Test-Path $corePath) {
    try {
        $json = Get-Content -Path $corePath -Raw | ConvertFrom-Json
        $applyFrames = $json.scenarioConfig.applyReferenceFrames
        $bandEnabled = $json.scenarioConfig.orbitalBand.enabled
        $useBandScale = $json.scenarioConfig.renderFrame.useBandScale
        Write-Host "applyReferenceFrames: $applyFrames"
        Write-Host "orbitalBand.enabled : $bandEnabled"
        Write-Host "renderFrame.useBandScale: $useBandScale"
    } catch {
        Write-Host "Failed to parse $corePath for scenarioConfig."
    }
}

Write-Section "Console Signal"
if (Test-Path $ConsolePath) {
    $patterns = @(
        "\[Space4XScenarioRef\]",
        "\[Space4XRunStart\]",
        "\[Space4XRunStartScenarioSelector\]",
        "\[Space4XSmokeScenarioSelector\]",
        "\[Space4XMiningScenario\]",
        "ObjectDisposedException",
        "NullReferenceException",
        "InvalidOperationException"
    )

    $consoleTail = Get-Content -Path $ConsolePath -Tail $TailLines
    $hits = @($consoleTail | Select-String -Pattern $patterns | Select-Object -Last 30)
    if ($hits.Count -eq 0) {
        Write-Host "No scenario/error signal found in $ConsolePath."
    } else {
        $hits | ForEach-Object { Write-Host $_.Line }
    }
} else {
    Write-Host "Console file not found: $ConsolePath"
}

Write-Section "Done"
Write-Host "Scenario reference check complete."
