# Goal Card: FleetCrawl Core Loop
ID: fleetcrawl_core_loop_v0
Date: 2026-02-19
Owner: shonh
Status: draft

## Goal
Validate a roguelike FleetCrawl run loop where room-to-room choices force tradeoffs between cargo throughput, survivability, and combat lethality.

## Hypotheses
- Cargo-forward loadouts clear resource rooms faster but underperform in combat pressure rooms.
- Combat-forward loadouts survive combat rooms better but lose long-run value due to lower cargo output.
- A flagship segment mutation path (loot and mission rewards) can be represented by deterministic data contracts before full runtime mutation logic lands.

## Scenario Frame
Theme: Mutating flagship expedition through chained rooms with escalating risk.
Why this scenario matters: It anchors FleetCrawl as an explicit Space4X mode instead of isolated mining/combat demos.

## Setup
Map/Scene: Headless FleetCrawl micro rooms.
Actors: 1 player-aligned flagship group, escorts/miners, hostile intercept groups.
Equipment/Loadouts: Cargo-biased and combat-biased variants built from existing hull and module IDs.
Rules/Constraints: Deterministic seed, no new bespoke systems required, use existing mission/combat/mining contracts.
Duration: 150-240 seconds per micro.

## Roles and Experience
- Seats or roles: captain, tactical, logistics, pilot.
- Experience tiers: baseline veteran crews for v0 (single-tier).
- Skill effects per seat: tactical biases intercept behavior, logistics biases extraction throughput.

## Behavior Profile
Cooperation: escorts cover miners and cargo craft.
Target sharing: escorts and carrier share hostile focus targets.
Discipline: hold formation until intercept triggers, then split by role.
Failure modes: cargo craft overexposed to intercept pressure, escort over-commit leaving extraction lane open.

## Addendum (Optional)
Path: Docs/Scenarios/GoalCards/Addenda/space4x_fleetcrawl_notes.md
Notes: Contains room archetypes, gate matrix, and flagship mutation design notes.

## Targeting and Fire Control
Detection: standard scenario sensor behavior.
Target selection: hostile strike groups prioritize high-cargo vulnerable craft.
Lock time: unchanged from current combat tuning.
Track loss: acceptable as long as attack run engagement is recorded.
Firing solution: existing combat profile behavior and attack-run telemetry.

## Movement and Orientation
Formation: mixed convoy (cargo + escorts).
Rotation limits: unchanged defaults from current hulls.
Facing rules: escorts bias hostile-facing posture, cargo units bias extraction vector.
Speed profile: cargo-forward units slower; combat-forward escorts faster.

## Weapons and Arcs
Weapon types: current projectile/laser/missile module set.
Firing arcs: inherited from existing carrier and craft profiles.
Ammo and heat: inherited from existing combat slice.

## Script
1. Enter room and execute objective (mine/scout/salvage pressure).
2. Trigger intercept pressure and force role split decisions.
3. Exit or fail based on survival and throughput.

## Metrics
- room_completion: room objective reached within scenario duration.
- mining_progress: positive mining throughput in resource-forward rooms.
- attack_run_activity: combat attack-run activity observed under intercept pressure.
- survival_ratio: surviving friendly ships at scenario end.

## Scoring
- Score = 0.35 * room_completion + 0.25 * normalized_mining_progress + 0.25 * normalized_attack_run_activity + 0.15 * survival_ratio.

## Acceptance
- Core scenario: mining progress and attack-run questions both pass.
- Derelict risk scenario: attack-run question passes and at least one cargo craft survives.
- Swarm escape scenario: attack-run question passes with non-zero hostile pressure.

## Regression Guardrails
- No determinism regressions on fixed seeds.
- No missing scenario resolution for new FleetCrawl IDs.
- No removal of existing micro scenario behavior.

## Nightly Focus
Scenario ID: space4x_fleetcrawl_core_micro
Run budget: 4 minutes
Pass gates: mining + combat question pass in same run
Do not regress: space4x_mining_micro and space4x_combat_micro behavior baselines
Priority work:
- Data-contract room chaining
- Cargo vs lethality loadout contrast
- Segment mutation reward hooks via loot contracts
Telemetry IDs: space4x.q.mining.progress, space4x.q.combat.attack_run

## Branch Plan
Branch name: feat/fleetcrawl-laptop-20260219
Merge criteria: scenario parse clean, question IDs valid, review complete
Owner/Reviewer: shonh / tbd

## Variants
- Derelict risk room with high intercept pressure.
- Swarm escape room prioritizing survival over extraction.
- Low-power flagship mutation room with reduced module budget.

## Telemetry/Outputs
- headless operator report
- question pack pass/fail
- scenario run summary

## Dependencies
- Space4XMiningScenarioSystem JSON spawn/action path
- Space4XScenarioActionSystem (MoveFleet, TriggerIntercept)
- Space4X combat telemetry/question registry
- Existing module and hull IDs

## Risks/Notes
- Flagship segment mutation is contract-first in v0; full runtime mutation behavior is follow-up work.
- New FleetCrawl-specific question IDs are deferred until baseline scenarios stabilize.

## Scenario JSON
Path: Assets/Scenarios/space4x_fleetcrawl_core_micro.json
Version: v0
