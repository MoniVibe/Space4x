# Movement Polish Checklist (Space4X)

Purpose: define the “polished movement” target for ships in a realistic, inertial space simulation.

## Core Principles
- Ships are true inertials. Velocity persists unless thrust or braking is applied.
- Turning does not auto-zero velocity. Arcs are expected.
- Braking is explicit and readable (retrograde burn or thrust-vectoring).
- Movement is performance aware and deterministic.
- Frame anchoring is stable (system vs galaxy drift is invisible to the player).

## Simulation Contract (Non-Negotiable)
- One movement authority per entity per tick.
- Authoritative transform/velocity updates happen only in fixed-step simulation systems.
- UI/MonoBehaviour layers emit intent, never final transform writes.
- `LocalToWorld` is presentation output, not gameplay authority.

## Polished Movement Criteria
1. Inertial feel is obvious within the first 5–10 seconds.
2. Approach profile is stable and class-appropriate.
3. Braking starts intentionally, not as a last-moment snap.
4. Final settle is controlled with minimal oscillation.
5. Skill deltas are visible in one run.
6. No hidden damping or magical corrections.
7. No NaNs, no runaway velocities, no jitter spikes.

## Class Differentiation
- Carriers: slow turn-in, long braking distance, heavy inertia.
- Miners: moderate inertia, shorter braking than carriers.
- Strike craft: agile turn-in, short braking, fast reorientation.

## Pilot Skill Differentiation
- Rookie: reaction lag, overshoot, jitter, slower settle.
- Elite: smooth arcs, minimal overshoot, accurate brake timing.
- Differences are visible without needing aggregate statistics.

## Collision Response (Movement)
- Momentum is conserved at contact; mass dominates outcomes.
- No sticky collisions; resolve with impulse plus separation.
- Post-impact drift is inertial; no hidden damping.
- Off-center hits impart angular velocity; decay only via intentional dampers.

## Explosion Impulse (Movement)
- Radial impulse scaled by distance and mass.
- Debris receives high impulse; main hull receives modest impulse.
- Impulses are readable and coherent, not random jitter.
- Large impulses do not cause tunneling or NaN velocities.

## Readability Targets
- Impacts cause immediate, visible vector change.
- Braking is legible and intentional.
- Ships continue along new vectors until corrected.

## Suggested Metrics
- Time-to-target (class-scaled window).
- Overshoot distance (class-scaled threshold).
- Settle time (from braking start to stable stop).
- Peak lateral velocity during turn-in.
- Skill delta: overshoot or settle time difference.

## Acceptance Checklist
- Inertial behavior is obvious and consistent.
- Class differences are visible in a single observation run.
- Skill differences are visible in a single observation run.
- Collision/explosion responses are readable and stable.
- No determinism regressions.
