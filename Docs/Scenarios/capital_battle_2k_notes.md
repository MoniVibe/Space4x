# Capital Battle 2k vs 2k Notes

Scenario ID: `space4x_stress_capital_2k_vs_2k`
File: `Assets/Scenarios/space4x_stress_capital_2k_vs_2k.json`

This document exists to keep edits out of the large/minified JSON while still capturing expectations, failure modes, and tuning knobs.

## Runtime Expectations
- Duration: `duration_s` (currently 120s).
- Spawning: uses `spawn` list (many carriers) with `scenarioConfig.spawnLane` set for stress layout.
- Movement/stability helpers: `scenarioConfig.applyFloatingOrigin` should remain enabled for large coordinate spreads.
- Strike craft behavior: controlled by `dogfightConfig` and per-carrier `Combat` fields.

## Observability (What To Look At)
Headless question IDs (if enabled by the scenario’s headless question pack):
- `space4x.q.perf.summary`
- `space4x.q.perf.budget` (optional strict gate)
- `space4x.q.combat.attack_run` (only meaningful if strike craft exist)
- `space4x.q.collision.phasing`

Perf summary raw keys (expected in telemetry):
- `perf.fixed_step.ms.p50`, `perf.fixed_step.ms.p95`, `perf.fixed_step.ms.p99`, `perf.fixed_step.ms.max`
- `perf.structural.delta.p95`
- `perf.memory.reserved.bytes.peak`

## Known Failure Modes
- OOM / runaway allocations (watch `perf.memory.reserved.bytes.peak`).
- Fixed-step spikes causing budget failures or watchdog exits (watch `perf.fixed_step.ms.p95` and `space4x.q.perf.budget`).
- Structural churn loops (watch `perf.structural.delta.p95`).
- Strike craft degenerate loops (attack runs never observed, or dogfight thrash).
- Collision instability at high density (phasing blackcats, NaNs, or missing collision events).

## Suggested Knobs (Without Touching The Existing JSON)
When you need a new variant for iteration:
- Reduce `duration_s` to shorten iteration time while preserving telemetry signal.
- Tighten dogfight search space:
  - reduce `dogfightConfig.acquireRadius`
  - narrow `dogfightConfig.coneDegrees`
  - adjust `dogfightConfig.breakOffDistance` / `breakOffTicks`
  - reduce `dogfightConfig.jinkStrength` to cut oscillation
- Reduce per-carrier strike craft counts or disable intercept in a new variant to isolate capital-only behavior.

## Suggested Downscale Variants (To Debug 2k Failures)
The 2k JSON is large/minified. Do not reformat it. Instead, create a new variant file (new name, new `scenarioId`) and keep diffs surgical.

Suggested variant patterns:
- `space4x_stress_capital_2k_vs_2k_short.json`: same spawn layout, but shorter `duration_s` to reproduce failure faster.
- `space4x_stress_capital_2k_vs_2k_capital_only.json`: same carriers but disable intercept / set strike craft count to 0 to isolate capital loop + collisions.
- `space4x_stress_capital_2k_vs_2k_dogfight_simplified.json`: reduce `dogfightConfig.acquireRadius` and `coneDegrees`, and reduce `jinkStrength` to damp oscillation.
- `space4x_stress_capital_500_vs_500_spawn.json`: generate a smaller spawn-based variant (same schema shape as 2k) so you can iterate without huge diffs.

Minimum doc hygiene for each new variant:
- Add it to `Docs/Scenarios/capital_battle_index.md` with a clear intended purpose.
- Declare which gates you expect (perf summary, perf budget, collision phasing, attack run if strike craft enabled).

## Copy/Paste Run Command (Headless)
Run from the Space4X project root (`space4x/`):

```powershell
cd space4x
Unity -batchmode -nographics -projectPath . -logFile Logs/capital_battle_2k.log --scenario Assets/Scenarios/space4x_stress_capital_2k_vs_2k.json --report Reports/capital_battle_2k_report.json
```

Notes:
- `--scenario` is consumed by `Space4XScenarioEntryPoint` and sets `SPACE4X_SCENARIO_PATH`.
- `--report` writes a report path and will typically cause telemetry paths to be derived from it when diagnostics are not enabled.
