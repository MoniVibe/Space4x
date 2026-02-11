# Goal Card: Capital Battle Ramp (Super Green to 100v100)
ID: capital_100v100_ramp_v0
Date: 2026-02-11
Owner: shonh
Status: active

## Goal
Establish a two-step battle validation path: pass a fast "super green" combat gate first, then run a proper 100 vs 100 capital battle with deterministic summary metrics.

## Hypotheses
- A short 20 vs 20 run can fail quickly on no-combat/no-attrition regressions.
- A 100 vs 100 run can provide stable battle summary outputs for winner and attrition tracking.
- Shared headless question IDs can be used as canonical gates for both steps.

## Scenario Frame
Theme: staged readiness ramp for large-fleet combat.
Why this scenario matters: it cuts wasted queue time by proving combat liveness early before spending budget on the full 100 vs 100 validation.

## Setup
Map/Scene: open-space combat lane.
Actors: two opposing capital fleets represented by two carrier anchors.
Equipment/Loadouts: default module loadouts.
Rules/Constraints: fixed seed, mirrored approach, no manual intervention.
Duration:
- super green: 45 seconds.
- proper 100 vs 100: 120 seconds.

## Script
1. Run `space4x_capital_20_vs_20_supergreen`.
2. If green, run `space4x_capital_100_vs_100_proper`.
3. Compare battle summary metrics and winner consistency across runs/seeds.

## Metrics
- `shots_fired_total`: combat liveness.
- `shots_hit_total`: engagement confirmation.
- `combatants_total`: expected battle scale.
- `combatants_destroyed`: attrition signal.
- `outcome_total_alive`: surviving force size.
- `winner_side`: side index for winner.
- `winner_alive`: remaining winner strength.
- `winner_ratio`: winner normalized survivability.

## Scoring
- Super green pass if liveness and attrition are observed.
- Proper pass if summary metrics are present and within expected envelopes.

## Acceptance
- Super green scenario must pass:
  - `space4x.q.combat.fire`
  - `space4x.q.combat.battle_summary`
- Super green fails fast when:
  - no shots are fired, or
  - no impact signal is observed (no hits, hull damage, critical damage, or destroyed combatants).
- Attrition remains a tracked metric in summary output but is not a hard fail in short smoke windows.
- Proper 100 vs 100 scenario must pass:
  - `space4x.q.combat.fire`
  - `space4x.q.combat.battle_summary`
- Performance question IDs remain optional for now:
  - `space4x.q.perf.summary`
  - `space4x.q.perf.budget`

## Regression Guardrails
- Keep deterministic seeds fixed for baseline runs.
- Do not remove battle-summary metrics from telemetry.
- Do not change question IDs once wired into nightly gates.

## Nightly Focus
Scenario IDs:
- `space4x_capital_20_vs_20_supergreen`
- `space4x_capital_100_vs_100_proper`
Run budget:
- 45s then 120s
Telemetry IDs:
- `space4x.q.combat.fire`
- `space4x.q.combat.battle_summary`
- `space4x.q.perf.summary` (optional)
- `space4x.q.perf.budget` (optional)

## Dependencies
- `Space4XHeadlessQuestionRegistry` support for `space4x.q.combat.battle_summary`.
- Combat telemetry emission for shots/combatants/outcome metrics.

## Risks/Notes
- `shipCount` is represented on fleet components, so full physical 100-per-side entity materialization should be verified with telemetry before tightening gates.
- Keep this as a spike card until 100 vs 100 stability is demonstrated over multiple seeds.

## Scenario JSON
Path: Assets/Scenarios/space4x_capital_20_vs_20_supergreen.json
Version: v0

Path: Assets/Scenarios/space4x_capital_100_vs_100_proper.json
Version: v0
