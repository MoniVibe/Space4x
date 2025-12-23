# Headless & Presentation Progress (updated 2025-12-23)

## Canonical Smoke Scenario
- **Scenario artifact**: `Assets/Scenarios/space4x_smoke.json`
  - Based on the mining+combat proof (two friendly carriers, one hostile carrier, scripted intercept action, multiple deposits).
  - Shared between headless CLI + `TRI_Space4X_Smoke` via `ScenarioInfo` / `SPACE4X_SCENARIO_PATH`.
- **Scenario injection**:
  - Headless CLI: `Unity -batchmode ... --scenario Assets/Scenarios/space4x_smoke.json --report Reports/space4x_smoke_metrics.json`
    - `Space4XScenarioEntryPoint` sets `SPACE4X_SCENARIO_PATH` and disables redundant headless proofs.
  - Presentation scene: `Space4XBootstrap` now resolves legacy IDs to `space4x_smoke` and creates a `ScenarioInfo` entity so the mining scenario system can locate the JSON automatically.

## Smoke Scene Expectations
- Scene: `Assets/Scenes/TRI_Space4X_Smoke.unity`
  - Loads the same SubScenes + registries as headless (no presentation-only spawners).
  - Diagnostics:
    - `Space4XSmokeWorldCountsSystem` logs entity counts and warns when carriers/miners/strike craft are missing (reference text now points to `space4x_smoke.json`).
    - `Space4XSmokePresentationCountsSystem` cross-checks RenderSemanticKeys vs. registries.
    - `Space4XSmokeFallbackSpawnerSystem` reports parity violations if fallback entities appear.
  - Camera: if no authored rig exists, `Space4XCameraBootstrap` spawns a placeholder controller (WASD/QE move, RMB orbit, MMB pan, scroll zoom) and publishes a `CameraRigState` for DOTS systems.

## Mirroring Workflow
1. **Headless first**
   - Implement new mechanics/behaviors in the shared scenario or systems.
   - Run the CLI command above; inspect logs for `[Space4XMiningScenario] Loaded '...space4x_smoke.json'` and telemetry exports.
2. **Presentation follow-up**
   - Add semantic keys / renderers for the new entities.
   - Extend smoke diagnostics or overlays to surface the behavior—never spawn fake entities in the scene.
3. **Documentation**
   - Update this file (and archive the previous snapshot) whenever a new smoke beat lands (≤4 days old).

## Quick Validation Checklist
- CLI output should mention `space4x_smoke.json`, spawn counts, and `Space4XScenarioRuntime` start/end ticks.
- In Editor, open `TRI_Space4X_Smoke` → confirm `Space4XBootstrap` inspector shows `scenarioId = space4x_smoke`.
- Watch the Console for:
  - `[Space4XSmokeWorldCounts] Phase=Initial ...` with non-zero carriers/miners/asteroids.
  - Absence of fallback warnings.
- (Optional) Run the smoke PlayMode tests (`Space4XSmokeTests`) to validate SubScene + diagnostic wiring.
