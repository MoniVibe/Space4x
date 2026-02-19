# Goal Card: FleetCrawl Pressure And Debt v0
ID: fleetcrawl_pressure_debt_v0
Date: 2026-02-19
Owner: iterator-desktop
Status: active

## Goal
Define and validate an endless-friendly pacing regime where room dwell time raises pressure and persistent run debt, without hard room timeout.

## Hypotheses
- Players can stay in-room for long durations, but threat escalates enough to preserve stakes.
- Delay in earlier rooms increases challenge in later rooms through run debt.
- Meta progression can enable partial or situational counterplay to the wall of death.

## Scenario Frame
Theme: Survivors run under mounting pressure.
Why this scenario matters: It supports high-skill long-hold play while maintaining strategic pacing at run level.

## Setup
Map/Scene: FleetCrawl survivors slice.
Actors: Player flagship + wing + hostile waves.
Rules/Constraints: No forced extraction by timer.
Duration: Unbounded in design; scenario may still use finite capture windows for tests.

## Script
1. Enter room and track `room_time_s` from start.
2. Compute `room_pressure` using pacing model v0 curve.
3. Apply pressure + debt mapping to wave budget/elite/hazard generation.
4. On room exit, accrue `run_debt` from over-target time for chosen difficulty.
5. Carry debt into next room threat calculations.

## Metrics
- `room_time_s`: elapsed time in current room.
- `room_pressure`: real-time escalation scalar.
- `run_debt`: persistent escalation debt.
- `wave_budget_mult`: spawn budget multiplier.
- `elite_spawn_rate`: elite incidence per minute.
- `hazard_rate_mult`: hazard pressure multiplier.
- `reward_decay_mult`: reward efficiency under pressure.

## Scoring
- Primary: monotonic increase in threat with room dwell time.
- Secondary: measurable carryover impact from debt into following room(s).

## Acceptance
- No hard timeout or forced exit solely due to room timer.
- Threat remains low-to-moderate during grace and clearly intensifies later.
- Over-target room time creates measurable debt and later threat increase.
- High-power builds can survive long rooms, but pressure/debt creates rising cost.

## Regression Guardrails
- Determinism remains stable for identical seed + action trace.
- Existing non-FleetCrawl scenarios remain unaffected.

## Scenario JSON
Path: Docs/Simulation/Space4X_FleetCrawl_Pacing_Model_v0.json
Version: v0
