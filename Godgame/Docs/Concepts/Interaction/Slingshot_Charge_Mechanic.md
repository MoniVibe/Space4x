# Slingshot Charge Mechanic

**Status:** In Development - <WIP: Projectile spawning not implemented>  
**Category:** Mechanic - Hand Interaction  
**Complexity:** Medium  
**Created:** 2025-10-31  
**Last Updated:** 2025-10-31

**⚠️ CURRENT STATE:**
- ✅ Charge accumulation implemented in `DivineHandStateSystem`
- ✅ State transitions implemented (Holding → SlingshotAim → Holding/Empty)
- ✅ Min/max charge validation exists
- ❌ Projectile spawning NOT implemented (no throw happens on release)
- ❌ Visual feedback (rubber band, trajectory) NOT implemented
- <NEEDS SPEC: Throw amount - how much cargo per throw?>

---

## Overview

**Purpose:** Convert hold duration into projectile velocity for throwing held objects  
**Player Impact:** Enables skill-based aiming - longer charge = farther throw  
**System Role:** Core hand interaction mechanic for resource/villager distribution

---

## How It Works

### Inputs
- RMB input phase: `Started` (begin charge), `Performed` (holding), `Canceled` (release)
- Hold duration: Time accumulator (0 → max charge time)
- Hand state: `Hand.State == HoldingResource` or `HoldingVillager`
- Cursor position: World position from raycast

### Process
1. **On RMB Started:**
   - Check guard: `Hand.Amount > 0` (has cargo)
   - Set `SlingshotState.ChargeTime = 0`
   - Set `Hand.State = SlingshotAim`
   - Store `SlingshotState.AimDirection = CursorWorldPos - HandPosition`

2. **On RMB Performed (each frame while holding):**
   - `ChargeTime += Time.deltaTime`
   - Clamp: `ChargeTime = min(ChargeTime, MaxCharge)`
   - Update aim ray: `AimDirection = CursorWorldPos - HandPosition`
   - Compute speed from curve: `Speed = Lerp(MinSpeed, MaxSpeed, ChargeTime / MaxCharge)`

3. **On RMB Canceled (release):**
   - If `ChargeTime >= MinCharge`:
     - Spawn projectile entity
     - Apply velocity: `Velocity = AimDirection.normalized × Speed`
     - Deduct from hand: `Hand.Amount -= ThrowAmount`
     - Set cooldown: `LastThrowTick = CurrentTick`
     - Transition: `Hand.State = Holding` (or `Empty` if amount = 0)
   - Else (released too fast):
     - Cancel throw, show feedback "Hold longer"

### Outputs
- Projectile entity with physics velocity
- Hand amount decreased
- Visual/audio feedback (whoosh, trajectory preview)
- State transition: `SlingshotAim → Holding/Empty`

---

## Rules

1. **Minimum Charge:** Must hold ≥ 0.2s to throw
   - Condition: `ChargeTime < MinCharge` on release
   - Effect: Cancel throw, play error sound

2. **Maximum Charge:** Caps at 2.0s (no benefit beyond)
   - Condition: `ChargeTime >= MaxCharge`
   - Effect: Speed plateaus, visual indicator (full charge glow)

3. **Cargo Requirement:** Can only charge with cargo
   - Condition: `Hand.Amount == 0`
   - Effect: Block transition to `SlingshotAim`, show error

### Edge Cases
- Cargo depleted during charge (hand siphoned by another system) → Auto-cancel charge
- Cursor moves off-screen → Use last valid aim direction
- Game paused mid-charge → Preserve charge time, resume on unpause

### Priority Order
1. Cargo check (must have cargo)
2. Input validation (RMB not blocked by UI)
3. Charge accumulation (time-based)
4. Release validation (min charge met)

---

## Parameters

| Parameter | Default | Range | Impact |
|-----------|---------|-------|--------|
| MinCharge | 0.2s | 0.1-0.5s | Prevents accidental taps |
| MaxCharge | 2.0s | 1.0-5.0s | Max power ceiling |
| MinSpeed | 5 m/s | 1-10 m/s | Gentle toss distance |
| MaxSpeed | 50 m/s | 20-100 m/s | Max throw distance |
| Cooldown | 0.5s | 0.1-2.0s | Throw spam prevention |

---

## Example

**Given:** Hand holding 500 wood, cursor over distant storehouse  
**When:** Player presses RMB, holds for 1.5 seconds, releases  
**Then:** 
1. RMB Started → `Hand.State = SlingshotAim`, `ChargeTime = 0`
2. Each frame → `ChargeTime += dt` (reaches 1.5s), `Speed = Lerp(5, 50, 1.5/2.0) = 42.5 m/s`
3. RMB Canceled → 
   - Spawn wood projectile entity
   - Set velocity: `(StorehousePos - HandPos).normalized × 42.5`
   - Hand amount: `500 - 100 = 400` (threw 100 units)
   - State: `SlingshotAim → Holding`
   - Projectile flies in arc, lands near storehouse

---

## Player Feedback

- **Visual:** Rubber-band stretch effect from hand to cursor (length = charge %), trajectory arc preview (dotted line)
- **Audio:** Tension creak (pitch rises with charge), whoosh on release (volume = speed)
- **UI:** Circular charge meter around cursor (fills 0-100%), "CHARGED" text at max

---

## Balance

- **Early:** Players learn with short throws (stone's throw range)
- **Mid:** Mastery enables cross-map resource delivery
- **Late:** Speedrunners optimize throw arcs for villager transport

### Exploits
- Infinite distance by charging forever → Capped at MaxCharge (2.0s)
- Machine-gun throws by rapid click → Cooldown (0.5s) prevents

---

## Interaction Matrix

| Other Mechanic | Relationship | Notes |
|----------------|--------------|-------|
| Hand State Machine | Dependency | SlingshotAim is a hand state |
| RMB Priority Router | Consumer | Registers as priority 40 |
| Projectile Physics | Output | Creates physics entities |
| Pile Siphon | Conflict | Can't siphon while aiming |

---

## Technical

- **Max entities:** 10 projectiles in-flight simultaneously
- **Update freq:** Per frame (charge accumulation), on input event (state changes)
- **Data needs:** `SlingshotState` component, charge curve (linear lerp), physics layer mask

---

## Tests

- [ ] ChargeTime < MinCharge → Cancel, no throw
- [ ] ChargeTime = MaxCharge → Speed plateaus
- [ ] Release with 0 cargo → Error, no projectile
- [ ] Charge interrupted → State returns to Holding
- [ ] 30 FPS vs 120 FPS → Same charge time to speed mapping
- [ ] Cooldown prevents spam (max 2 throws/second)
- [ ] Aim direction updates smoothly during charge

---

## Open Questions

1. Should charge curve be linear or exponential (more power at end)?
2. Should we show trajectory preview or let player learn by feel?

---

## Version History

- **v0.1 - 2025-10-31:** Initial mechanic spec from legacy Slingshot_Contract.md

---

## Related Mechanics

- Hand State Machine: `Docs/Concepts/Interaction/Hand_State_Machine.md`
- Projectile Physics: `Docs/Concepts/Interaction/Projectile_Arc.md` (to be created)
- RMB Priority: `Docs/Concepts/Interaction/RMB_Priority.md`

---

## Truth Source Mapping

**Existing Components (✅ Already Implemented):**

```csharp
// Assets/Scripts/Godgame/Interaction/Hand/HandComponents.cs (VERIFIED)
public struct Hand : IComponentData {
    public HandState State;              // ✅ Includes SlingshotAim
    public ResourceType HeldType;        // ✅ What's being held
    public int HeldAmount;               // ✅ How much cargo
    public int HeldCapacity;             // ✅ Max cargo
    public Entity Grabbed;               // ✅ Grabbed entity (villager/object)
    
    // Slingshot-specific (✅ ALREADY EXISTS!)
    public float MinChargeSeconds;       // ✅ Min charge time
    public float MaxChargeSeconds;       // ✅ Max charge time  
    public float ChargeSeconds;          // ✅ Current charge accumulator
    public float CooldownUntilSeconds;   // ✅ Cooldown timer
    public float CooldownDurationSeconds;// ✅ Cooldown config
}

// ✅ State enum includes SlingshotAim
public enum HandState : byte {
    Empty = 0,
    Holding = 1,
    Dragging = 2,
    SlingshotAim = 3,  // ✅ EXISTS
    Dumping = 4
}

// ✅ Handler enum includes SlingshotAim
public enum HandRightClickHandler : byte {
    None = 0,
    StorehouseDump = 1,
    PileSiphon = 2,
    // ... others
    SlingshotAim = 7  // ✅ EXISTS
}

// ✅ Priority constants exist
public static class HandRightClickPriority {
    public const int SlingshotAim = 60;  // ✅ EXISTS
}
```

**Already Implemented Systems:**

```csharp
// ✅ Assets/Scripts/Godgame/Interaction/Hand/DivineHandStateSystem.cs
// Lines 119-141: SlingshotAim state handling

case HandState.SlingshotAim:
    // ✅ Validates handler
    // ✅ Accumulates charge: ChargeSeconds += deltaTime
    // ✅ Clamps to MaxChargeSeconds
    // ✅ Checks MinChargeSeconds on release
    // ✅ Sets cooldown on valid release
    // ✅ Transitions back to Holding/Empty
```

**Missing Implementation (❌ NOT YET DONE):**

```csharp
// ❌ Projectile spawning logic (referenced but not implemented)
// Line 289 in concept: SpawnProjectile(...) - DOES NOT EXIST
// <NEEDS IMPLEMENTATION: Projectile entity creation>

// ❌ Aim direction tracking  
// <NEEDS COMPONENT: Where is aim stored? Not in Hand currently>

// ❌ Speed calculation from charge
// <NEEDS IMPLEMENTATION: ChargeSeconds → velocity conversion>

// ❌ Visual feedback
// <NEEDS IMPLEMENTATION: Rubber band VFX, trajectory preview>
```

## Design Feel (What It Should Be Like)

**Player Experience:**
- Hold RMB while holding cargo
- Rubber-band visual stretches from hand to cursor (visual tension)
- Audio creak intensifies as charge builds
- Power meter fills (subtle UI near cursor)
- Release → satisfying **whoosh** + watch object fly in arc
- Impact → thud/splash based on what it hits

**Charge Curve Feel:**
```
0.0s - 0.2s: Too fast (blocked, error sound "hold longer")
0.2s - 0.5s: Gentle toss (close range, precision)
0.5s - 1.0s: Medium throw (tactical distance)
1.0s - 1.5s: Strong throw (cross-map delivery)
1.5s - 2.0s: Max power (comedic distance, villagers screaming?)
2.0s+:       No extra benefit (caps at max)
```

**Skill Expression:**
- **Novice:** Hold too long or too short, unpredictable
- **Intermediate:** Consistent medium throws
- **Expert:** Perfect charge timing for exact distances

---

## Projectile Behavior (Design)

**<NEEDS SPEC: What happens when thrown object flies?>**

### Trajectory
- Parabolic arc (gravity applies)
- Initial velocity from charge curve: 5-50 m/s
- <FOR REVIEW: Air resistance? Wind?> 

### Mid-Flight
- Visual: Motion blur trail, rotation
- Audio: Whoosh (volume = speed)
- Physics: <CLARIFICATION: Collide with obstacles or pass through?>

### Impact
- **On ground:** Create pile (if resource) or bounce (if villager)
- **On storehouse:** Auto-deposit (if resource) or bounce (if villager)
- **On water:** Splash, sink, resources lost? <NEEDS DECISION>
- **On units:** <UNDEFINED: Damage? Comedy? Physics ragdoll?>

---

## Truth Source Notes (For Reference)

**What Exists:** ✅ Charge accumulation fully functional (DivineHandStateSystem lines 119-141)
- Hand.ChargeSeconds accumulates each frame
- Min/max validation works
- State transitions work
- Cooldown enforcement works

**What's Missing:** ❌ Nothing actually throws!
- Release validates and transitions state
- But no projectile spawns
- No velocity applied
- Just returns to Holding state

**Design Note:** State machine skeleton exists, needs "throw" behavior filled in

---

## Design Questions

1. **Throw amount:** All cargo? Fixed 100 units? Player-controlled?
2. **Charge curve:** Linear or exponential? (More power at end?)
3. **Trajectory preview:** Show arc path or let player learn by feel?
4. **Max range:** Cap distance or allow cross-map?
5. **Villager throws:** Comedy (screaming) or serious (tactical transport)?
6. **Impact effects:** Bounce, stick, damage, or context-dependent?
7. **Aim assist:** None (pure skill) or subtle snap to valid targets?

---

**Implementation Path:**

1. ✅ **DONE:** `Hand` component with charge fields exists
2. ✅ **DONE:** `DivineHandStateSystem` accumulates charge (lines 119-141)
3. ✅ **DONE:** Min/max charge validation implemented
4. ✅ **DONE:** Cooldown tracking exists
5. ✅ **DONE:** State machine transitions work
6. ❌ **TODO:** Projectile spawning on release (currently just transitions state, no throw happens!)
7. ❌ **TODO:** Aim direction storage (<NEEDS SPEC: Add to Hand or separate component?>)
8. ❌ **TODO:** Speed calculation from ChargeSeconds
9. ❌ **TODO:** Visual feedback (rubber band, trajectory arc)
10. ❌ **TODO:** Audio feedback (charge creak, whoosh)

**Current Gap:** State machine works, but **nothing actually throws**. Release validates charge and transitions state, but no projectile spawns.

**Truth Source Contract:**

See `Docs/TruthSources_Inventory.md#13-divine-hand` for full list of proposed components.

---

**For Implementers:** Start with simple fixed-speed throw (no charge) to validate projectile physics, then add charge curve  
**For Designers:** Key tuning is MinSpeed/MaxSpeed ratio - affects skill ceiling

