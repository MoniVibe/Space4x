# Space4X Solar System Expansion Phase 0 Checklist

Status: Draft v1 (planning artifact)
Date: 2026-02-18
Related:
- `Docs/Simulation/Space4X_Solar_System_Expansion_Architecture.md`
- `Docs/Simulation/Space4X_Solar_System_Expansion_Plan.md`
- `Assets/Scenarios/space4x_solar_phase0_baseline.json`

## 1) Purpose

Define the exact baseline capture contract before implementing solar-system expansion features.

Phase 0 is complete when we can repeatedly capture stable performance and behavior metrics from a fixed seed/profile matrix, with explicit pass/fail gates.

## 2) Baseline Matrix

Record each run with:

- `run_id`
- `scenario_path`
- `scenario_id`
- `seed`
- `profile` (`debug_lod_off` or `production_lod_on`)
- `repeat_index` (0..N-1)

Minimum matrix:

- Scenario: `Assets/Scenarios/space4x_solar_phase0_baseline.json`
- Seed: `42` (fixed for baseline)
- Repeats per profile: `3`
- Profiles: `debug_lod_off`, `production_lod_on`

## 3) Exact Metric Fields (Source of Truth)

Use `headless_answers.json` question payload as the canonical Phase 0 metric source.

Question envelope fields:

- `scenarioId`
- `questions[].id`
- `questions[].status`
- `questions[].required`
- `questions[].answer`
- `questions[].unknown_reason`
- `questions[].window.startTick`
- `questions[].window.endTick`

### Required Question IDs for Phase 0

- `space4x.q.mining.progress`
- `space4x.q.perf.summary`
- `space4x.q.perf.budget`

### Required Metrics by Question

`space4x.q.mining.progress` metrics:

- `questions[].metrics.gather_commands`
- `questions[].metrics.ore_delta`
- `questions[].metrics.cargo_delta`
- `questions[].metrics.pass`

`space4x.q.perf.summary` metrics:

- `questions[].metrics.fixed_step_ms_p50`
- `questions[].metrics.fixed_step_ms_p95`
- `questions[].metrics.fixed_step_ms_p99`
- `questions[].metrics.fixed_step_ms_max`
- `questions[].metrics.structural_delta_p95`
- `questions[].metrics.reserved_bytes_peak`
- `questions[].metrics.tick_samples`
- `questions[].metrics.structural_samples`

`space4x.q.perf.budget` metrics:

- `questions[].metrics.observed`
- `questions[].metrics.budget`
- `questions[].metrics.tick`

## 4) Derived Phase 0 Record Fields

For each run, compute and persist:

- `phase0.perf_budget_margin = budget - observed`
- `phase0.tick_window = endTick - startTick`
- `phase0.perf_summary_known = status(space4x.q.perf.summary) != unknown`
- `phase0.mining_progress_pass = status(space4x.q.mining.progress) == pass`
- `phase0.perf_budget_pass = status(space4x.q.perf.budget) == pass`

## 5) Phase 0 Acceptance Gates

- Gate A: All required questions are present in output.
- Gate B: `space4x.q.mining.progress` status is `pass`.
- Gate C: `space4x.q.perf.summary` status is `pass` (not `unknown`).
- Gate D: `space4x.q.perf.budget` status is `pass`.
- Gate E: `phase0.perf_budget_margin >= 0`.
- Gate F: For each profile, repeated runs show no missing required metrics.

## 6) First Target Scenario Config Changes

Target file:

- `Assets/Scenarios/space4x_solar_phase0_baseline.json`

Intent for this scenario:

- Keep gameplay close to current mining micro behavior.
- Enable reference frames + floating origin baseline.
- Add Phase 0 required question pack.
- Use explicit render/orbital values so baseline is reproducible.

Scenario config keys used:

- `scenarioConfig.applyFloatingOrigin`
- `scenarioConfig.applyReferenceFrames`
- `scenarioConfig.renderFrame`
- `scenarioConfig.orbitalBand`
- `scenarioConfig.headlessQuestions`

## 7) Notes

- This checklist is baseline-only and does not require new gameplay systems.
- Expansion execution starts after this checklist is green for both profiles.

