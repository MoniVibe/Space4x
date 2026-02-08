# Goal Card: <Short Name>
ID: <slice>_<intent>_<variant>
Date: <YYYY-MM-DD>
Owner: <name>
Status: draft | active | archived

## Goal
<One or two sentences describing the behavior we want to observe or enforce.>

## Hypotheses
- <Expected outcome #1>
- <Expected outcome #2>

## Scenario Frame
Theme: <short narrative>
Why this scenario matters: <why this is a goal for iteration>

## Setup
Map/Scene: <scene or scenario base>
Actors: <ships, crews, factions, NPCs>
Equipment/Loadouts: <modules, hulls, weapons>
Rules/Constraints: <no warp, no shields, limited ammo>
Duration: <seconds>

## Roles and Experience
- Seats or roles: <pilot, gunner, comms, tactics, etc>
- Experience tiers: <rookie, experienced, veteran, elite>
- Skill effects per seat: <reaction time, precision, lock speed, etc>

## Behavior Profile
Cooperation: <solo vs coordinated, comms, shared targeting>
Target sharing: <broadcast, local, none>
Discipline: <hold fire, focus fire, stagger>
Failure modes: <panic, overfocus, target lock oscillation>

## Targeting and Fire Control
Detection: <sensors, detection time, visibility rules>
Target selection: <priority rules>
Lock time: <base, modifiers>
Track loss: <conditions, reacquire behavior>
Firing solution: <lead, arcs, line of sight>

## Movement and Orientation
Formation: <ring, wedge, grid>
Rotation limits: <yaw/pitch/roll rates>
Facing rules: <stay facing target, keep arcs>
Speed profile: <min/max>

## Weapons and Arcs
Weapon types: <projectiles, beams>
Firing arcs: <angles and constraints>
Ammo and heat: <limits>

## Nuance Prompts (fill what applies)
Perception: <LOS, occlusion, stealth, sensor lag>
Coordination: <callouts, shared target memory, comms delay>
Reaction timing: <lock, aim, fire latencies>
Skill/stat modifiers: <seat-specific effects, fatigue, penalties>
Morale/discipline: <hold fire, focus fire, panic, misfire>
Environment/interference: <weather, jamming, obstacles>
Failure cases: <thrash, oscillation, friendly fire>
Determinism cues: <seed, logging, reproducibility>

## Script
1. <Step 1>
2. <Step 2>
3. <Step 3>

## Metrics
- <metric name>: <definition>
- <metric name>: <definition>

## Scoring
- <formula or aggregation>

## Acceptance
- <pass/fail or acceptable range>

## Regression Guardrails
- <no determinism regressions>
- <no loss of baseline performance>

## Nightly Focus
Scenario ID: <scenarioId>
Run budget: <mins or frames>
Pass gates: <metric thresholds>
Do not regress: <list>
Priority work: <2-4 concrete tasks>
Telemetry IDs: <headless question IDs>

## Branch Plan
Branch name: <scenarios/goal-cards/<short>>
Merge criteria: <pass gates + review>
Owner/Reviewer: <names>

## Variants
- <variant A>
- <variant B>

## Telemetry/Outputs
- <log or report ids>
- <chart or capture ids>

## Dependencies
- <systems or data required>

## Risks/Notes
- <assumptions, blockers, caveats>

## Scenario JSON
Path: <Assets/Scenarios/your_scenario.json>
Version: <v0>
