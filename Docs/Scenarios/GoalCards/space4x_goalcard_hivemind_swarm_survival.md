# Goal Card: Hivemind Swarm Break
ID: hivemind_swarm_break_v0
Date: 2026-02-09
Owner: shonh
Status: draft

## Goal
Validate that two hero cruisers can locate and destroy a swarm hivemind while surviving sustained infected pressure.

## Hypotheses
- The hivemind is destroyed before scenario end.
- At least one hero cruiser survives.

## Scenario Frame
Theme: Two hero cruisers vs infected swarm with a single command hive.
Why this scenario matters: Tests intel-weighted targeting, survivability under swarm pressure, and kill-the-source tactics.

## Setup
Map/Scene: Headless deep-space arena.
Actors: 2 hero cruisers (friendly), infected swarm (hostile, 1200 fighters), 1 hivemind ship (hostile).
Equipment/Loadouts: Cruisers use heavy batteries; swarm uses light fighters.
Rules/Constraints: Cruisers are briefed that the hivemind controls the swarm; intel influences target choice (no hard-coded orders).
Duration: 180 seconds.

## Roles and Experience
- Seats or roles: Cruiser captain + weapons/sensors seats, hivemind controller, swarm fighters.
- Experience tiers: Cruiser veteran, hivemind elite, swarm veteran.
- Skill effects per seat: Cruiser accuracy + survivability; hivemind coordination; swarm aggression.

## Behavior Profile
Cooperation: Swarm converges on cruisers; hivemind stays at stand-off range.
Target sharing: Cruisers share intel about the command node.
Discipline: Cruisers hold focus on hive when intel confidence is high.
Failure modes: Target churn between swarm and hive when intel decays.

## Targeting and Fire Control
Detection: Cruisers receive intel about the command node at scenario start.
Target selection: Intel-weighted targeting prioritizes hivemind; swarm prioritizes cruisers.
Lock time: Standard with intel bonus.
Track loss: Reacquire hive when intel is reinforced.
Firing solution: Cruisers maintain optimal range, swarm closes for pressure.

## Movement and Orientation
Formation: Swarm surrounds; hivemind offsets behind swarm.
Rotation limits: Cruiser slower turn; fighters agile.
Facing rules: Cruisers keep arcs on hive; swarm faces cruisers.
Speed profile: Cruisers moderate; swarm fast.

## Weapons and Arcs
Weapon types: Cruiser heavy turrets; swarm light interceptors.
Firing arcs: Cruiser broadside; swarm forward.
Ammo and heat: Cruiser energy + ammo; swarm light weapons.

## Nuance Prompts (fill what applies)
Perception: Hivemind location seeded as intel fact.
Coordination: Cruisers broadcast intel-weighted focus.
Reaction timing: Target priority recalculates as intel decays.
Failure cases: Overfocus on swarm, loss of hive track.
Determinism cues: Fixed seed; reproducible spawn layout.

## Script
1. Spawn two hero cruisers and hivemind; spawn swarm fighters.
2. Cruisers push to hive while swarm applies pressure.
3. Victory when hivemind dies with at least one cruiser alive.

## Metrics
- hivemind_destroyed: 1 if hive ship is destroyed.
- cruiser_survives: 1 if at least one cruiser alive at exit.
- time_to_kill_s: Time to hivemind death.
- swarm_destroyed: Count of infected fighters destroyed.

## Scoring
- Pass if hivemind_destroyed == 1 AND cruiser_survives == 1.

## Acceptance
- hivemind_destroyed == 1
- cruiser_survives == 1
- time_to_kill_s <= 180

## Regression Guardrails
- No determinism regressions on seed replay.
- No silent failure to spawn hive or swarm.

## Nightly Focus
Scenario ID: space4x_hivemind_swarm_micro
Seed Source: Space4XScenarioSeedSystem (central seeding)
Run budget: 3 mins
Pass gates: hivemind_destroyed, cruiser_survives
Do not regress: swarm spawn counts, intel-weighted target prioritization
Priority work: swarm pressure, hive survivability, intel decay tuning
Telemetry IDs: space4x.q.hivemind_destroyed, space4x.q.cruiser_survives

## Branch Plan
Branch name: scenarios/goal-cards/hivemind-swarm
Merge criteria: pass gates + review
Owner/Reviewer: shonh / tbd

## Variants
- heavier hive escort
- cruiser with reduced turn rate

## Telemetry/Outputs
- headless metrics bundle
- kill timeline logs

## Dependencies
- Space4X combat loop
- Fighter swarm behavior
- Space4XScenarioSeedSystem
- Space4XIntelTargetingSystem

## Risks/Notes
- Central seeding uses carrier/fighter proxies until dedicated hivemind types exist.

## Scenario JSON
Path: Assets/Scenarios/space4x_hivemind_swarm_micro.json
Version: v0
