# Goal Card: Capital Ship Firing Range
ID: gunnery_capital_shooting_range_v0
Date: 2026-02-09
Owner: shonh
Status: active

## Goal
Demonstrate that crew experience tiers and profile doctrines produce measurable, repeatable differences in gunnery performance and decision-making under identical hardware conditions.

## Hypotheses
- Elite > veteran > experienced >= rookie in reaction time, precision, and time-to-kill.
- Profile doctrine changes tool usage and tactics (materialists favor drones; spirituals bias psionics/mana; warlikes are more aggressive).
- Differences remain measurable under fixed seed and target motion.

## Scenario Frame
Theme: Capital ship training range validating crew experience and doctrine expression.
Why this scenario matters: It anchors gunnery skill scaling, profile expression, and drone/tooling choices as observable signals.

## Setup
Map/Scene: Controlled firing range (open space, low clutter).
Actors: 1 capital ship crewed by a single experience tier per run; target practice drones.
Equipment/Loadouts: Identical hull and loadout; one spinal mount with low spread; drone bay enabled.
Rules/Constraints: No external ECM; fixed seed; targets follow scripted motion.
Duration: 120 seconds (warmup 10s, live fire 100s, cooldown 10s).

## Roles and Experience
- Seats or roles: gunnery, tactical, sensors (optional comms).
- Experience tiers: rookie, experienced, veteran, elite.
- Skill effects per seat:
  - gunnery: reaction_time_ms, aim_spread_deg, lock_time_ms, burst_discipline.
  - tactical: target_priority_coherence, volley_timing, drone_tasking cadence.
  - sensors: acquisition_latency_ms, track_stability, target_loss_reacquire.

## Behavior Profile
Cooperation: coordinated crew; target sharing across seats.
Target sharing: broadcast to gunnery + tactical.
Discipline: hold fire during warmup; staggered volleys during live window.
Failure modes: lock churn, overfire on disabled targets, target switching thrash.

## Schedule Regime
Time base: scenario.
Profiles:
- gunnery: warmup (0-10s), live_fire (10-110s), cooldown (110-120s).
- tactical: warmup (0-10s), live_fire (10-110s), cooldown (110-120s).
- sensors: warmup (0-10s), live_fire (10-110s), cooldown (110-120s).
Training windows: live_fire session with gunnery + tactical + sensors overlap.
Attendance rules: missing any seat invalidates run.
Reuse notes: applicable to other gunnery/target acquisition drills.

## Needs and Shift Overrides
Needs modeled: hunger, fatigue, injury, morale.
Thresholds: soft 0.6, hard 0.85.
Override rules: soft finish current action; hard immediate interrupt.
Profile nuance: lawful/pure rarely break; chaotic unpredictable; corrupt selfish; warlike duty-bound.
Examples: elites may eat on shift if prolonged; injured crew sidelined during live fire.

## Profile Interplay and Outcomes
Interplay focus: cooperation + environment-driven (crew expression against targets and ship systems).
Assignment notes: explicit assignment, pool refs, avoid random.
Expected outcomes: profiles express doctrine via tool choice, target prioritization, and risk tolerance.
Outcome reporting: metrics broken down by profile tags (warlike vs spiritual vs materialist).
Examples: materialist elite biases drones; corrupt materialist uses suicide drones; pure materialist uses repair/support drones.
Notes: Interplay does not have to be conflict; the intent is dynamic profile expression with or against other profiles or the simulation itself.

## Experience and Doctrine Expression
Experience deltas: elites acquire faster, maintain lock, and avoid overfire; rookies show slower reacquire and higher miss rates.
Profile x experience nuance: warlike elites aggressive + tactically sharp; spiritual elites employ psionic/mana instruments; materialists deploy drones.
Doctrine/tooling expression: materialist drones; corrupt suicide drones; pure repair/support drones; warlike uses aggressive intercept drones.

## Addendum (Optional)
Path: Docs/Scenarios/GoalCards/Addenda/space4x_capital_shooting_range_notes.md
Notes: Longform nuance for profile doctrines, drone behaviors, and edge cases.

## Targeting and Fire Control
Detection: sensors-driven LOS, no ECM in base run.
Target selection: priority on closest threat, then highest velocity crossing target.
Lock time: reduced by experience; drones may assist with targeting.
Track loss: occurs on target occlusion or high angular velocity.
Firing solution: lead required on crossing targets; aim assist scales with experience.

## Movement and Orientation
Formation: capital ship stationary; drones follow scripted arcs.
Rotation limits: turret yaw/pitch rates scale with crew skill.
Facing rules: maintain target tracking in firing arc.
Speed profile: targets move at low/medium/high speed tiers.

## Weapons and Arcs
Weapon types: spinal mount (projectile), optional point defense.
Firing arcs: 120-180 deg forward arc.
Ammo and heat: finite heat budget; overheating penalizes cadence.

## Nuance Prompts (fill what applies)
Perception: sensor lag affects rookies more than elites.
Coordination: tactical seat staggers volleys to prevent overkill.
Reaction timing: gunnery pre-queues shots at high experience.
Skill/stat modifiers: seat-specific penalties under fatigue.
Morale/discipline: warlike elites ignore soft breaks during live fire.
Environment/interference: optional jamming variant later.
Failure cases: friendly fire on drones (if enabled), oscillation on crossing targets.
Determinism cues: fixed seed, fixed drone paths, fixed loadout.

## Script
1. Spawn capital ship and target drones; warmup (no scoring).
2. Live fire window with scripted target motion.
3. Cooldown window; stop firing; collect telemetry.

## Metrics
- reaction_time_ms: target acquisition to first shot.
- hit_accuracy_pct: hits / shots.
- time_to_kill_ms: mean time to kill per target.
- shots_per_kill: mean shots to destroy.
- energy_spent_per_kill: optional efficiency signal.
- lock_churn_rate: lost locks / minute.
- drone_assist_rate: drone-assisted hits / total hits.

## Scoring
- Score = 0.35 * accuracy + 0.25 * (1 - reaction_time_norm) + 0.25 * (1 - time_to_kill_norm) + 0.15 * efficiency.

## Acceptance
- Ordering: elite > veteran > experienced >= rookie across composite score.
- Minimum separation thresholds to avoid ties (TBD).
- Drone usage should match doctrine where applicable.

## Regression Guardrails
- Determinism preserved across identical seeds.
- No regressions in baseline hit_accuracy for elite tier.

## Nightly Focus
Scenario ID: space4x_capital_shooting_range_micro
Run budget: 120s sim, 1 seed per tier
Pass gates: composite score ordering, accuracy thresholds
Do not regress: determinism, lock_churn_rate
Priority work:
- Implement gunnery skill scaling and telemetry
- Add drone tasking interface per doctrine
- Wire target motion scripts and tracking
Telemetry IDs: space4x.q.gunnery.capital_range.score

## Branch Plan
Branch name: scenarios/space4x/capital-shooting-range
Merge criteria: pass gates + stable telemetry keys + reviewed by lead
Owner/Reviewer: shonh / TBD

## Variants
- Rapid yaw profile with crossing targets
- High-speed drones with low-visibility pass
- No-drone baseline

## Telemetry/Outputs
- space4x.q.gunnery.capital_range.score
- space4x.q.gunnery.capital_range.hit_rate
- space4x.q.gunnery.capital_range.reaction_time

## Dependencies
- Gunnery skill/experience scaling
- Drone tasking and doctrine mapping
- Target drone spawn + scripted motion
- Telemetry for lock, hit, time-to-kill

## Risks/Notes
- Gunnery/drone systems may be stubbed; treat as design target until implemented.

## Scenario JSON
Path: Assets/Scenarios/space4x_capital_shooting_range_micro.json
Version: v0
