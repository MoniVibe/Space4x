# impl_space4x_brake_lead_v1

## Summary
- Adds stopping-distance brake lead scaling to vessel movement via `BrakeLeadFactor` (default 0 = off).
- Scales desired speed when remaining distance is below `stopDistance * BrakeLeadFactor`.

## Details
- `Assets/Scripts/Space4x/Systems/AI/VesselMovementSystem.cs`: computes stop distance from current speed and deceleration, then dampens desired speed when inside the lead distance.
- `Assets/Scripts/Space4x/Runtime/VesselComponents.cs`: config field `BrakeLeadFactor` (default 0).
- `Assets/Scripts/Space4x/Authoring/Space4XVesselMotionProfileAuthoring.cs`: exposes brake lead factor in authoring.

## Validation
- Not run (per instructions).
