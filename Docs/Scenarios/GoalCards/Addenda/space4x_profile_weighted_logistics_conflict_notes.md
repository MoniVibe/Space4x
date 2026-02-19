# Addendum: Profile-Weighted Logistics Conflict - Tuning Questions
Date: 2026-02-13
Owner: codex
Status: draft

## Purpose
Define concrete tuning questions for `space4x_profile_weighted_logistics_conflict_micro` so agents can iterate profile weights and measure behavior change without action-script bypasses.

## Rule Constraint
- No hardcoded behavior bypasses for this scenario.
- Keep `actions: []`; modify profile/faction/relationship weights instead of command scripting.

## Baseline Run
- Scenario: `space4x_profile_weighted_logistics_conflict_micro`
- Scenario path: `Assets/Scenarios/space4x_profile_weighted_logistics_conflict_micro.json`
- Command placeholder: `<SPACE4X_HEADLESS_BINARY> -batchmode -nographics -logFile - --scenario Assets/Scenarios/space4x_profile_weighted_logistics_conflict_micro.json`
- Primary gate: `space4x.q.mining.progress`

## Tuning Questions
- `Q1 - Compliance Spread`: How much does raising friendly miner compliance (+0.10) reduce mining stall risk?
  - Knob: `individuals[*].behaviorDisposition.compliance` for `friendly_miner_*`
  - Expected signal: higher `space4x.mining.ore_delta` and fewer `MINING_STALL` black-cat events.
- `Q2 - Risk Pressure`: At what hostile risk threshold does friendly throughput collapse?
  - Knob: `individuals[*].behaviorDisposition.riskTolerance` for `hostile_miner_*` and `hostile_strike_*`
  - Sweep: `0.70 -> 0.85` in `0.05` increments.
  - Expected signal: rising disruption with eventual drop in `space4x.mining.cargo_delta`.
- `Q3 - Formation Adherence`: Does friendly strike formation adherence stabilize escort behavior under contest?
  - Knob: `friendly_strike_* .behaviorDisposition.formationAdherence`
  - Sweep: `0.60, 0.75, 0.90`
  - Expected signal: improved stability in `space4x.combat.wing_directive_seen` with non-degrading mining gate.
- `Q4 - Stance Mix`: Which stance mix gives best throughput/contest balance?
  - Knob: `individuals[*].stances`
  - Friendly candidate: heavier `Loyalist`; hostile candidate: heavier `Mutinous` + `Opportunist`.
  - Expected signal: clear side-to-side behavior asymmetry while pass gate remains green.
- `Q5 - Relation Stress`: How sensitive is throughput to trust/fear perturbation among miners?
  - Knob: `personalRelations[*].trust` and `personalRelations[*].fear`
  - Sweep: trust `-0.15/+0.15`, fear `-0.10/+0.10`
  - Expected signal: measurable cadence change in gather/dropoff, reflected in `space4x.mining.gather_commands`.
- `Q6 - Collective Outlook`: Do faction outlook toggles materially change contested behavior?
  - Knob: `scenarioConfig.friendlyFactionOutlook` / `scenarioConfig.hostileFactionOutlook`
  - Variant ideas: add/remove `Authoritarian`, `Honorable`, `Corrupt`, `Materialist`.
  - Expected signal: doctrine posture shifts without direct order scripting.

## Sweep Order (Recommended)
1. Run baseline unchanged and record mining/combat metrics.
2. Apply `Q1` and `Q2` only; isolate profile scalar effects.
3. Apply `Q3` and `Q4`; isolate doctrine + stance effects.
4. Apply `Q5` and `Q6`; isolate social + collective effects.

## Capture Checklist Per Sweep
- Scenario id and seed.
- `space4x.q.mining.progress` result.
- `space4x.mining.gather_commands`
- `space4x.mining.ore_delta`
- `space4x.mining.cargo_delta`
- `space4x.combat.wing_directive_seen`
- Any black-cat code (`MINING_STALL`, others).

## Open Questions
- Should this scenario get a dedicated headless question id for profile-weighted doctrine in logistics (instead of relying on mining gate + raw metrics)?
- Is current hostile pressure sufficient to expose nonlinear failure thresholds, or should we add a higher-pressure variant with same no-bypass rules?
