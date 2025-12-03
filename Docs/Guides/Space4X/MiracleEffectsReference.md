# Miracle Effects Reference

**Status:** Engineering Reference  
**Category:** Space4X Miracle System  
**Created:** 2025-01-XX  
**Last Updated:** 2025-01-XX

---

## Existing Miracle Concepts

### Current Implementation

**Authoring Component:** `Space4XMiracleAuthoring`
- Located: `Assets/Scripts/Space4x/Authoring/Space4XMiracleAuthoring.cs`
- Uses PureDOTS miracle components: `MiracleDefinition`, `MiracleRuntimeState`, `MiracleTarget`, `MiracleCaster`
- Current types: `MiracleType.Fireball` (instant strike), `MiracleType.Shield` (sustained shield)
- Casting modes: `MiracleCastingMode.Instant`, `MiracleCastingMode.Sustained`
- Lifecycle states: `MiracleLifecycleState.Ready`, `MiracleLifecycleState.Active`, `MiracleLifecycleState.CoolingDown`

**Sample Scenes:**
- `Space4X_VesselDemo.unity` - Contains `Space4XMiracleRig` with instant strike and sustained shield examples
- `Space4X_MiningDemo_SubScene.unity` - Includes miracle rig for registry/telemetry coverage
- `Space4XRegistryDemo_SubScene.unity` - Miracle authoring examples

**Registry Integration:**
- Miracles emit telemetry (energy, cooldowns) via `Space4XRegistryBridgeSystem`
- Spatial indexing via `SpatialIndexedTag` for grid residency
- Telemetry counters track miracle energy and cooldown totals

### Current Gaps vs. Desired Effects

**Missing Effect Types:**
1. **Damage Types:** Currently only generic `Fireball` - need thermal/kinetic/radiant damage differentiation
2. **Impulse Control:** No knockback, dampening, or force application systems
3. **Spawning:** No construct/summon capabilities for spawning entities
4. **Time Manipulation:** No slow/haste/reverse time effects (time control exists at simulation level but not as miracle effects)

**Missing Multi-Effect Support:**
- Current system treats each miracle as a single type
- Need combinators for multi-effect miracles (e.g., Orbital Strike = damage + impulse)

**Missing Effect Payloads:**
- No structured effect data (magnitude, duration, area, stacking rules)
- Effects are implicit in `MiracleType` rather than explicit in data

---

## Registry Expectations

From `Docs/TODO/Space4x_PureDOTS_Integration_TODO.md`:
- Miracle activations emit telemetry events
- Miracle energy/cooldown telemetry is complete
- Pending: focused tests for miracle execution scenarios
- Pending: ability UX telemetry hooks (casting latency, cancellation)

---

## Telemetry Requirements

**Current Metrics:**
- Miracle energy totals
- Miracle cooldown totals
- Miracle lifecycle state

**Desired Metrics (to be added):**
- Damage dealt per miracle type
- Entities spawned per miracle
- Time dilation seconds applied
- Impulse forces applied

---

## Integration Points

**Spatial Services:**
- All miracle entities must have `SpatialIndexedTag`
- Miracles participate in spatial grid for area-of-effect queries

**Time System:**
- Time manipulation effects must integrate with PureDOTS `TimeState`
- Effects should respect deterministic tick requirements

**Combat System:**
- Damage effects route to hull/shield stats
- Impulse effects apply to physics/velocity components
- Effects should integrate with existing combat loop mechanics

---

## Next Steps

See `space-46be45.plan.md` for implementation plan covering:
1. Effect taxonomy and data schema
2. Authoring component expansion
3. Runtime effect processing systems
4. Presentation and telemetry hooks
5. Validation and testing




