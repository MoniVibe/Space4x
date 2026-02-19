# Goal Card: Profile-Weighted Wing Doctrine
ID: profile_weighted_wing_doctrine_micro_v0
Date: 2026-02-13
Owner: codex
Status: draft

## Goal
Validate that strike craft decisions and wing behavior emerge from individual and collective profile weights, with minimal hardcoded behavior.

## Hypotheses
- High-lawfulness, high-compliance pilots stay in coordinated wing doctrine more consistently.
- High-chaos, high-risk pilots fragment doctrine earlier and break formation more often under pressure.
- Both sides still execute combat loops (attack run, CAP transitions) without scripted action forcing.

## Scenario Frame
Theme: Two carrier groups with identical hardware but different pilot + faction profile weighting.
Why this scenario matters: Isolates profile-driven decision quality from loadout or spawn asymmetry.

## Setup
Map/Scene: Space4X headless combat micro.
Actors: 2 carriers, 8 strike craft total (4 per side), pilot profiles for all strike craft.
Equipment/Loadouts: Same carrier and strike craft configuration on both sides.
Rules/Constraints: No scripted scenario actions; behavior must come from profile + doctrine systems.
Duration: 90 seconds.

## Roles and Experience
- Seats or roles: carrier command authority + strike craft pilots.
- Experience tiers: mixed veteran profiles through behavior disposition + stance weighting.
- Skill effects per seat: compliance, risk, aggression, patience, and formation adherence shape decisions.

## Behavior Profile
Cooperation: Friendly wing favors tight coordination and order compliance.
Target sharing: Both sides use normal combat sensing/target propagation.
Discipline: Friendly side should favor form-up persistence; hostile side should tolerate break directives.
Failure modes: Direction thrash, no attack execution, or no observed wing directive transitions.

## Profile Interplay and Outcomes
Interplay focus: individual pilot weights + faction outlooks + personal relations.
Assignment notes: pilotProfileIds explicitly map pilots to strike craft; no runtime random assignment required.
Expected outcomes: hostile wing shows lower cohesion under pressure while still engaging.
Outcome reporting: compare attack-run and wing-directive telemetry in one run.

## Targeting and Fire Control
Detection: default dogfight acquisition.
Target selection: standard combat systems with profile-weighted behavior influence.
Lock time: default combat timing.
Track loss: expected under maneuvering pressure.
Firing solution: default attack-run and CAP behavior.

## Movement and Orientation
Formation: wing form-up versus break dynamics.
Rotation limits: default strike craft steering + turn constraints.
Facing rules: combat system controlled.
Speed profile: configured via dogfight settings.

## Nuance Prompts (fill what applies)
Perception: standard visibility and target reacquisition under movement.
Coordination: wing directive and order-compliance behavior should be visible.
Reaction timing: profile weights can alter willingness to obey regroup/break patterns.
Morale/discipline: represented through stances, behavior disposition, and relation trust/fear.
Failure cases: no wing directive seen, no attack run observed.
Determinism cues: fixed seed, explicit profile lists.

## Script
1. Spawn mirrored carrier groups with different profile weighting and relation structures.
2. Allow combat loop to run without scenario action forcing.
3. Evaluate whether profile-weighted doctrine expression appears in telemetry.

## Metrics
- `space4x.q.combat.attack_run`: pass/fail gate for attack-run loop.
- `space4x.combat.wing_directive_seen`: evidence that wing doctrine directives were exercised.
- `space4x.combat.attack_run_seen`: confirms attack phase execution.
- `space4x.combat.cap_seen`: confirms CAP/transition behavior was observed.

## Scoring
- Pass if combat attack run question passes and wing directives are observed at least once.

## Acceptance
- `space4x.q.combat.attack_run` returns `pass`.
- `space4x.combat.wing_directive_seen >= 1`.

## Regression Guardrails
- No hardcoded behavior injection via scripted actions for this scenario.
- No determinism regressions for fixed seed replay.

## Nightly Focus
Scenario ID: space4x_profile_weighted_wing_doctrine_micro
Run budget: 2 minutes
Pass gates: combat attack run + wing directive seen
Do not regress: profile-weighted doctrine expression
Priority work: tune profile weight spread and relation weights to widen signal
Telemetry IDs: space4x.q.combat.attack_run

## Branch Plan
Branch name: scenarios/profile-weighted-wing-doctrine
Merge criteria: pass gates + review
Owner/Reviewer: codex / tbd

## Variants
- Mirror swap (disciplined/chaotic sides swapped spatially).
- Increase craft count to 8v8 for stronger doctrine signal.

## Addendum
Path: Docs/Scenarios/GoalCards/Addenda/space4x_profile_weighted_wing_doctrine_notes.md
Notes: concrete tuning questions and sweep order for profile/faction/relation weights.

## Telemetry/Outputs
- headless question output for `space4x.q.combat.attack_run`
- operator metrics for `space4x.combat.wing_directive_seen`

## Dependencies
- `Space4XStrikeCraftWingDecisionSystem`
- `Space4XWingFormationPlannerSystem`
- `Space4XHeadlessCombatTelemetrySystem`
- `Space4XMiningScenarioSystem` profile seeding path

## Risks/Notes
- Current question pack has no dedicated doctrine-specific question id yet; this spike uses existing combat attack-run question plus telemetry metrics.
- This scenario intentionally avoids scripted scenario actions to honor the no-bypass constraint.

## Scenario JSON
Path: Assets/Scenarios/space4x_profile_weighted_wing_doctrine_micro.json
Version: v0
