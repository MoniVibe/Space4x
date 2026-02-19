# Goal Card: Profile-Weighted Logistics Conflict
ID: profile_weighted_logistics_conflict_micro_v0
Date: 2026-02-13
Owner: codex
Status: draft

## Goal
Validate that mining/logistics behavior and escort posture emerge from individual and collective profile weights, without scripted command bypasses.

## Hypotheses
- Discipline-weighted miners sustain gather/dropoff loops with lower churn under threat.
- Chaos/risk-weighted raider pilots disrupt local coherence and increase opportunistic pressure.
- The scenario still produces measurable mining progress despite contested space.

## Scenario Frame
Theme: Two mining groups contend over a central ore lane while escort-capable carriers react from profile-driven doctrine.
Why this scenario matters: Exercises economic throughput plus combat pressure while keeping behavior profile-led.

## Setup
Map/Scene: Space4X headless mining-combat micro.
Actors: 2 carriers, 6 mining vessels, 4 strike craft, 3 deposits.
Equipment/Loadouts: Shared baseline modules via default loadouts.
Rules/Constraints: No scripted actions; no explicit intercept triggers; behavior comes from systems and profile weights.
Duration: 120 seconds.

## Roles and Experience
- Seats or roles: carrier command, mining pilots, strike pilots.
- Experience tiers: mixed discipline/risk profiles with explicit stance weighting.
- Skill effects per seat: compliance, formation adherence, caution, risk tolerance, and aggression shape outcomes.

## Behavior Profile
Cooperation: friendly pilots favor disciplined gather and return cycles.
Target sharing: standard combat and sensing flow.
Discipline: friendly side should preserve mining cadence under pressure.
Failure modes: mining stall, churn without delivery, or no escort/combat signal when hostile pressure exists.

## Profile Interplay and Outcomes
Interplay focus: pilot profile weights + faction outlooks + personal trust/fear relations.
Assignment notes: mining and strike pilots are assigned via explicit profile ids.
Expected outcomes: throughput remains positive while contested behavior varies by profile group.
Outcome reporting: mining progress question plus combat telemetry signals.

## Nuance Prompts (fill what applies)
Perception: contested deposit creates overlapping intent pressure.
Coordination: profile-weight differences should alter cohesion and pacing.
Reaction timing: risk-heavy actors should switch intent more aggressively.
Morale/discipline: represented by stance weights and behavior disposition values.
Failure cases: `MINING_STALL` black-cat, zero ore/cargo delta, or no combat activation.
Determinism cues: fixed seed + explicit profile lists.

## Script
1. Spawn friendly and hostile mining ecosystems around a shared center deposit.
2. Run without scripted scenario actions.
3. Verify mining throughput and profile-expression signals.

## Metrics
- `space4x.q.mining.progress`: primary pass gate for mining loop health.
- `space4x.mining.gather_commands`: confirms mining loop engagement.
- `space4x.mining.ore_delta` and `space4x.mining.cargo_delta`: confirms positive yield/flow.
- `space4x.combat.wing_directive_seen`: indicates doctrine pressure surfaced during contest.

## Scoring
- Pass if mining progress question passes and gather commands are non-zero.

## Acceptance
- `space4x.q.mining.progress` returns `pass`.
- `space4x.mining.gather_commands > 0`.

## Regression Guardrails
- No scripted action forcing for fleet movement/intercepts in this scenario.
- Preserve deterministic replay behavior for fixed seed.

## Nightly Focus
Scenario ID: space4x_profile_weighted_logistics_conflict_micro
Run budget: 2 minutes
Pass gates: mining progress + gather command activity
Do not regress: profile-weighted logistics behavior under contest
Priority work: tune profile spread for stronger throughput/cohesion contrast
Telemetry IDs: space4x.q.mining.progress

## Branch Plan
Branch name: scenarios/profile-weighted-logistics-conflict
Merge criteria: pass gates + review
Owner/Reviewer: codex / tbd

## Variants
- Increase contested hostility density with same logistics count.
- Reduce friendly compliance to probe stall threshold.

## Addendum
Path: Docs/Scenarios/GoalCards/Addenda/space4x_profile_weighted_logistics_conflict_notes.md
Notes: concrete tuning questions and sweep order for profile/faction/relation weights.

## Telemetry/Outputs
- question output for `space4x.q.mining.progress`
- operator metrics including `space4x.mining.*` and `space4x.combat.wing_directive_seen`

## Dependencies
- `Space4XMiningScenarioSystem`
- `Space4XStrikeCraftWingDecisionSystem`
- `Space4XHeadlessQuestionRegistry`
- profile seeding (`individuals`, `personalRelations`, `behaviorDisposition`)

## Risks/Notes
- Mining progression is the hard gate; combat doctrine metrics are observational in this spike.
- This scenario intentionally avoids action scripts to honor no-bypass constraints.

## Scenario JSON
Path: Assets/Scenarios/space4x_profile_weighted_logistics_conflict_micro.json
Version: v0
