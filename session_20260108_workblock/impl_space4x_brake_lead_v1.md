# impl_space4x_brake_lead_v1

## Summary
- Adds stopping-distance brake lead scaling to vessel movement behind `BrakeLeadEnabled` (default off).
- Scales desired speed when remaining distance is below `stopDistance * BrakeLeadFactor`.

## Details
- `Assets/Scripts/Space4x/Systems/AI/VesselMovementSystem.cs`: computes stop distance from current speed and deceleration, then dampens desired speed when inside the lead distance.
- `Assets/Scripts/Space4x/Runtime/VesselComponents.cs`: config fields `BrakeLeadEnabled` (default 0) and `BrakeLeadFactor`.
- `Assets/Scripts/Space4x/Authoring/Space4XVesselMotionProfileAuthoring.cs`: exposes brake lead toggle + factor in authoring.

## Validation
- Not run (per instructions).
