# Addendum: Profile-Weighted Wing Doctrine - Tuning Questions
Date: 2026-02-13
Owner: codex
Status: draft

## Purpose
Define concrete tuning questions for `space4x_profile_weighted_wing_doctrine_micro` so agents can iterate profile and collective weights while preserving the no-bypass rule.

## Rule Constraint
- No hardcoded behavior bypasses for this scenario.
- Keep `actions: []`; tune profile/faction/relation weights instead of scripting directives.

## Baseline Run
- Scenario: `space4x_profile_weighted_wing_doctrine_micro`
- Scenario path: `Assets/Scenarios/space4x_profile_weighted_wing_doctrine_micro.json`
- Command placeholder: `<SPACE4X_HEADLESS_BINARY> -batchmode -nographics -logFile - --scenario Assets/Scenarios/space4x_profile_weighted_wing_doctrine_micro.json`
- Primary gate: `space4x.q.combat.attack_run`

## Tuning Questions
- `Q1 - Compliance Threshold`: What minimum friendly compliance keeps wing regroup stable?
  - Knob: `friendly` strike pilot `behaviorDisposition.compliance`
  - Sweep: `0.70, 0.80, 0.90`
  - Expected signal: higher `space4x.combat.wing_directive_seen` stability with sustained attack-run pass.
- `Q2 - Chaos Pressure`: At what hostile lawfulness/chaos mix does wing fragmentation dominate?
  - Knobs: hostile `individuals[*].law`, hostile `stances` (`Mutinous`/`Opportunist` weights)
  - Sweep: law `-0.60 -> -0.90` in `0.10` steps
  - Expected signal: rising break behavior and higher directive churn.
- `Q3 - Formation Adherence`: How strongly does formation adherence suppress doctrine thrash?
  - Knob: `behaviorDisposition.formationAdherence` both sides
  - Sweep: symmetric and asymmetric (`friendly high`, `hostile low`)
  - Expected signal: less oscillation for high-adherence side.
- `Q4 - Risk vs Patience`: Which combination produces decisive aggression without collapse?
  - Knobs: `behaviorDisposition.riskTolerance`, `behaviorDisposition.patience`
  - Sweep: paired matrix (`risk high/low` x `patience high/low`)
  - Expected signal: identify stable attack behavior that still passes gate.
- `Q5 - Relation Trust/Fear`: Do wing-internal trust/fear values materially affect cohesion?
  - Knob: `personalRelations[*].trust` and `personalRelations[*].fear`
  - Sweep: trust `+/- 0.15`, fear `+/- 0.15`
  - Expected signal: measurable change in coordination without scripted orders.
- `Q6 - Collective Outlook`: Which faction outlook pairs maximize profile-expression contrast?
  - Knob: `scenarioConfig.friendlyFactionOutlook` and `hostileFactionOutlook`
  - Variant ideas: add/remove `Honorable`, `Authoritarian`, `Corrupt`, `Xenophobe`
  - Expected signal: stronger side-to-side doctrine divergence at equal hardware.

## Sweep Order (Recommended)
1. Run baseline unchanged and capture combat telemetry.
2. Apply `Q1` and `Q2` to isolate individual profile scalar effects.
3. Apply `Q3` and `Q4` to isolate group-cohesion dynamics.
4. Apply `Q5` and `Q6` to isolate social/collective effects.

## Capture Checklist Per Sweep
- Scenario id and seed.
- `space4x.q.combat.attack_run` result.
- `space4x.combat.wing_directive_seen`
- `space4x.combat.attack_run_seen`
- `space4x.combat.cap_seen`
- Any black-cat flags or loop-proof failures.

## Open Questions
- Should we add a dedicated headless question id for doctrine stability (directive churn/oscillation), separate from attack-run pass?
- Do we need an 8v8 variant to make profile-expression deltas clearer in telemetry?
