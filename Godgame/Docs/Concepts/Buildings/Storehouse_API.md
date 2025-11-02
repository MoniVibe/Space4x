# Storehouse API Contract

**Status:** Approved - <WIP: API wrapper not implemented>  
**Category:** Mechanic - Resource Storage  
**Complexity:** Simple  
**Created:** 2025-10-31  
**Legacy Source:** `Docs/Concepts/legacy/Storehouse_API.md`

**⚠️ CURRENT STATE:**
- ✅ Storehouse components fully implemented (PureDOTS)
- ✅ Inventory buffers working (`StorehouseInventoryItem[]`)
- ✅ Capacity tracking working (`StorehouseCapacityElement[]`)
- ✅ Reservations working (`StorehouseReservationItem[]`)
- ❌ No clean API wrapper (direct buffer manipulation)
- ❌ No intake trigger component (for tap-dump RMB)
- ❌ No events (OnTotalsChanged, OnCapacityChanged)

---

## Overview

**Purpose:** Single write path for resource totals, no direct pile manipulation  
**Player Impact:** Storehouses are the "bank" for resources  
**System Role:** Central inventory authority for village economy

---

## How It Works

### Inputs
- Villager delivery: Carrying resources → deposit
- Hand dump: Player RMB over storehouse
- <UNDEFINED: Pile consolidation? Bulk transfers?>

### Process
1. **Add(resourceTypeIndex, amount):**
   - Check capacity for resource type
   - If space available: Add to inventory buffer item
   - If full: Return rejected amount (caller creates pile)
   - Update `StorehouseInventory.TotalStored`
   
2. **Remove(resourceTypeIndex, amount):**
   - Check inventory buffer for resource
   - If available: Deduct from buffer item
   - If insufficient: Return actual removed amount
   - Update `StorehouseInventory.TotalStored`
   
3. **Space(resourceTypeIndex):**
   - Query capacity buffer for resource type
   - Return: capacity - stored - reserved

### Outputs
- Updated inventory totals
- Accepted/rejected amounts
- <NEEDS: Events for UI updates?>

---

## Rules

1. **Type Validation:** Only accept defined resource types
   - Condition: ResourceTypeIndex not in capacity buffer
   - Effect: Reject entire amount, log warning

2. **Capacity Enforcement:** Never exceed capacity
   - Condition: stored + amount > capacity
   - Effect: Accept partial (up to free space), return remainder

3. **No Negative Amounts:** Prevent underflow
   - Condition: Remove amount > stored
   - Effect: Remove only what's stored, return actual amount

### Edge Cases
- Add while full → Return remainder (caller spawns pile)
- Remove more than exists → Return actual removed amount (0 if empty)
- Concurrent adds (villager + player) → Buffer operations are frame-ordered

### Priority Order
1. Capacity check
2. Type validation
3. Amount clamping
4. Buffer modification

---

## Parameters

| Parameter | Default | Range | Impact |
|-----------|---------|-------|--------|
| Capacity per type | Varies | 500-5000 | Storage limits |
| Total capacity | Sum of types | 1000-10000 | Overall storage |

**Current Implementation:** 
- ✅ Configured in `StorehouseAuthoring.cs`
- Default: 1000 total, 500 wood, 500 ore

---

## Example

**Given:** Storehouse with 300/500 wood capacity, villager carrying 150 wood  
**When:** Villager calls `Add(wood, 150)`  
**Then:** 
1. Check capacity: 500 - 300 = 200 available ✓
2. Accept all 150 ✓
3. Update buffer: wood item amount = 300 + 150 = 450
4. Update total: TotalStored = previous + 150
5. Return: 150 (all accepted)

**Given:** Same storehouse, then add 100 more wood  
**When:** Call `Add(wood, 100)`  
**Then:**
1. Check capacity: 500 - 450 = 50 available
2. Accept partial: only 50
3. Update buffer: wood amount = 450 + 50 = 500 (FULL)
4. Update total: TotalStored += 50
5. Return: 50 (rejected 50, caller must handle)

---

## Player Feedback

- **Visual:** <NEEDS SPEC: Storehouse model shows fullness? Resource type colors?>
- **Audio:** <NEEDS SPEC: Sound on deposit? Reject sound?>
- **UI:** <NEEDS SPEC: Tooltip shows capacity? Fill bars?>

**Current:** ❌ No visual feedback implemented

---

## Balance

- **Early:** Small storehouses fill quickly → build more
- **Mid:** Larger capacities, multiple storehouses
- **Late:** <UNDEFINED: Logistics system? Automated transfer?>

### Exploits
- Infinite storage bypass → Capacity enforced by API contract
- Duplicate adds → Frame ordering prevents (single-threaded ECS write)

---

## Interaction Matrix

| Other Mechanic | Relationship | Notes |
|----------------|--------------|-------|
| VillagerAISystem | Consumer | Villagers call Add on delivery ✅ |
| Hand Dump | Consumer | Player RMB calls Add <WIP: Handler exists?> |
| Aggregate Piles | Fallback | Rejected amounts → spawn pile |
| Reservations | Integration | Reserves reduce available space ✅ |

---

## Technical

- **Max entities:** 1-10 storehouses per village
- **Update freq:** On-demand (not per-frame)
- **Data needs:** `StorehouseInventoryItem[]` buffer (already exists ✅)

---

## Tests

- [ ] Add within capacity → full acceptance
- [ ] Add exceeding capacity → partial acceptance, correct remainder
- [ ] Remove more than stored → return actual amount
- [ ] Space() returns correct available capacity
- [ ] Type validation rejects unknown types
- [ ] Concurrent adds preserve totals (conservation test)

---

## Open Questions

1. **Event system:** Emit events for UI or use reactive queries?
2. **Intake trigger:** Should storehouse have physical trigger zone for RMB?
3. **API location:** Static class, system helper, or extension methods?

---

## Version History

- **v0.1 - 2025-10-31:** Ported from legacy Storehouse_API.md

---

## Related Mechanics

- Aggregate Piles: `Docs/Concepts/Resources/Aggregate_Piles.md`
- Hand Dump: `Docs/Concepts/Interaction/` (RMB handlers)
- Villager Delivery: Truth sources ✅ implemented

---

## Design Intent (What It Should Feel Like)

**Player Perspective:**
- Storehouses are the "village bank" - authoritative, reliable
- Depositing feels satisfying (resources safe)
- Full storehouse is visible problem (need expansion)
- Overflow creates ground piles (visual feedback)

**Design Goals:**
- Single source of truth for village resources
- No "lost" resources (conservation)
- Clear capacity limits (strategic constraint)
- Simple API contract (3 methods: Add, Remove, Space)

---

## Truth Source Status (Conceptual)

**What Exists:** ✅ Storehouse inventory system fully functional in DOTS
**What's Needed:** Clean API contract layer

**Design Notes:**
```
When adding resources:
  - Check capacity first
  - Accept partial if space limited
  - Return rejected amount (caller handles overflow)
  
When removing resources:
  - Check availability
  - Return actual removed amount
  - Never go negative
  
When querying space:
  - Consider: capacity - stored - reserved
  - Per resource type, not just total
```

**Verification:** System works in current code (villagers deposit successfully)

---

## API Contract (Design)

### Add(resourceType, amount) → acceptedAmount
**Intent:** Deposit resources into storehouse  
**Returns:** How much was actually accepted  
**Behavior:** 
- Full acceptance if space available
- Partial acceptance if limited space
- Zero if wrong type or completely full

### Remove(resourceType, amount) → removedAmount
**Intent:** Take resources from storehouse  
**Returns:** How much was actually removed  
**Behavior:**
- Full removal if available
- Partial if insufficient stock
- Zero if none available

### Space(resourceType) → availableSpace
**Intent:** Query how much can still be added  
**Returns:** Free capacity for that resource type  
**Behavior:** Capacity - Stored - Reserved

---

## Design Questions

1. **Should storehouses auto-organize?** Sort by type? Prioritize scarce resources?
2. **Visual capacity indication?** Fullness bars? Resource stacks visible through windows?
3. **Multiple storehouses?** Do they share capacity pool or independent?
4. **Intake zones?** Should player need to aim at specific spot or anywhere on building?

---

**For Implementers:** Straightforward refactor - extract existing logic into API class  
**For Designers:** Core mechanic solid (capacity enforcement works), just needs cleanup  
**For Reviewers:** Check `VillagerAISystem.cs` lines 437-507 for current implementation

