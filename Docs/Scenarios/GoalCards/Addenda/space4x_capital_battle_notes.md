# Capital Battle Notes (Addendum)
Date: 2026-02-12
Status: draft

This addendum is the "operating manual" for extending Capital Battle without conflicts.

## What Capital Battle Is
Capital Battle is a scenario suite with two purposes:
- Stress test: find perf cliffs, memory cliffs, structural-change spikes, and stability hazards under large engagements.
- Feature test bed: a shared harness to validate behaviors (targeting, strike craft loops, comms/sensors, collision robustness) with stable telemetry gates.

## Authoritative Scenarios (Do Not Reformat)
- `space4x_stress_capital_100_vs_100`
- `space4x_stress_capital_200_vs_200`
- `space4x_stress_capital_2k_vs_2k`

These are referenced by the inventory index and other agents. If you need to change one, treat it like an API change: claim ownership and coordinate.

## Extension Protocol (Agent-Friendly)
When adding a new Capital Battle variant:
1) Create a new scenario JSON with a new `scenarioId` and filename.
2) Add a row in `Docs/Scenarios/capital_battle_index.md` with purpose + tags.
3) Declare which headless question IDs you expect to gate on (and whether they are required).
4) If you add new telemetry keys:
   - Reserve the namespace in the goal card first.
   - Prefer stable keys; never encode transient entity IDs into metric keys.

Naming suggestions (non-binding):
- Scenario IDs: `space4x_stress_capital_<N>_vs_<N>_<intent>`
- Headless questions: `space4x.q.capital_battle.<slice>.<metric>`
- Raw metrics: `space4x.capital_battle.<slice>.<metric>`

## Recommended "Feature Test Bed" Checklist
Run-specific: mark each item as `present`, `missing`, or `blocked`.

Targeting / command
- Target selection stable under load (no oscillation/thrash)
- Strike craft intercept intent is expressed (launch + converge + break-off/rejoin) when enabled
- Command overrides do not drop fleets into idle unexpectedly

Formations / fleet coherence
- Formation adherence is visible and stable when implemented
- Regroup behavior converges; no infinite orbiting

Morale / cohesion / doctrine expression
- Morale/cohesion hooks are exercised (friendly fire, prolonged combat) when implemented
- Doctrine/profile tags are observable in tactical choices when implemented

Combat loop
- Target selection stable (no oscillation/thrash)
- Damage applied consistently (no invulnerable targets, no negative HP)
- Disable/kill transitions clean (no zombie entities)

Strike craft / dogfight
- Launch cadence stable; does not stall at high counts
- Attack runs observed (see `space4x.q.combat.attack_run`)
- Break-off/rejoin logic does not spiral into NaNs

Sensors / comms (only when enabled)
- Contacts acquire/drop without stale/ghost behavior (see `space4x.q.sensors.acquire_drop`)
- Comms delivery ratio sane, latency bounded (see `space4x.q.comms.delivery`)

Collision / physics
- No collision phasing blackcats; collision events recorded (see `space4x.q.collision.phasing`)
- No NaN velocities; no tunneling impulses

Performance / memory
- `space4x.q.perf.summary` not `UNKNOWN`
- `perf.fixed_step.ms.p95` tracked and comparable across runs
- Structural churn bounded (`perf.structural.delta.p95`)
- Peak reserved memory tracked (`perf.memory.reserved.bytes.peak`)
- Optional strict gate: `space4x.q.perf.budget` must PASS

## Known Failure Modes (What To Watch)
- Telemetry missing/truncated: perf summary becomes `UNKNOWN` (insufficient samples or missing `perf.*` keys).
- Budget failure: `space4x.q.perf.budget` FAIL with a concrete `metric=... observed=... budget=...`.
- Structural churn spikes: `perf.structural.delta.p95` jumps, indicating a pathological structural-change loop.
- Collision instability: phasing blackcats or no collision events in a scenario intended to collide.
- Strike craft stalls: `space4x.combat.attack_run_seen` stays 0 even when strike craft exist.

## Schema Notes
Scenarios may include `"$schema": "schemas/scenario.schema.json"` relative to `Assets/Scenarios/`.
The schema is intentionally permissive to avoid forcing edits across existing scenarios.
