# Goal Card: Movement Observatory (Carrier Baseline)
ID: movement_observe_carrier_v1
Date: 2026-02-10
Owner: shonh
Status: draft

## Goal
Establish a clear baseline for carrier-class movement feel (inertia, turn radius, retrograde braking) while observing miners and strike craft in the same scenario.

## Hypotheses
- Carriers accelerate slowly, coast noticeably, and use visible retrograde braking before stopping.
- Miners show moderate inertia with shorter braking distance than carriers.
- Strike craft remain agile and do not inherit capital-ship sluggishness.

## Scenario Frame
Theme: Carrier convoy crosses a belt while a hostile carrier probes the lane.
Why this scenario matters: Movement feel must be believable across classes before we tune any motion profile.

## Setup
Map/Scene: space4x_movement_observe_micro
Actors: 2 carriers, 2 miners, strike craft escorts
Equipment/Loadouts: default module loadouts
Rules/Constraints: no warp, legacy mining/patrol disabled
Duration: 160s

## Roles and Experience
- Seats or roles: navigation officer, captain, pilot
- Experience tiers: mixed (default)
- Skill effects per seat: default movement modifiers

## Behavior Profile
Cooperation: fleet patrol + escort intercept
Target sharing: fleet-local
Discipline: moderate (avoid snap turns)
Failure modes: overshoot, jitter, retrograde thrash

## Targeting and Fire Control
Detection: default sensors
Target selection: fleet posture driven
Lock time: default
Track loss: default
Firing solution: default

## Movement and Orientation
Formation: loose patrol
Rotation limits: carrier-heavy, craft-agile
Facing rules: maintain travel heading unless intercept engaged
Speed profile: carriers slow, miners moderate, strike craft fast

## Collisions and Explosions (Movement)
- Momentum is conserved at contact; carrier mass dominates small craft.
- No sticky collisions; resolve with impulse plus separation.
- Post-impact drift is inertial; no hidden damping unless modeled.
- Off-center hits impart angular velocity; decay only via intentional dampers.
- Explosion impulses are radial, distance-scaled, and mass-weighted.
- Large impulses do not cause tunneling or NaN velocities.
- Impulses are readable: ships visibly change vector and continue on it until corrected.

## Weapons and Arcs
Weapon types: default
Firing arcs: default
Ammo and heat: default

## Nuance Prompts (fill what applies)
Perception: none
Coordination: escort release timing
Reaction timing: intercept response time
Skill/stat modifiers: default movement multipliers
Morale/discipline: none
Environment/interference: asteroids only
Failure cases: overshoot, oscillation, drift-stall
Determinism cues: fixed seed

## Script
1. Alpha carrier accelerates forward, then executes a wide turn and reverse arc.
2. Hostile carrier advances into lane.
3. Alpha triggers intercept and later returns to origin to observe braking.

## Metrics
- carrier_peak_speed: max carrier speed
- carrier_overshoot_distance: distance past target before settling
- carrier_settle_time_s: time from arrival to steady stop
- miner_brake_distance: miner stop distance
- craft_turn_time_s: strike craft time to reorient on intercept

## Scoring
- Qualitative baseline (pass if motion is smooth and inertial without snap or jitter)

## Acceptance
- No movement blackcats or NaN velocities
- Carrier shows visible coasting and retrograde braking before stop
- Strike craft remain visibly more agile than carriers

## Regression Guardrails
- No determinism regressions
- No loss of baseline performance

## Nightly Focus
Scenario ID: space4x_movement_observe_micro
Seed Source: scenario seed
Run budget: 3-5 min
Pass gates: movement turnrate bounds + no NaN
Do not regress: carrier turn smoothness
Priority work: tune carrier accel/decel/turn profile, adjust retrograde boost, verify miner delta
Telemetry IDs: space4x.q.movement.turnrate_bounds

## Branch Plan
Branch name: scenarios/goal-cards/movement-observe-carrier
Merge criteria: baseline observed + tuning plan recorded
Owner/Reviewer: shonh

## Variants
- movement_observe_carrier_v2 (longer arc)
- movement_observe_carrier_v3 (combat stress)

## Telemetry/Outputs
- Reports/space4x_movement_observe_metrics.csv
- Reports/space4x_movement_observe_metrics.json

## Dependencies
- VesselMovementSystem
- Space4XMovementTelemetrySystem
- ScenarioActionSystem

## Risks/Notes
- Observation-only pass; no tuning until baseline captured.

## Scenario JSON
Path: Assets/Scenarios/space4x_movement_observe_micro.json
Version: v0
