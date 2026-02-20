# Mode 3 RTS Z-Command Plan

## Goal

Implement a Homeworld-style Mode 3 command flow where move orders can target meaningful 3D positions (including Z/height), while preserving current queue and attack-move semantics.

## Current Baseline

- Mode 3 camera supports free-fly and optional Y-axis lock.
- Right-click orders currently carry only screen position + modifiers.
- Context order resolution raycasts and falls back to a fixed `y=0` plane.
- Result: many move orders collapse to planar movement when no collider hit exists.

## Target Behavior

1. Right-click in Mode 3 issues a move/attack/harvest order with a true 3D target.
2. Player can control command height explicitly (movement plane workflow).
3. Existing semantics remain:
- `Shift`: queue orders.
- `Ctrl`: convert move to attack-move.
4. No hardcoded movement hacks: execution remains module/profile driven in movement systems.

## Control Scheme (Phaseable)

1. Baseline RTS-Z:
- Maintain a persistent movement plane (`origin`, `normal`, `height`).
- Right-click projects cursor ray onto this plane if no valid entity/world hit exists.
2. Height adjustment:
- Hold `M` + mouse wheel (or vertical drag) adjusts movement plane height.
- Plane height persists until changed.
3. UX visibility:
- Preview marker at projected target.
- Vertical guide line from plane target toward nearest reference plane/object.

## Data Contract Changes

1. Extend `RightClickEvent` payload (PureDOTS package) with optional resolved world data:
- `HasWorldPoint`
- `WorldPoint`
- `HasHitEntity`
- `HitEntity`
- `HitGround`
2. Add Mode 3 movement-plane state singleton (Space4X or PureDOTS input domain):
- `PlaneOrigin`
- `PlaneNormal`
- `PlaneHeight`
- `LastCommandPoint`
3. Optional preview buffer/event for presentation-only drawing.

## System Changes

1. `RtsInputBridge`
- Resolve pointer ray at emit time.
- Populate enriched right-click payload where possible.
- Keep legacy fields for compatibility.
2. `Space4XCameraRigController`
- Own/update movement-plane height controls in Mode 3.
- Publish plane state for order systems.
3. `Space4XContextOrderSystem`
- Prefer explicit event world point.
- Else intersect ray with active movement plane.
- Only fallback to fixed world plane as last resort.
4. Order application remains unchanged structurally:
- Continue writing `OrderQueueElement` with full `float3 TargetPosition`.

## Implementation Phases

1. Phase 1 (functional)
- Add movement plane state.
- Add 3D projection path in context order.
- Preserve Shift/Ctrl behavior.
2. Phase 2 (usability)
- Add preview marker + height line.
- Add clear input hinting in debug overlay.
3. Phase 3 (polish)
- Add optional Homeworld-style movement-disc rendering and altitude snapping options.
- Tune defaults for readability at different zoom distances.

## Validation Checklist

1. Mode 3 right-click issues non-zero-height targets without requiring collider hits.
2. `Shift` queue and `Ctrl` attack-move still function identically.
3. Mixed altitude orders execute in sequence with expected vessel traversal.
4. No regressions in Mode 1/Mode 2 control paths.
5. Determinism and fixed-step behavior remain stable in ECS replay/rewind scenarios.

## Initial File Touchpoints

- `puredots/Packages/com.moni.puredots/Runtime/Input/RtsInputEvents.cs`
- `puredots/Packages/com.moni.puredots/Runtime/Input/RtsInputBridge.cs`
- `space4x/Assets/Scripts/Space4x/Camera/Space4XCameraRigController.cs`
- `space4x/Assets/Scripts/Space4x/Systems/Space4XContextOrderSystem.cs`

