# Aggregate Piles System

**Status:** Approved  
**Category:** System - Resource Economy  
**Scope:** Global  
**Created:** 2025-10-31  
**Last Updated:** 2025-10-31

---

## Purpose

**Primary Goal:** Visual, physical representation of resources outside storehouses  
**Secondary Goals:**
- Make resource flow tangible (see wood pile grow as villagers gather)
- Provide hand interaction targets (siphon from piles, drip to ground)
- Prevent resource loss during transitions (carrying → storehouse full → pile)

---

## System Overview

### Components

1. **AggregatePile:** Dynamic resource heap - Entity with amount, type, visuals
2. **Hand:** Player god hand - Can siphon (take) or drip (give) resources
3. **Villagers:** Workers delivering resources - Create piles when storehouse full
4. **Storehouses:** Final destination - Accept pile overflow manually
5. **PileMergeConfig:** Merge rules - Radius and capacity limits

### Connections

```
Villager harvests → Can't deliver (full) → Spawns pile on ground
Pile + Nearby same-type pile → Merge if within radius
Pile > MaxCapacity → Split into multiple piles
Hand + Pile → Siphon (take from pile)
Hand + Ground → Drip (create/add to pile)
Pile + Player action → Deposit to storehouse
```

### Feedback Loops

- **Positive:** More piles → easier to find resources → faster hand siphon
- **Negative:** Too many piles → visual clutter → player forced to consolidate
- **Balance:** Merge radius keeps pile count reasonable

---

## System Dynamics

### Inputs
- Villager overflow (can't deliver to storehouse)
- Hand drip (player drops resources)
- Storehouse rejection (capacity full)

### Internal Processes
1. Pile spawn (if none nearby) or add to existing
2. Every 5 seconds: Check merge radius
3. If same-type pile within radius → merge
4. If pile > max capacity → split overflow to new pile

### Outputs
- Visual piles on terrain
- Siphon/drip interaction targets
- Resource conservation (no loss)

---

## State Machine

### States
1. **Growing:** Amount increasing - Entry: Resource added - Exit: Merge or split triggered
2. **Stable:** No changes - Entry: No activity for 1 second - Exit: Resource change
3. **Merging:** Combining with nearby - Entry: Nearby pile detected - Exit: Merge complete
4. **Splitting:** Overflow handling - Entry: Amount > max - Exit: New pile spawned

### Transitions
```
Growing → Merging [nearby pile detected]
Merging → Stable [merge complete]
Stable → Splitting [amount > max capacity]
Splitting → Stable [overflow removed]
```

---

## Key Metrics

| Metric | Target Range | Critical Threshold |
|--------|--------------|-------------------|
| Piles per type | 2-5 | > 20 (too scattered) |
| Merge frequency | 1-2 per minute | > 10/min (thrashing) |
| Avg pile size | 500-1500 | < 100 (inefficient) |

---

## Balancing

- **Self:** Merge reduces pile count naturally; split prevents mega-piles
- **Player:** Can manually consolidate by siphon → drip
- **System:** Merge radius and max capacity tune pile distribution

---

## Scale & Scope

### Small Scale (Single Pile)
- Individual resource amounts (0-2500 units)
- Visual size tiers (tiny → small → medium → large → huge)

### Medium Scale (Local Area)
- Multiple piles near storehouse
- Merge creates local resource "pools"

### Large Scale (Whole Map)
- Resource distribution pattern
- Player can see economy health from pile locations

---

## Time Dynamics

### Short Term (Seconds)
- Hand siphon/drip (instant transfers)
- Villager deposits (every few seconds)

### Medium Term (Minutes)
- Piles merge and stabilize
- Visual accumulation near storehouses

### Long Term (Hours)
- Resource flow patterns emerge
- Pile "highways" form along villager paths

---

## Failure Modes

- **Death Spiral:** Storehouse always full → infinite piles spawn → lag - Recovery: Auto-delete oldest piles beyond cap (100)
- **Stagnation:** No piles ever merge → scattered chaos - Recovery: Increase merge radius in options
- **Runaway:** Hand siphons create massive piles → lag - Recovery: Enforce max pile size (5000 units)

---

## Player Interaction

- **Observable:** Pile visual size, resource type color coding
- **Control Points:** Hand siphon (take), hand drip (give), manual storehouse deposit
- **Learning Curve:** Beginner (discovery: "I can interact with piles") → Intermediate (optimization: strategic pile placement) → Expert (mastery: pile manipulation for villager routing)

---

## Systemic Interactions

### Dependencies
- Storehouse capacity system (creates piles when full)
- Hand state machine (siphon/drip mechanics)
- Resource type catalog (wood, ore, food colors)

### Influences
- Villager pathfinding (avoid stepping on piles)
- Visual clutter (too many piles = UI issue)
- Economy visibility (piles show resource flow)

### Synergies
- Hand + Pile = fast resource redistribution
- Pile + Storehouse = buffer for overflow

---

## Exploits

- Create infinite piles by siphon/drip loop → Severity: Low - Fix: Enforce merge on same location
- Lag game with 1000+ piles → Severity: High - Fix: Hard cap at 200 piles, auto-merge oldest

---

## Tests

- [ ] Siphon caps at hand capacity
- [ ] Drip creates pile at correct visual size
- [ ] Merge combines same-type piles within radius
- [ ] Split spawns new pile when > max capacity
- [ ] Cross-type piles don't merge
- [ ] 30 FPS vs 120 FPS: same transfer rates (frame-rate independent)
- [ ] 1000 units siphon → drip → siphon = 1000 (conservation)

---

## Performance

- **Complexity:** O(n) per pile update, O(n²) merge checks (optimized with spatial grid)
- **Max entities:** 200 piles hard cap
- **Update freq:** 5 Hz (merge checks), instant (siphon/drip)

---

## Visual Representation

### Size Curve
```
Amount → Visual Scale
0-100   → 1x  (tiny mound)
100-500 → 3x  (small pile)
500-1000 → 10x (medium heap)
1000-2500 → 30x (large mound)
2500+    → 50x (huge pile)
```

### Data Flow
```
[Villager carrying] → [Storehouse full?] 
    Yes → [Find nearby pile or spawn] → [Add amount]
    No  → [Deposit to storehouse]

[Hand siphon] → [Transfer per frame (clamp)] → [Hand amount++, Pile amount--]

[Pile merge check] → [Find neighbors within radius] → [Same type?]
    Yes → [Sum amounts, destroy one pile]
    No  → [Keep separate]
```

---

## Iteration Plan

- **v1.0 (MVP):** Spawn, grow, visual size - No merge, no split
- **v2.0:** Merge within radius - Prevents clutter
- **v3.0:** Hand siphon/drip - Player interaction
- **v4.0:** Visual polish (particles, shadows, type-specific models)

---

## Open Questions

1. Should piles decay over time if left outdoors?
2. Should weather (rain) affect pile visuals (wet wood)?
3. Should creatures/villagers be able to knock over piles?

---

## References

- Legacy: `C:\Users\Moni\Documents\claudeprojects\godgame\truthsources\Aggregate_Resources.md`
- Black & White 2: Resource piles near storage buildings
- Factorio: Ground item stacking behavior

---

## Related Documentation

- Truth Sources: `Docs/TruthSources_Inventory.md#resource-nodes`
- Hand Siphon: `Docs/Concepts/Interaction/Hand_Siphon.md` (to be created)
- Storehouse: `Docs/Concepts/Buildings/Storehouse_System.md`

---

**For Implementers:** Focus on Phase 1 (spawn/grow) first, iteration plan guides feature additions  
**For Designers:** Key balance knobs are merge radius (2.5m) and max capacity (2500)  
**For Playtesters:** Watch for: pile clutter, resource loss, merge frequency feeling right

