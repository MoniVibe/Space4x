# Goal Card: Capital Battle (Stress + Feature Test Bed)
ID: capital_battle_suite_v0
Date: 2026-02-12
Owner: shonh
Status: draft

## Goal
Codify "Capital Battle" as a dual-purpose scenario suite:
1) A repeatable performance/stability stress test for large capital engagements.
2) A behavior/feature test bed with a clear checklist + telemetry gates so multiple agents can extend it without overlap.

## Scenario Set (Authoritative IDs)
- `space4x_stress_capital_100_vs_100` (stress + behavior probe baseline)
- `space4x_stress_capital_200_vs_200` (stress escalation)
- `space4x_stress_capital_2k_vs_2k` (scale wall / perf ceiling)

## Scenario IDs
Scenario ID: `space4x_stress_capital_100_vs_100`
Scenario ID: `space4x_stress_capital_200_vs_200`
Scenario ID: `space4x_stress_capital_2k_vs_2k`

## Scenario Frame
Theme: Two opposing capital fleets collide at scale; the sim must remain stable, deterministic, and observable.
Why this scenario matters: It becomes the default "big fight" harness where we validate perf budgets and feature behaviors (target selection, strike craft loops, comms/sensors beats, collision robustness).

## No-Overlap Guardrails
- Treat the existing `space4x_stress_capital_*` JSONs as owned artifacts; extend the suite via docs, schema, and new variants only when explicitly claimed.
- Add new variants with new filenames and document them in `Docs/Scenarios/capital_battle_index.md`.
- Reserve telemetry namespaces before adding new probes: prefer `space4x.q.capital_battle.*` for headless questions and `space4x.capital_battle.*` for raw metric keys.

## Behaviors / Features Exercised (Test Bed Checklist)
- Combat loop: target selection stability, weapon cadence, damage application, disable/kill handling.
- Targeting: stable target choice under load, no target thrash oscillation.
- Formations: formation adherence and regroup behavior (when implemented).
- Intercept: strike craft intercept launch/cadence and intercept completion (when enabled).
- Morale/cohesion hooks: morale/cohesion signals change under friendly fire and prolonged engagement (when implemented).
- Doctrine expression: profile/doctrine affects tactical choices (when implemented).
- Strike craft: spawn/launch cadence, dogfight acquire/break-off/rejoin stability, wing directives.
- Sensors + comms: contact churn, delivery ratios/latency, wrong-transport diagnostics (when enabled).
- Movement: steering stability at scale (no NaN, no oscillation), formation coherence (when implemented).
- Collision/physics: no phasing, no tunneling explosions, no NaN velocities; collision event volume stays bounded.
- Performance + memory: fixed-step time distribution remains observable; structural change spikes are detectable.
- Observability: telemetry present and not truncated; key questions produce PASS/FAIL/UNKNOWN deterministically.

## Metrics / Telemetry (Current, Concrete)
Headless question IDs already present in runtime:
- `space4x.q.perf.summary` (expects `perf.fixed_step.ms.*`, `perf.structural.delta.p95`, `perf.memory.reserved.bytes.peak`)
- `space4x.q.perf.budget` (PASS unless `PerformanceBudgetStatus.HasFailure != 0`)
- `space4x.q.combat.attack_run` (expects `space4x.combat.*` strike craft / attack run signals)
- `space4x.q.comms.delivery` (expects `space4x.comms.*` ratios + latency)
- `space4x.q.sensors.acquire_drop` (expects `space4x.sensor.*` acquire/drop/stale signals)
- `space4x.q.collision.phasing` (expects `space4x.collision.event_count` and no `COLLISION_PHASING` blackcats)

Perf summary raw keys referenced by the headless question:
- `perf.fixed_step.ms.p50`, `perf.fixed_step.ms.p95`, `perf.fixed_step.ms.p99`, `perf.fixed_step.ms.max`
- `perf.structural.delta.p95`
- `perf.memory.reserved.bytes.peak`
- `perf.samples.tick_count`, `perf.samples.structural_count`

Combat/comms/sensors raw keys referenced by headless questions:
- `space4x.combat.strikecraft_seen`, `space4x.combat.attack_run_seen`, `space4x.combat.cap_seen`
- `space4x.combat.wing_directive_seen`, `space4x.combat.strikecraft_max`, `space4x.combat.attack_run_max_active`, `space4x.combat.cap_max_active`
- `space4x.comms.sent`, `space4x.comms.emitted`, `space4x.comms.received`, `space4x.comms.delivery_ratio`, `space4x.comms.emit_ratio`
- `space4x.comms.first_latency_ticks`, `space4x.comms.max_inbox_depth`, `space4x.comms.max_outbox_depth`
- `space4x.sensor.acquire_detected_ratio`, `space4x.sensor.drop_detected_ratio`, `space4x.sensor.toggle_count`
- `space4x.sensor.stale_samples`, `space4x.sensor.sample_count`
- `space4x.collision.event_count`

## Scoring
This suite is primarily gate-based (pass/fail/unknown). When scoring is needed:
- Primary score: `perf.fixed_step.ms.p95` (lower is better) with a hard budget gate.
- Secondary score: structural churn (`perf.structural.delta.p95`) and peak reserved memory.

## Acceptance / Pass Gates (Suite-Level)
Common gates across all Capital Battle variants:
- No crash, no hard hang, no out-of-memory.
- `space4x.q.perf.summary` is not `UNKNOWN` (requires enough samples and perf metrics present).
- `space4x.q.perf.budget` is `PASS` for runs tagged as "budgeted" (perf gate mode or strict runs).
- If strike craft are present: `space4x.q.combat.attack_run` is `PASS` (attack runs observed).
- If comms/sensors beats are enabled for the run: `space4x.q.comms.delivery` and `space4x.q.sensors.acquire_drop` must not be `FAIL`.
- Collision robustness: `space4x.q.collision.phasing` must not be `FAIL`.
- Determinism: same seed + same scenarioId produces stable PASS/FAIL/UNKNOWN outcomes for required questions.
- Telemetry: telemetry outputs present, readable, and not truncated (treat truncation as a failure for strict runs).

## Nightly Focus
Scenario IDs:
- `space4x_stress_capital_100_vs_100`: smoke-level capital battle harness
- `space4x_stress_capital_200_vs_200`: regression detector for perf cliffs
- `space4x_stress_capital_2k_vs_2k`: scale wall probe (run sparingly)

Run budget: start with 120s; reduce duration for local iteration if perf telemetry is still populated.

## Tooling Metadata
- Schema path (relative to `Assets/Scenarios/`): `schemas/scenario.schema.json`
- Inventory index: `Docs/Scenarios/capital_battle_index.md`
- 2k-specific notes: `Docs/Scenarios/capital_battle_2k_notes.md`

## Dependencies
- Headless entry point: `Space4XScenarioEntryPoint` (`--scenario`, `--report`)
- Headless questions registry: `Space4XHeadlessQuestionRegistry`
- Perf telemetry source producing `perf.*` metric keys

## Risks/Notes
- Some gates are conditional on beats being enabled (sensors/comms) and strike craft existing; keep those dependencies explicit in scenario variants.
- Avoid creating "fake tuning": if a config key is not consumed by runtime yet, document it as advisory-only.
