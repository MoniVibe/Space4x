# Space4X Smoke Scenario Audit (2025-12-23)

## Existing Scenario Assets
- `space4x/Assets/Scenarios/space4x_mining_combat.json` – current canonical mining + combat proof used by headless runs (`seed`, `duration_s`, `spawn`, `actions`, telemetry expectations).
- `space4x/Assets/Scenarios/space4x_mining.json` / `space4x_refit.json` / `space4x_research_mvp.json` – specialized demos for mining-only, refit, research flows.
- `puredots/Packages/com.moni.puredots/Runtime/Runtime/Scenarios/Samples/space4x_smoke.json` – legacy sample referenced by `scenarioId = "scenario.space4x.smoke"` but not aligned with current mining-combat parity.

## How scenarios are selected / loaded
- **Headless CLI**: `Space4XScenarioEntryPoint` listens for `--scenario Assets/Scenarios/...json` or `SPACE4X_SCENARIO_PATH`. When the JSON looks like a mining scenario it sets `SPACE4X_SCENARIO_PATH` and lets gameplay systems handle the rest.
- **Runtime loader**: `Space4XMiningScenarioSystem`
  - Reads `SPACE4X_SCENARIO_PATH`; if unset, falls back to `ScenarioInfo.ScenarioId` (injected by `Space4XBootstrap` Mono) and looks under `Assets/Scenarios/{ScenarioId}.json`.
  - Parses JSON and spawns carriers / miners / resource deposits, schedules scripted actions, and writes `Space4XScenarioRuntime`.
  - Requires `TimeState` and registry SubScenes (resources, fleets, comms) to be loaded.
- **Presentation bootstrap**: `Space4XBootstrap` (in smoke scene) currently defaults to `scenario.space4x.smoke`, which maps to the sample JSON under `PureDOTS/.../Samples`. This works for older demos but diverges from the canonical mining-combat JSON unless the component is overridden in the scene.

## Smoke scene wiring
- Scene: `Assets/Scenes/TRI_Space4X_Smoke.unity`
  - Contains `Space4XBootstrap`, registry SubScenes, presentation systems, and diagnostics (`Space4XSmokeWorldCountsSystem`, `Space4XSmokePresentationCountsSystem`).
  - Expected to visualize the same scenario used by headless CLI (`space4x_mining_combat.json`), but the serialized `Space4XBootstrap` still points at the legacy sample ID unless manually updated.
- Diagnostics:
  - `Space4XSmokeWorldCountsSystem` warns when carriers/miners are missing (“Expected from space4x_mining_combat.json scenario”).
  - `Space4XSmokeFallbackSpawnerSystem` logs if fallback spawners kick in (parity violation).

## Observations / gaps
- Presentation side (`Space4XBootstrap.scenarioId`) references `scenario.space4x.smoke`, whereas headless uses `space4x_mining_combat.json`. There is no single shared artifact analogous to Godgame’s `godgame_smoke.json`.
- The env-var path only gets set when `--scenario` is passed; opening `TRI_Space4X_Smoke` in the Editor does **not** automatically load `space4x_mining_combat.json` unless the Mono is updated or the user sets `ScenarioInfo`/env manually.
- Multiple JSONs exist for legacy demos; no authoritative “smoke” JSON that everyone iterates on.

## Next steps (per plan)
1. Author a dedicated smoke JSON (e.g., `space4x_smoke.json`) that captures the mining + combat showcase and lives under `space4x/Assets/Scenarios/`.
2. Update `Space4XBootstrap` defaults + serialized scene data (and/or add upgrade logic) so both presentation and headless fall back to the same JSON.
3. Ensure diagnostics (`Space4XSmokeWorldCountsSystem`, `Space4XSmokePresentationCountsSystem`) refer to the shared artifact.
4. Document the workflow (`Docs/Headless/HEADLESS_PROGRESS.md`, runbook) so stale progress reports can be archived as new smoke beats land.

