# Space4X Movement Kernel (Pure Slice)

## Goal

Run flagship movement through one clear writer path so we can separate intended motion from side-effects.

## Current toggle

- Controller field: `Space4XPlayerFlagshipController.pureMovementKernelMode`
- Input bit: `PlayerFlagshipFlightInput.PureKernelMode`
- Default in current branch: `true`

## Pure slice contract

When `PureKernelMode == 1` and manual movement is enabled:

1. `Space4XPlayerFlagshipInputSystem` is the authoritative writer for flagship `LocalTransform`.
2. It writes `ShipFlightRuntimeState` and `VesselMovement` from the same computed velocity.
3. It bypasses legacy movement ramps and local velocity clamping.
4. `Space4XThreatBehaviorSystem` is blocked from writing transforms on `PlayerFlagshipTag`.
5. `Space4XPlayerFlagshipKernelGuardSystem` asserts kernel ownership and reverts any non-kernel transform overwrite in the same tick.

## Why

- Removes hidden disagreement between `runtime_speed` and `vessel_speed`.
- Makes movement diagnosis easier: one integrator, one intent source.
- Lets us add features back deliberately, one at a time.

## Next reintegration order (recommended)

1. Add optional accel/decel shaping back (off by default).
2. Add optional damping model.
3. Add optional collision/sweep response.
4. Add optional orbit/continuum coupling.

Each step should be behind an explicit kernel setting and validated with probe logs.
