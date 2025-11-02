# Needs-Driven Construction System

**Status:** Draft - <WIP: Village mechanics not yet defined>  
**Category:** System - Buildings  
**Scope:** Village-wide  
**Created:** 2025-10-31  
**Last Updated:** 2025-10-31

**⚠️ CLARIFICATION NEEDED:**
- Village concept not yet defined (is there even a "village" entity?)
- Alignment system unclear (good/evil mechanics undecided)
- Advancement progression unknown (tech tree vs tribute vs free-form?)
- Construction initiation: player-placed or auto-generated from needs?

---

## Purpose

Buildings should emerge **organically from villager needs** rather than being purely player-placed. The god (player) shapes the village through influence and miracles, but villagers decide *what* they need based on their wellness, advancement level, and cultural alignment.

**Primary Goal:** Villages feel alive and self-organizing  
**Secondary Goals:**
- Reward good city management (happy villagers → better buildings)
- Make alignment meaningful (good vs evil affects architecture)
- Create emergent city layouts (not just player grid placement)

---

## System Overview

### Components

1. **Villager Needs** (Input Signals)
   - Role: Generate construction demands
   - Type: Sensors (Health, Energy, Morale, Shelter)

2. **Construction Demand** (Aggregated Needs)
   - Role: What the village wants to build
   - Type: Request queue

3. **Village Advancement** (Unlock System)
   - Role: Determines what CAN be built
   - Type: Tech tree / progression

4. **Village Alignment** (Cultural Style)
   - Role: Determines HOW buildings look/behave
   - Type: Modifier (good/evil, order/chaos)

5. **Construction Sites** (Active Building)
   - Role: Buildings under construction
   - Type: Work-in-progress entities

6. **Builder Villagers** (Labor)
   - Role: Execute construction
   - Type: Actors with Builder job

### Connections

```
VillagerNeeds → [Aggregate] → ConstructionDemand
                                    ↓
                              [Filter by]
                                    ↓
                            VillageAdvancement
                                    ↓
                            [Create if unlocked]
                                    ↓
                            ConstructionSite
                                    ↓
                            [Assign builders]
                                    ↓
                            VillagerJob = Builder
                                    ↓
                            [Work over time]
                                    ↓
                            Completed Building
                                    ↓
                            [Styled by]
                                    ↓
                            VillageAlignment
```

### Feedback Loops

- **Positive:** More houses → more villagers → more workers → faster construction
- **Negative:** Too many builders → fewer gatherers → resource starvation → slower construction
- **Balance:** Optimal builder ratio maintains growth without stalling economy

---

## Needs → Demands Mapping

### Housing Demand
```
IF: VillagerNeeds.Energy < 30% for >50% of population
AND: No shelter / overcrowded houses
THEN: Generate ConstructionDemand(House, urgency=0.9)
```

**Signals:**
- Low energy (no rest place)
- High population density
- Homeless villagers

### Storage Demand
```
IF: Resources piling on ground (>500 units in piles)
AND: Storehouse capacity < 80%
THEN: Generate ConstructionDemand(Storehouse, urgency=0.6)
```

**Signals:**
- Ground piles accumulating
- Existing storehouses near full
- Villagers carrying but can't deliver

### Worship Demand
```
IF: VillagerMood.Mood < 40% for >30% of population
AND: No temple within 50m radius
THEN: Generate ConstructionDemand(Temple, urgency=0.7)
```

**Signals:**
- Low morale (need spiritual center)
- No nearby worship building
- Recent miracle witnessed (inspired to build)

### Defense Demand
```
IF: Recent attack detected
OR: VillageAlignment.GoodEvil < -20 (evil path)
THEN: Generate ConstructionDemand(Wall/Tower, urgency=0.8)
```

**Signals:**
- Enemy units nearby
- Combat happened recently
- Evil god wants imposing fortress

### Food Production Demand
```
IF: Food resources < 20% of total
AND: Population > 20
THEN: Generate ConstructionDemand(Farm/FishingHut, urgency=0.75)
```

**Signals:**
- Food scarcity
- Hunting/gathering insufficient
- Growing population

---

## Advancement System

**<WIP: Progression mechanics undefined>**

### **NEEDS CLARIFICATION:**
- How do buildings unlock? (Population gates? Tribute system? Tech research?)
- Is there a tech tree or free-form unlocking?
- What triggers advancements? (Player action? Automatic?)
- Are there distinct "levels" or gradual unlocking?

### Proposed Tech Levels (Draft - Subject to Change)

**Note:** These are placeholders. Actual progression needs design approval.

#### Level 0-1: Early Buildings
- <FOR REVIEW> Basic shelter (tent/hut concept)
- <FOR REVIEW> Simple storage (pile → structure)
- **Materials:** Wood only

#### Level 2-3: Mid Buildings  
- <FOR REVIEW> Improved housing
- <FOR REVIEW> Worship structures (temple concept)
- **Materials:** <UNDEFINED: Stone system? Ore processing?>

#### Level 4-5: Late Buildings
- <FOR REVIEW> Advanced structures
- <FOR REVIEW> Wonder/monument concepts
- **Materials:** <UNDEFINED: Special resources?>

**⚠️ DO NOT IMPLEMENT** until progression system designed

---

## Alignment Influence

**<WIP: Alignment system not yet defined>**

### **NEEDS CLARIFICATION:**
- Does alignment even exist in this game? (Good/evil god choice?)
- Is alignment per-village or global player choice?
- Does alignment affect mechanics or just visuals?
- How is alignment measured/tracked?

### Proposed Visual Styles (If Alignment Implemented)

**Note:** Placeholder concepts pending alignment system design.

#### Good Path (IF alignment > threshold)
- <FOR REVIEW> Visual style: Bright, welcoming
- <FOR REVIEW> Buildings: Gardens, healing themes
- <UNDEFINED: Specific building list>

#### Evil Path (IF alignment < threshold)  
- <FOR REVIEW> Visual style: Dark, imposing
- <FOR REVIEW> Buildings: Spikes, sacrifice themes
- <UNDEFINED: Specific building list>

#### Neutral/No Alignment
- <DEFAULT> Mixed natural materials
- <DEFAULT> Pragmatic placement

**⚠️ DO NOT IMPLEMENT** until alignment system approved

---

## Construction Process

### Phase 1: Demand Generation
**System:** `ConstructionDemandSystem` <WIP>

**<CLARIFICATION NEEDED: Who decides what to build?>**
- Option A: Villagers auto-request based on needs
- Option B: Player manually places buildings
- Option C: Hybrid (basics auto, special manual)

```
IF auto-generation chosen:
  Every 5 seconds:
    1. Scan villager needs
    2. Aggregate shortfalls
    3. Generate ConstructionDemand entities
    4. Prioritize by urgency
```

### Phase 2: Site Placement
**System:** `ConstructionSiteSpawnSystem`
```
For each ConstructionDemand:
  1. Check VillageAdvancement.UnlockedBuildings
  2. If building type unlocked:
     a. Find valid placement location
        - Near existing buildings (unless first)
        - Flat terrain
        - Not blocking resources/paths
        - Alignment-based clustering rules
     b. Check resource availability
     c. Spawn ConstructionSite entity
     d. Consume ConstructionDemand
```

### Phase 3: Resource Delivery
**System:** `ConstructionResourceSystem`
```
For each ConstructionSite:
  1. Define required resources (e.g., 100 wood, 50 ore)
  2. Villager jobs: Deliver resources to site
  3. Track DeliveredAmount vs RequiredAmount
  4. Once resources delivered → construction can begin
```

### Phase 4: Building
**System:** `ConstructionProgressSystem`
```
For each ConstructionSite with resources:
  1. Count assigned builders (VillagerJob.Type = Builder)
  2. Progress += (builders × buildRate × deltaTime)
  3. Update visual (scaffolding, % complete)
  4. When progress >= 100%:
     → Spawn actual building prefab
     → Remove ConstructionSite
     → Builders return to idle
```

---

## Player Interaction

### Direct Control (Traditional RTS)
**Option A:** Player can manually place buildings
- God hand picks building from menu
- Places construction site
- Villagers auto-assign to build

**Pros:** Familiar, precise control  
**Cons:** Less "god game," more RTS micromanagement

### Indirect Influence (God Game)
**Option B:** Villagers decide what to build, player influences
- Villagers generate demands automatically
- Player can **veto** unwanted constructions
- Player can **boost** construction speed (prayer power)
- Player can **nudge** placement with influence ring

**Pros:** Emergent, villagers feel autonomous  
**Cons:** Less control, might frustrate planners

### Hybrid (Recommended)
**Option C:** Mix of both
- **Villager-initiated:** Basic needs (houses, storehouses) auto-requested
- **Player-placed:** Special buildings (temples, wonders, defenses)
- **Player can override:** Disable auto-construction if desired

**Pros:** Best of both worlds  
**Cons:** Needs clear UI to show what's automatic vs manual

---

## Building Types

**<WIP: Building roster not finalized>**

### **NEEDS CLARIFICATION:**
- Which buildings actually exist in scope?
- Are there housing tiers or single house type?
- Food system (farms, fishing) - implemented or cut?
- Production buildings (mills, smithies) - needed?

### Confirmed Buildings (From Truth Sources)

| Building | Status | Notes |
|----------|--------|-------|
| Storehouse | ✅ Exists | `StorehouseInventory` component implemented |
| Resource Nodes | ✅ Exists | Wood/ore nodes in `GodgameResourceNode` |

### Proposed Buildings (Pending Design Approval)

| Building | Purpose | Priority | Clarity Status |
|----------|---------|----------|----------------|
| Housing | Villager shelter | High | <NEEDS SPEC> Capacity? Tiers? |
| Temple | Prayer generation | High | <NEEDS SPEC> Bonus mechanics? |
| Walls | Defense | Medium | <UNDEFINED> Combat system first |
| Farms | Food production | Low | <UNDEFINED> Food system scope? |
| Workshops | Production | Low | <UNDEFINED> Needed for MVP? |

**⚠️ DO NOT IMPLEMENT** specific buildings until approved

---

## Construction Costs & Time

### Base Build Times
- **Small:** 30 seconds (mud hut, stockpile)
- **Medium:** 60 seconds (house, storehouse)
- **Large:** 120 seconds (temple, walls)
- **Epic:** 300 seconds (wonder, grand temple)

### Builder Efficiency
```
Base Build Rate = 1.0% per second per builder
Actual Progress = BuildRate × (1 + ToolQuality × 0.1) × BuilderCount

Example:
- 3 builders, standard tools (quality 1.0)
- House (60s base)
- Progress = 1.0 × (1 + 1.0 × 0.1) × 3 = 3.3% per second
- Time = 100 / 3.3 = ~30 seconds (half of base time)
```

---

## Edge Cases & Exploits

### Infinite Construction Loop
**Problem:** Player places 100 houses at once, all builders stuck forever  
**Solution:** Cap active construction sites to (population / 10) max

### Resource Starvation
**Problem:** All wood used for building, no fuel/tools  
**Solution:** Reserve % of resources (e.g., 20% never allocated to construction)

### Homeless Despite Houses
**Problem:** Houses built far away, villagers don't move in  
**Solution:** Auto-assign villagers to nearest available housing

### Alignment Flip Ruins Aesthetics
**Problem:** Player goes good→evil, existing good buildings look out of place  
**Solution:** Gradual visual corruption (vines→spikes), or force rebuild

---

## Testing Scenarios

### Functional Tests
- [ ] Housing demand triggers when population > capacity
- [ ] Storage demand triggers when ground piles accumulate
- [ ] Temple demand triggers when morale low
- [ ] Construction completes when resources delivered + builders assigned
- [ ] Buildings spawn at correct tech level

### Balance Tests
- [ ] Village can grow from 10→100 villagers smoothly
- [ ] No resource deadlocks (stuck waiting for wood)
- [ ] Builder allocation doesn't starve other jobs
- [ ] Construction speed feels satisfying (not too fast/slow)

### Emergence Tests
- [ ] Different playthroughs create different city layouts
- [ ] Good vs evil villages look visually distinct
- [ ] Player can recognize village health from skyline

---

## Open Questions

1. **Should players be able to demolish buildings?**
   - Pro: Fix mistakes, redesign city
   - Con: Villagers built it, god destroying homes feels wrong
   
2. **Should construction pause when resources depleted?**
   - Pro: Realistic
   - Con: Frustrating micro

3. **Should advanced buildings require Tribute unlocks or just tech level?**
   - Pro (Tribute): Player agency, gating
   - Con: Less emergent, more scripted

4. **Should alignment affect construction costs?**
   - Evil: Faster but uglier?
   - Good: Slower but more durable?

---

## Implementation Notes

**Truth Sources Required:**
- `Docs/TruthSources_Inventory.md#construction` (to be created)

**New Components Needed:**
```csharp
ConstructionDemand       // Villager needs → build requests
ConstructionSite         // Active building WIP
VillageAdvancement       // Tech tree unlocks
VillageAlignment         // Good/evil/order/chaos
BuildingTemplate         // Prefab + costs + requirements
```

**New Systems Needed:**
```csharp
ConstructionDemandSystem       // Aggregate needs
ConstructionSiteSpawnSystem    // Place sites
ConstructionResourceSystem     // Deliver materials
ConstructionProgressSystem     // Build over time
BuildingStyleSystem            // Apply alignment visuals
```

---

## Related Concepts

- Resources: `Docs/Concepts/Resources/` (resource gathering/delivery)
- Villagers: `Docs/Concepts/Villagers/Villager_Needs.md` (to be created)
- Progression: `Docs/Concepts/Progression/Tribute_System.md` (to be created)
- Alignment: `Docs/Concepts/Progression/Alignment_System.md` (to be created)

---

**For Implementers:** Start with Phase 1 (demand generation) before building placement  
**For Designers:** Focus on needs→demands mapping, tweak urgency thresholds  
**For Playtesters:** Key question: "Does the village feel alive or does it feel like you're micromanaging?"

---

## Version History

### v0.1 - 2025-10-31
- Initial draft based on truth source architecture discussion
- Defined needs→demands→construction flow
- Outlined tech levels and alignment influence

