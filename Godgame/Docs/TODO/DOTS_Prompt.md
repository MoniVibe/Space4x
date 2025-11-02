# DOTS Agent Prompt: Divine Hand Review & Fixes

## Context

- Feature slice: Godgame Divine Hand MVP (see `Docs/TODO/Hand.md` and TruthSources `Hand_StateMachine.md`, `RMBtruthsource.md`, `Slingshot_Contract.md`, `Aggregate_Resources.md`, `Storehouse_API.md`).
- Current ECS implementation lives under `Assets/Scripts/Godgame/Interaction/Hand/`.
  - `HandComponents.cs` defines the core state struct, right-click context buffer, events.
  - `RightClickSystems.cs` scaffolds the probe/router step (currently stubbed).
  - `DivineHandStateSystem.cs` drives state transitions and charge/cooldown bookkeeping.
  - `DivineHandAuthoring.cs` seeds the hand singleton/event buffers.
- DOTS review log in `Docs/TODO/DOTSRequest.md` highlights pending fixes from the first pass.

## Tasks for DOTS Agent

1. **Right-click affordance gating**
   - Update `RightClickProbeSystem` so each handler is only enqueued when its target passes the TruthSource guard:
     * `StorehouseDump`: confirm storehouse intake collider (`Storehouse_API`) under cursor and capacity > 0.
     * `PileSiphon`: verify aggregate pile with matching resource type and hand capacity remaining (`Aggregate_Resources`).
     * `GroundDrip`: ensure ground hit on valid layer for pile creation.
     * `Drag`/`SlingshotAim`: respect villager/resource rules.
   - Remove placeholder unconditional contexts; ensure router resolves to `HandRightClickHandler.None` when no handler qualifies.
   - Maintain Burst compatibility (no managed allocations, minimal custom structs).

2. **Cooldown field**
   - Extend `Hand` struct with `CooldownDurationSeconds` (authored via `DivineHandAuthoring.cooldownAfterThrowSeconds`).
   - Ensure `DivineHandStateSystem` uses `CooldownDurationSeconds` when setting `CooldownUntilSeconds` after throws/dumps.

3. **Charge handling**
   - Reset `ChargeSeconds` to 0 on entry into `SlingshotAim`.
   - Clamp accumulation using the live `handRW.ValueRW.ChargeSeconds` (not stale copies) and respect `MinChargeSeconds`/`MaxChargeSeconds`.
   - Guarantee charge starts at 0 and must reach `MinChargeSeconds` before `Release` succeeds.

4. **Validation**
   - Verify systems compile under Burst (no new dependencies beyond `Unity.Physics` / existing packages).
   - Update/extend tests or add temporary static asserts where helpful (e.g., state transitions guards).
   - Reflect any follow-on tasks back into `Docs/TODO/DOTSRequest.md`.

## Deliverables

- Updated ECS systems addressing the items above.
- Notes on tests run (PlayMode/Burst) and any newly discovered blockers.
- Documentation tweaks if new data contracts or constraints emerge.
