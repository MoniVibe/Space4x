# Space4X 100v100 Runbook

Scenario file: `Assets/Scenarios/space4x_100v100.json`

## Editor Run (TRI_Space4X_Smoke)

1. Open scene `Assets/Scenes/TRI_Space4X_Smoke.unity`.
2. Set environment variable:
   - PowerShell: `$env:SPACE4X_SCENARIO_PATH = "Assets/Scenarios/space4x_100v100.json"`
3. Enter Play mode.

Notes:
- `Space4XAutoRenderCatalogBootstrap` auto-loads `Assets/Resources/Space4XRenderCatalog_v2.asset` when needed.
- The scenario uses only `Space4XMiningScenarioSystem` JSON spawning paths (no fallback/fake spawners).

## Headless Run

```bash
Space4X_Headless.x86_64 --scenario Assets/Scenarios/space4x_100v100.json --report Reports/space4x_100v100_metrics.json
```

## Acceptance Checks

- Loader:
  - `[Space4XMiningScenario] Loaded '...space4x_100v100.json'`
- Spawn summary:
  - `SCENARIO_SPAWN ... carriers_spawned=200`
- World counts:
  - `[Space4XSmokeWorldCounts] ... Catalog=True ... Carrier=200 ... MiningVessel=400 ...`
- Presentation counts:
  - `[Space4XSmokePresentationCounts] ... MaterialMeshInfo=...` (must be `> 0`)
- Visual sanity in smoke scene:
  - Carriers render (capsule silhouette).
  - Miners render (cube silhouette).
  - Strike craft render (capsule silhouette).
  - Deposits render (sphere silhouette).
