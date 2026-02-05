# Space4X Orbits + Reference Frames Contract (MVP)

Status: Draft contract for MVP implementation.
Source notes: C:\Dev\unity_clean\orbit.md

## 1) Purpose
Define a deterministic, scalable orbital model that supports galaxy -> system -> planet -> local
movement without heavy N-body simulation. The contract establishes the data model, system
ordering, invariants, and tests for hierarchical reference frames with on-rails orbits and
off-rails local physics.

## 2) MVP Decisions (Locked)

- **Frame hierarchy**: Galaxy -> Star System -> Planet -> Local Bubble (combat/docking/mining).
- **Authoritative system frame**: Star-fixed inertial system frame (not barycentric).
- **Orbit model**: Analytic Keplerian orbits + patched conics with SOI transitions.
- **Precision**: High-precision sim state (double or fixed) + float local physics and rendering.
- **SOI transitions**: Velocity and position continuity with hysteresis (enter < exit).
- **Local physics**: Off-rails only inside local bubble or SOI. On-rails elsewhere.
- **Presentation**: LocalTransform is derived, not authoritative; floating origin remains a
  presentation concern.

## 3) Design Tenets

- Determinism first: analytic orbits computed from tick and elements.
- Frames are explicit and composable.
- No giant float coordinates in simulation state.
- SOI transitions are reversible and continuity-preserving.
- Rendering does not feed back into simulation state.

## 4) Data Model (Components)

### Frame Entities

- `Space4XReferenceFrame`
  - `Kind`: Galaxy / StarSystem / Planet / LocalBubble / ShipBubble
  - `ParentFrame`: parent frame entity or null
  - `IsOnRails`: 1 if analytic orbit, 0 if local physics
  - `EpochTick`
  - `PositionInParent` (double3)
  - `VelocityInParent` (double3)

- `Space4XFrameTransform`
  - `PositionWorld` (double3)
  - `VelocityWorld` (double3)
  - `UpdatedTick`

- `Space4XOrbitalElements` (for on-rails frames/bodies)
  - `SemiMajorAxis`, `Eccentricity`, `Inclination`
  - `LongitudeOfAscendingNode`, `ArgumentOfPeriapsis`
  - `MeanAnomalyAtEpoch`, `Mu`, `EpochTick`

- `Space4XSOIRegion`
  - `EnterRadius` (double)
  - `ExitRadius` (double)

- `Space4XReferenceFrameRootTag`
  - Identifies the root (galaxy) frame.

- `Space4XReferenceFrameConfig`
  - `Enabled` (0/1)
  - `LocalBubbleRadius`
  - `EnterSOIMultiplier`, `ExitSOIMultiplier`

### Moving Entities

- `Space4XFrameMembership`
  - `Frame`: current reference frame entity
  - `LocalPosition` (float3)
  - `LocalVelocity` (float3)

- `Space4XFrameTransition`
  - `FromFrame`, `ToFrame`
  - `WorldPosition`, `WorldVelocity`
  - `TransitionTick`, `Pending`

## 5) System Ordering (MVP)

Initialization

1. `Space4XReferenceFrameBootstrapSystem`
   - Creates root frame if config enabled.

Simulation

2. `Space4XOrbitalEphemerisSystem`
   - Computes `PositionInParent` for on-rails frames from orbital elements.

3. `Space4XFrameTransformSystem`
   - Composes world transforms for frames (parent + local).

4. `Space4XSOITransitionSystem`
   - Detects boundary crossings and queues `Space4XFrameTransition`.

5. `Space4XFrameMembershipSyncSystem` (later)
   - Applies transitions, updates local positions/velocities.

Presentation

6. `Space4XFrameToLocalTransformSystem` (later)
   - Converts local positions to `LocalTransform` for rendering.

## 6) Transition Contract (SOI)

At transition tick:

- World position continuity:
  x(t-) == x(t+)

- World velocity continuity:
  v(t-) == v(t+)

Hysteresis:

- Enter radius < Exit radius to prevent thrashing.

## 7) On-Rails vs Off-Rails Rules

- On-rails:
  - Position and velocity derived from analytic orbit.
  - No local physics integration.

- Off-rails:
  - Local physics in the active bubble.
  - Frame membership remains stable until SOI change or bubble exit.

## 8) Determinism Constraints

- Orbital state derived from tick + elements.
- No simulation state depends on camera, UI, or render systems.
- Frame transitions are discrete events logged in components (not implicit).

## 9) Tests (MVP)

- SOI handoff continuity:
  - Random states crossing boundary.
  - Assert continuity of world position and velocity.

- Round-trip transform:
  - Convert system -> planet -> system and compare within epsilon.

- Determinism:
  - Run N ticks twice with same seed and compare hashed frame states.

- Stress:
  - 10k+ on-rails entities with floating origin shifts.
  - Verify no local jitter near camera bubble.

## 10) Notes on Current Implementation

`Space4XOrbitAnchor` and `Space4XOrbitDriftSystem` currently provide simple circular drift for
presentational motion. The new reference frame model supersedes this once enabled but should
coexist until the full transition pipeline is wired.

## 11) Next Steps

- Implement the stubs for components and systems behind `Space4XReferenceFrameConfig.Enabled`.
- Add a simple ephemeris solver (circular orbit placeholder -> Kepler solver).
- Wire SOI transitions for ships + local bubble membership.
- Add headless invariants for continuity tests.
