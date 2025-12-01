# CombatState Component Contract

**Status**: Contract Definition  
**Target**: PureDOTS Sim Systems  
**Last Updated**: 2025-12-01

---

## Overview

This document defines the `CombatState` component contract that PureDOTS simulation systems must provide for Space4X combat presentation to function correctly.

---

## Component Definition

```csharp
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Presentation
{
    /// <summary>
    /// Combat-specific state for entities.
    /// Must be provided by PureDOTS sim systems for combat visualization.
    /// </summary>
    public struct CombatState : IComponentData
    {
        /// <summary>True if entity is currently in combat</summary>
        public bool IsInCombat;
        
        /// <summary>Target entity being engaged (Entity.Null if none)</summary>
        public Entity TargetEntity;
        
        /// <summary>Health ratio (0-1), current health / max health</summary>
        public float HealthRatio;
        
        /// <summary>Shield ratio (0-1), current shields / max shields</summary>
        public float ShieldRatio;
        
        /// <summary>Last tick when damage was taken</summary>
        public uint LastDamageTick;
        
        /// <summary>Current engagement phase</summary>
        public CombatEngagementPhase Phase;
    }

    /// <summary>
    /// Combat engagement phase enumeration.
    /// </summary>
    public enum CombatEngagementPhase : byte
    {
        None = 0,
        Approach = 1,
        Exchange = 2,
        Disengage = 3
    }
}
```

---

## Entity Requirements

### Carriers
- **Must have** `CombatState` when:
  - Carrier has locked a target (via `InterceptRequest` or similar)
  - Carrier is within engagement range of enemy
  - Carrier is firing weapons
- **Component Location**: Same entity as `Carrier` component

### Crafts (Strike Craft)
- **Optional** `CombatState` when:
  - Craft is actively engaging enemy (if strike craft have combat capability)
- **Component Location**: Same entity as `MiningVessel` or strike craft component

### Fleet Aggregates
- **Must have** `CombatState` when:
  - Fleet is in `Space4XFleet.Posture = Engaging`
  - Any carrier in fleet is in combat
- **Component Location**: Fleet impostor entity (aggregate combat state)

---

## Update Requirements

### When to Set `IsInCombat = true`
- When carrier locks target (via `InterceptRequest` or target lock system)
- When first shot is fired
- When carrier enters engagement range of enemy
- When fleet posture changes to `Engaging`

### When to Set `IsInCombat = false`
- When target is destroyed or out of range
- When retreat order is issued (`Space4XFleet.Posture = Retreating`)
- When engagement ends (no targets, no weapons firing)
- When fleet posture changes from `Engaging` to another state

### When to Update `LastDamageTick`
- **Immediately** when damage occurs
- Set to current `TimeState.CurrentTick`
- Used by presentation system for damage flash feedback

### When to Update `HealthRatio` / `ShieldRatio`
- **Every tick** during combat (if health/shields are changing)
- **On damage events** (immediate update)
- Derived from hull integrity / shield systems
- Range: 0.0 (destroyed/no shields) to 1.0 (full health/full shields)

### When to Update `TargetEntity`
- When target lock changes
- When target is destroyed (set to `Entity.Null`)
- When new target is acquired

### When to Update `Phase`
- **Approach**: When moving toward target but not yet in range
- **Exchange**: When within engagement range and weapons firing
- **Disengage**: When retreating or breaking off engagement
- **None**: When not in combat

---

## Update Frequency

- **During Combat**: Update every tick
- **On Damage Events**: Update immediately (same tick)
- **On Target Lock/Unlock**: Update immediately (same tick)
- **On Fleet Posture Change**: Update immediately (same tick)

---

## Integration Points

### PureDOTS Systems That Should Set CombatState

1. **Intercept/Combat Resolution System**:
   - Sets `IsInCombat = true` when intercept begins
   - Updates `TargetEntity` when target is locked
   - Updates `LastDamageTick` when damage occurs
   - Sets `IsInCombat = false` when intercept ends

2. **Hull Integrity System**:
   - Updates `HealthRatio` based on current hull health
   - Updates `ShieldRatio` based on current shield strength

3. **Fleet Management System**:
   - Sets fleet aggregate `CombatState` based on fleet posture
   - Aggregates combat state from individual carriers

### Space4X Presentation Systems That Read CombatState

1. **Space4XCombatPresentationSystem**:
   - Reads `CombatState` to determine visual state
   - Applies combat colors, shield glow, damage flash

2. **Space4XDamageFeedbackSystem**:
   - Reads `LastDamageTick` to trigger flash effects
   - Reads `HealthRatio` for health-based visuals

3. **Space4XProjectilePresentationSystem**:
   - May read `CombatState` to determine if projectiles should spawn
   - (If projectiles are sim entities, they may not need CombatState)

---

## Example Implementation

```csharp
// In PureDOTS intercept/combat system
public partial struct CombatResolutionSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var timeState = SystemAPI.GetSingleton<TimeState>();
        uint currentTick = timeState.CurrentTick;

        foreach (var (carrier, intercept, hull, combatState, entity) in SystemAPI
                     .Query<RefRO<Carrier>, RefRO<InterceptRequest>, RefRO<HullIntegrity>, RefRW<CombatState>>()
                     .WithEntityAccess())
        {
            // Update combat state based on intercept
            if (intercept.ValueRO.TargetEntity != Entity.Null)
            {
                combatState.ValueRW.IsInCombat = true;
                combatState.ValueRW.TargetEntity = intercept.ValueRO.TargetEntity;
                combatState.ValueRW.Phase = CombatEngagementPhase.Exchange;
            }
            else
            {
                combatState.ValueRW.IsInCombat = false;
                combatState.ValueRW.TargetEntity = Entity.Null;
                combatState.ValueRW.Phase = CombatEngagementPhase.None;
            }

            // Update health ratio
            combatState.ValueRW.HealthRatio = hull.ValueRO.CurrentHealth / math.max(0.0001f, hull.ValueRO.MaxHealth);
            
            // Update shield ratio (if shields exist)
            if (SystemAPI.HasComponent<ShieldIntegrity>(entity))
            {
                var shields = SystemAPI.GetComponent<ShieldIntegrity>(entity);
                combatState.ValueRW.ShieldRatio = shields.CurrentShields / math.max(0.0001f, shields.MaxShields);
            }
            else
            {
                combatState.ValueRW.ShieldRatio = 0f;
            }

            // Update last damage tick (if damage occurred this tick)
            if (hull.ValueRO.LastDamageTick == currentTick)
            {
                combatState.ValueRW.LastDamageTick = currentTick;
            }
        }
    }
}
```

---

## Testing

Until PureDOTS sim systems provide `CombatState`, a temporary test harness is available:

- **File**: `Assets/Scripts/Space4x/Presentation/Space4XCombatStateTestHarness.cs`
- **Usage**: Only active when `#define SPACE4X_DEBUG_COMBAT_STATE` is defined
- **Purpose**: Simulate `CombatState` for developer testing

---

## Notes for PureDOTS Team

- `CombatState` is a presentation component but must be set by sim systems
- Presentation systems are read-only from sim components
- `CombatState` should be updated every tick during combat for smooth visuals
- `LastDamageTick` must be updated immediately when damage occurs (same tick)
- Fleet aggregates should have aggregated `CombatState` based on fleet posture

---

**End of Contract**

