# Space4X Smoke Scenario Audit (2026-01-31)

## Existing Scenario Assets
- `Assets/Scenarios/space4x_smoke.json` – canonical mining + combat smoke scenario (used by headless proofs + diagnostics).
- `Assets/Scenarios/space4x_mining_combat.json` – legacy mining + combat proof (kept for targeted regression runs).
- `Assets/Scenarios/space4x_mining.json` / `space4x_refit.json` / `space4x_research_mvp.json` – specialized demos.
- `puredots/Packages/com.moni.puredots/Runtime/Runtime/Scenarios/Samples/space4x_smoke.json` – legacy sample (do not use for current smoke parity).

## How scenarios are selected / loaded
- **Headless CLI**: `Space4XScenarioEntryPoint` accepts `--scenario` or `SPACE4X_SCENARIO_PATH` and passes that to the runtime loader. Smoke proofs/diagnostics expect `space4x_smoke.json`.
- **Runtime loader**: `Space4XMiningScenarioSystem` reads `SPACE4X_SCENARIO_PATH`; if unset, it falls back to `ScenarioInfo` and resolves `space4x_smoke` under `Assets/Scenarios/`.
- **Presentation bootstrap**: `Space4XBootstrap` in the smoke scene should set/resolve `scenarioId = space4x_smoke` and create a `ScenarioInfo` entity so the mining scenario system can locate the JSON.

## Smoke scene wiring
- Scene: `Assets/Scenes/TRI_Space4X_Smoke.unity`
  - Must load the same SubScenes + registries as headless (no presentation-only spawners).
  - Diagnostics (`Space4XSmokeWorldCountsSystem`, `Space4XSmokePresentationCountsSystem`) already reference `space4x_smoke.json`.

## Observations / gaps
- The canonical smoke JSON now exists in `Assets/Scenarios/space4x_smoke.json`, and headless proofs/diagnostics are keyed to it.
- Verify that `TRI_Space4X_Smoke.unity` is present and serialized with `scenarioId = space4x_smoke` so editor smoke aligns with headless.
- Keep the smoke duration within the headless timeout budget (currently tuned to 75s to avoid 90s runner timeouts).

## Next steps (per plan)
1. Keep `space4x_smoke.json` as the authoritative smoke artifact and avoid reintroducing legacy `scenario.space4x.smoke` samples.
2. Confirm the smoke scene’s `Space4XBootstrap` points at `space4x_smoke`.
3. Ensure smoke diagnostics continue to reference the shared artifact.
