# Goal Card: Infected Swarm Hivemind
ID: infected_swarm_hivemind_micro
Date: 2026-02-09
Owner: codex
Status: draft

## Goal
Validate that a post-scarcity cruiser can survive a high-pressure infected swarm and eliminate the hive mind controlling it.

## Hypotheses
- The cruiser survives long enough to reach and destroy the hive mind.
- Intel-weighted targeting keeps focus on the hive mind once identified.

## Scenario Frame
Theme: Lone advanced cruiser vs infected swarm with a hidden command node.
Why this scenario matters: Exercises command pipeline, intel-based target prioritization, and high-entity combat pressure.

## Setup
Map/Scene: Space4X headless combat micro.
Actors: 1 friendly post-scarcity cruiser, 1 hostile hive mind carrier, 1200 infected strike craft.
Equipment/Loadouts: Cruiser heavy warship loadout; hive mind spawns infected fighters.
Rules/Constraints: Cruiser is briefed that the hive mind controls the swarm; intel influences target selection (no hard-coded orders).
Duration: 180s.

## Roles and Experience
- Seats or roles: captain, weapons, sensors, tactical.
- Experience tiers: veteran crew on cruiser.
- Skill effects per seat: faster target acquisition, higher hit confidence.

## Behavior Profile
Cooperation: Coordinated ship-wide focus on hive mind.
Target sharing: Shared intel fact across captain/weapon seats.
Discipline: Focus fire, avoid swarm distraction.
Failure modes: Overfocus on swarm, target lock churn.

## Targeting and Fire Control
Detection: Intel fact flags hive mind as command node.
Target selection: Hive mind first, swarm second.
Lock time: Standard with intel bonus.
Track loss: Reacquire hive mind if swarm obscures.
Firing solution: Lead targets, maintain optimal range.

## Movement and Orientation
Formation: N/A (single cruiser).
Rotation limits: Cruiser-class yaw/pitch limits.
Facing rules: Keep weapons arcs on hive mind.
Speed profile: Aggressive approach, controlled retreat when needed.

## Weapons and Arcs
Weapon types: Heavy batteries + point defense.
Firing arcs: Forward and broadside arcs.
Ammo and heat: Normal limits; no ammo starvation.

## Nuance Prompts (fill what applies)
Perception: Swarm occlusion may delay hive mind lock.
Coordination: Shared target memory between seats.
Reaction timing: Target reacquire under swarm pressure.
Failure cases: Oscillation between swarm and hive mind.
Determinism cues: Fixed seed, stable spawn positions.

## Script
1. Spawn cruiser and infected hive mind with swarm fighters.
2. Cruiser receives hive mind elimination intent via intel.
3. Scenario ends when hive mind destroyed or cruiser is lost.

## Metrics
- space4x.q.hivemind_destroyed: hive mind entity eliminated.
- space4x.q.cruiser_survives: cruiser survives to scenario end.

## Scoring
- Pass if both metrics are true.

## Acceptance
- Pass: hive mind destroyed and cruiser survives.
- Fail: cruiser lost or hive mind survives.

## Regression Guardrails
- No determinism regressions in target selection.
- Maintain baseline perf with 1200 swarm fighters.

## Nightly Focus
Scenario ID: space4x_infected_swarm_hivemind_micro
Seed Source: Space4XScenarioSeedSystem (central seeding)
Run budget: 3 min
Pass gates: hivemind_destroyed, cruiser_survives
Do not regress: intel-weighted target selection stability
Priority work: intel decay tuning, hive mind tagging, strike craft perf
Telemetry IDs: space4x.q.hivemind_destroyed, space4x.q.cruiser_survives

## Branch Plan
Branch name: scenarios/goal-cards/infected-swarm-hivemind
Merge criteria: pass gates + review
Owner/Reviewer: codex

## Variants
- scale to 2400 infected fighters
- add second hero escort

## Telemetry/Outputs
- headless questions for hive mind + cruiser survival
- perf telemetry for frame budget under swarm

## Dependencies
- Captain order pipeline
- Space4XScenarioSeedSystem
- Space4XIntelTargetingSystem
- Strike craft spawn + dogfight tuning

## Risks/Notes
- Post-scarcity cruiser uses placeholder ship template until dedicated template exists.
- Intel-based focus uses command-node facts, not explicit orders.

## Scenario JSON
Path: Assets/Scenarios/space4x_infected_swarm_hivemind_micro.json
Version: v0
