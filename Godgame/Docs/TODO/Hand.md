# Godgame Divine Hand Plan

This document specializes the generalized “god hand” capture for our Godgame build. It anchors to the TruthSources (`Hand_StateMachine.md`, `RMBtruthsource.md`, `Slingshot_Contract.md`, `Aggregate_Resources.md`, `Storehouse_API.md`) and to the Black & White 2 reference. Use this as the authoritative backlog for Interaction assembly work; keep it in sync with `Docs/TODO/Godgame_PureDOTS_Integration_TODO.md` and session notes.

## Goals & principles

- Pure DOTS data flow: inputs → intent/state → physics/resource systems → visuals (FX/UI).
- Deterministic and Burst-friendly: keep input bridges on the main thread, everything else Burst/job.
- One-frame intent, multi-frame execution: the hand emits commands, specialized systems execute them.
- Physical credibility: held objects follow via PD control; throws are impulses derived from hand velocity.
- Single source of truth: `Hand` component owns state; other systems react but do not override transitions.

## Black & White 2 baseline beats (carry forward unless scoped out)

- Left-click drags orbit the camera; middle mouse pans; scroll zooms. `CameraRig` drives orbit around influence center.
- Right-click interacts: hold over pile to siphon, over storehouse to dump, over ground to drip a new pile, drag into slingshot aim to throw.
- The hand carries one resource type at a time. Capacity starts at 10000 units (tunable).
- Throw strength is flick-based with optional charge; longer aim increases impulse up to a cap.
- T hotkey toggles throw modes, with a slingshot mode for charge arc an object into a roughly precalculated trajectory, "stretching" and aiming with cursor movements, Alt+hold to lock XZ axis and allow for Y axis aim mode, returning to unlock XZ on alt release 
- Shift+Releasing an object queues that object for launch, holding it until a release object (or all objects) is pressed, releasing it with the saved impulse and trajectory
- HUD shows held type/amount, villager info when carrying humans, and charge feedback during slingshot.

## TruthSource alignment

- State machine and guards mirror `Hand_StateMachine.md`.
- Handler priority/order matches `RMBtruthsource.md`: `UI > ModalTool > StorehouseDump > PileSiphon > Drag > GroundDrip`.
- Slingshot aim/throw flow follows `Slingshot_Contract.md` (`BeginAim`, `UpdateAim`, `Release`, `Cancel`).
- Resource APIs use `Aggregate_Resources.md` and `Storehouse_API.md`; no ad-hoc mutations.
- Telemetry funnels into PureDOTS metrics via `TelemetryMetric` (`TelemetryStream` singleton).

## Entities & components (core schema)

```csharp
public struct InputState : IComponentData
{
    public float2 PointerPos;     // screen coordinates
    public float2 PointerDelta;   // per-frame screen delta
    public float  Scroll;
    public bool PrimaryHeld;      // LMB
    public bool SecondaryHeld;    // RMB
    public bool MiddleHeld;       // MMB
    public bool ThrowModifier;    // e.g., Shift for precision
}
```

```csharp
public struct CameraRig : IComponentData
{
    public float3 Pivot;
    public float  Distance;
    public float  PitchDeg;
    public float  YawDeg;
    public float  OrbitSmoothing;
    public float  ZoomMin, ZoomMax;
    public float  PitchMin, PitchMax;
}
```

```csharp
public enum HandState : byte
{
    Empty,
    Holding,
    Dragging,
    SlingshotAim,
    Dumping
}

public struct Hand : IComponentData
{
    public HandState State;
    public float3    WorldPos;
    public float3    PrevWorldPos;
    public float3    AimDir;
    public Entity    Hovered;
    public Entity    Grabbed;          // non-null when carrying a world entity
    public ResourceType HeldType;
    public bool      HasHeldType;
    public int       HeldAmount;
    public int       HeldCapacity;
    public float     MaxCarryMass;
    public float     GrabLiftHeight;
    public float     ThrowScalar;
    public float     CooldownUntilSeconds;
    public float     MinChargeSeconds;
    public float     MaxChargeSeconds;
    public float     ChargeSeconds;
    public float     DumpRatePerSecond;
    public float     SiphonRange;
}
```

```csharp
public struct HandHistory : IComponentData
{
    public float3 V0, V1, V2, V3;   // most recent hand velocities
}

public struct HandStateChanged : IBufferElementData
{
    public HandState From;
    public HandState To;
}

public struct HandCarryingChanged : IBufferElementData
{
    public bool HasResource;
    public ResourceType Type;
    public int Amount;
    public int Capacity;
}
```

```csharp
public enum ResourceType : byte { Wood, Ore, Food, Worship }

public struct Pickupable : IComponentData
{
    public float Mass;
    public float3 GrabOffsetLocal;
    public float MaxHoldSpeed;
}

public struct Grabbed : IComponentData
{
    public Entity Hand;
    public float3 LocalOffset;
    public float3 LastHandPos;
}
```

```csharp
public struct ResourceSource : IComponentData
{
    public ResourceType Type;
    public float Amount;
    public float MaxRatePerSec;
    public float3 Outlet;
}

public struct ResourceSink : IComponentData
{
    public ResourceType Type;
    public float Amount;
    public float Capacity;
    public float MaxRatePerSec;
    public float3 Inlet;
}

public struct SiphonChannel : IComponentData
{
    public Entity Source;
    public Entity Sink;
    public float  Rate;
    public float  MaxDistance;
}
```

Additional data (telemetry events, HUD tags, etc.) should stay Burst-friendly (`FixedString64Bytes`, no managed refs).

## System graph & update order (MVP slice)

1. `InputGatherSystem` (Mono bridge in `Godgame.Interaction`): reads New Input System actions and writes `InputState`.
2. `CameraOrbitSystem` (Simulation, pre-physics): updates `CameraRig`, rays ground to seed `Hand.WorldPos`.
3. `RightClickProbeSystem` (Simulation): performs one Physics raycast, fills a `RightClickContext` buffer ordered by `RMBtruthsource.md`.
4. `RightClickRouterSystem` (Simulation): picks winning handler (`StorehouseDump`, `PileSiphon`, `Drag`, `GroundDrip`, `SlingshotAim`) and writes `RightClickResolved`.
5. `DivineHandStateSystem` (Simulation): applies `Hand_StateMachine` guards, mutates `Hand`, and emits state/carry events.
6. `HandCarrySystem` (Simulation, pre-physics): positions grabbed entities, manages PD follow, processes villager interrupts.
7. `HandSlingshotSystem` (Simulation): updates charge, aim, and on release computes throw impulses.
8. `HandDumpSystem` (Simulation): handles storehouse transfers and ground drips via resource/storehouse APIs.
9. `HandTelemetrySystem` (Simulation, post-handlers): batches `TelemetryMetric` entries (siphon, dump, throw).
10. `HandPresentationSystem` (Presentation): drives cursor mesh/VFX/HUD updates.

## State machine & guards

- `Empty`: no cargo. Primary pickup on valid target enters `Holding`. Right-click resolves handlers but dumping/throwing are disallowed.
- `Holding`: carrying resources or a villager. Guards enforce single resource type, capacity clamp, and cooldown. Transitions:
  - → `Dragging` when router resolves drag behavior (villager reposition).
  - → `SlingshotAim` when router resolves slingshot and `HasCharge` passes.
  - → `Dumping` while storehouse dump succeeds.
  - → `Empty` when cargo reaches zero or on cancel.
- `Dragging`: maintains tether for world entities; returns to `Holding` on release or `Empty` if entity destroyed.
- `SlingshotAim`: accumulates `ChargeSeconds` between min/max, keeps aim ray normalized. `Release` applies impulse and clears cargo; `Cancel` returns to `Holding`.
- `Dumping`: continues while right-click Performed over storehouse and space remains; falls back to `GroundDrip` when storehouse rejects input.

Each transition emits `HandStateChanged`; carry delta fires `HandCarryingChanged`. Additional event structs (`HandEventSiphon`, `HandEventDump`, `HandEventThrow`) feed telemetry and VFX.

## Pickup & carry

- Primary press on pickupable checks `MaxCarryMass` and `Pickupable` tag. On success: disable gravity (`PhysicsGravityFactor = 0`), raise damping (`PhysicsDamping`), add `Grabbed`.
- Villagers: set `VillagerState` to `Interrupted`, emit `OnInterrupted(HandPickup)`, break job ownership. Release restores previous state or enters `Interrupted` if thrown.
- Resource piles: we operate virtually—call `AggregatePile.Take(int)` to fill `Hand.HeldAmount`, leaving pile amounts authoritative.
- Held metadata: `HasHeldType` differentiates resource cargo from villager/object carry. HUD listens to `HandCarryingChanged`.

## Right-click handler breakdown

- **StorehouseDump**: Highest priority beneath UI. Guard: `HasHeldType`, `Storehouse.Space(type) > 0`, within intake cone, cooldown elapsed. Behavior: `accepted = storehouse.Add(type, request)`; remainder stays in hand or spills to ground if zero.
- **PileSiphon**: Guard: matching resource pile, capacity remaining, pile amount > 0. Transfer per frame `delta = min(handRate, pile.MaxRatePerSec, capacityRemaining) * dt`. When hand reaches >0, state becomes `Holding`.
- **Drag**: For villager reposition and special props. Maintains `Hand.State == Dragging`, writes desired position for follower system.
- **GroundDrip**: Guard: `HasHeldType`, valid ground layer. Transfers `min(DumpRatePerSecond * dt, HeldAmount)` to `AggregatePile.Add`. Spawns/merges piles according to truthsource merge rules.
- **SlingshotAim**: Guard: `Hand.State == Holding`, router hit passes `SlingshotMask`, `HeldAmount == 0 || slingshot allows resources`. `BeginAim` zeroes charge, caches aim origin; `UpdateAim` increments `ChargeSeconds`; `Release` calculates impulse and clears cargo (villagers become ragdolls or resume behavior after landing).

## Throw tuning (B&W2 feel)

- Maintain `HandHistory` velocities; weight recent frames higher (`v = 0.4*V0 + 0.3*V1 + 0.2*V2 + 0.1*V3`).
- Impulse: `impulse = normalized(v) * (baseScalar + flickScalar * length(v)) * massScale`, with `massScale = math.rsqrt(mass + 0.25f)`.
- Charge multiplier: `mult = math.lerp(1f, maxChargeMultiplier, ChargeSeconds / MaxChargeSeconds)`.
- Optional spin from lateral delta clamped to avoid thrashing.

## Resource accounting & storehouse loop

- All transfers go through APIs (`AggregatePile.Take/Add`, `Storehouse.Add/Remove/Space`). No direct field edits.
- Siphon/dump share the same conservation rules; totals reconcile in storehouse tests.
- Telemetry keys (`Hand.PileSiphon`, `Hand.StorehouseDump`, `Hand.GroundDrip`, `Hand.Throw`) use `FixedString64Bytes`.

## HUD & feedback

- HUD subscribes to `HandCarryingChanged` and `HandStateChanged` dynamic buffers.
- Cursor hints mirror copy from `RMBtruthsource.md`.
- Charge UI: radial fill anchored to cursor based on `ChargeSeconds`.
- Audio hooks triggered via dedicated event buffers to stay Burst-safe.
- VFX: siphon beam (source→hand), dump burst (hand→storehouse), throw trail. Keep placeholder particle prefabs until polish.

## Bootstrap & authoring checklist

- Systems live under `Assets/Scripts/Godgame/Interaction/` with namespace `Godgame.Interaction.Hand`.
- Bakers:
  - `DivineHandAuthoring` creates Hand singleton, HandHistory, event buffers, and seeds defaults (capacity, rates).
  - `CameraRigAuthoring` writes `CameraRig`.
  - Pile/storehouse bakers already exist per TruthSources; ensure layers/tags match `RMBtruthsource.md`.
- Tests:
  - PlayMode: siphon 200 units from pile, dump into storehouse, assert totals, events, telemetry.
  - PlayMode: slingshot villager and verify impulse threshold and state restoration.
  - Frame-rate independence: run siphon/dump script at 30 FPS vs 120 FPS; difference ≤ 1 unit.

## Out-of-scope for first pass

- Mass selection and multi-carry.
- Creature/miracle gestures (defer to later slices).
- Advanced VFX/audio pooling beyond placeholders.

## Edge cases & guardrails

- Storehouse full: emit deny feedback once per second, fall back to `GroundDrip`.
- Pile depleted mid-siphon: router re-evaluates; hand returns to `Empty` if no cargo.
- Villager release over invalid ground: snap via nav/path service before clearing `Grabbed`.
- Cooldown: after throw/dump, set `CooldownUntilSeconds = currentTime + cooldownAfterThrowSeconds`.
- No managed allocations inside systems; prefer `FixedList`/`NativeArray` stacks.

## Test matrix summary

- Table-driven tests for router priority (UI, StorehouseDump, PileSiphon, Drag, GroundDrip, SlingshotAim).
- Integration: `Gather → Hand siphon → Storehouse dump` verifies conservation and telemetry.
- Burst validation: `DivineHandStateSystem` guard logic runs under Burst tests (no managed code).

## Implementation checklist (MVP)

- [ ] `InputGatherSystem` bridge + `CameraOrbitSystem`.
- [ ] `RightClickProbeSystem` + `RightClickRouterSystem` honoring TruthSource priorities.
- [ ] `DivineHandStateSystem` with state/carry event buffers and cooldown tracking.
- [ ] `HandCarrySystem` for PD follow + villager interrupts.
- [ ] `HandSlingshotSystem` including charge + impulse computation.
- [ ] `HandDumpSystem` covering storehouse transfer and ground drip.
- [ ] `HandTelemetrySystem` + HUD bindings.
- [ ] PlayMode/Frame-rate tests listed above.

> DOTS review request logged in `Docs/TODO/DOTSRequest.md`.

### Review notes (pending fixes)

- Gate right-click contexts on actual targets before adding them; current stub always promotes `StorehouseDump`, forcing `DivineHandStateSystem` into `Dumping` even off-target.
- Add a dedicated `CooldownDurationSeconds` field to `Hand` (authoring-provided) and use it when setting `CooldownUntilSeconds`; do not reuse `MinChargeSeconds`.
- Reset `ChargeSeconds` to 0 when entering `SlingshotAim` and clamp using the live writeback value so charges build from zero as TruthSource specifies.

### Parallel workstreams (suggested)

- Input bridge + camera: implement the `PlayerInput` Mono bridge that writes `InputState` each frame and finish `CameraOrbitSystem` ray grounding so the hand has a stable world anchor.
- Targeting affordances: create systems/components for detecting storehouse intake colliders, pile hits, and ground layers, exposing lightweight data (`HandTargetMetadata`) that `RightClickProbeSystem` can query without extra physics calls.
- Hand carry/PD: implement `HandCarrySystem` with PD follow, gravity toggles, villager interrupt integration, and tests for jitter limits.
- Resource/storehouse glue: wire `HandDumpSystem` and `HandSiphonSystem` to the existing `AggregatePile`/`Storehouse` APIs, including telemetry writes and conservation tests.
- Slingshot execution: author projectile/villager throw logic (impulse, spin, cooldown application) and PlayMode tests verifying charge thresholds.
- HUD & events: add UI listeners for `HandStateChanged`, `HandCarryingChanged`, and populate cursor hints per `RMBtruthsource.md`.
- Telemetry: design `HandTelemetrySystem` entries aligned with PureDOTS metrics (`TelemetryStream`) so throws/siphons surface in the shared HUD.
