# Prayer Power Economy

**Status:** Draft - <WIP: Miracle system undefined>  
**Category:** Core  
**Scope:** Global  
**Created:** 2025-10-31  
**Last Updated:** 2025-10-31

**⚠️ CLARIFICATION NEEDED:**
- Is prayer power even the right resource model?
- Should miracles cost resources or just have cooldowns?
- Is this inspired by Black & White or going a different direction?
- Temple mechanics - needed or out of scope?

---

## Purpose

Prayer Power is the **divine currency** that fuels miracles and god powers. It represents the strength of worship from your followers and is the core resource that links population management to divine intervention gameplay.

**Primary Goal:** Create meaningful economy between city management and miracle usage  
**Secondary Goals:**
- Reward growing a thriving population
- Create strategic tension (save for big miracle vs spend on many small)
- Make good/evil choices impact resources

---

## System Overview

### Components

1. **Worshippers** <CONFIRMED: Villagers exist>
   - Role: Generate prayer power over time
   - Type: Actor (villagers)
   - Truth Source: `VillagerId`, `VillagerMood` ✅
   
2. **Prayer Power Pool** <WIP: Not yet implemented>
   - Role: Store accumulated worship
   - Type: Resource (global currency)
   - Truth Source: <NEEDS COMPONENT: `PrayerPowerPool`>

3. **Miracles** <PARTIAL: RainMiracleSystem stub exists>
   - Role: Consume prayer power
   - Type: Sink (abilities)
   - Truth Source: <NEEDS COMPONENT: `MiracleRuntimeState`>

4. **Multipliers** <UNDEFINED>
   - Role: <CLARIFICATION NEEDED: Alignment system?>
   - Type: <CLARIFICATION NEEDED: Temple system?>
   - Truth Source: <NOT YET DESIGNED>

### Connections

```
Villagers → [Worship] → Prayer Power Pool
Temple → [Amplifies] → Prayer Rate
Impressiveness → [Multiplies] → Prayer Rate
Prayer Power Pool → [Consumed by] → Miracles
Miracles → [Create] → Impressive Moments
Impressive Moments → [Increase] → Worship Rate
```

### Feedback Loops

- **Positive Loop:** More prayer power → more miracles → happier villagers → more prayer power
- **Negative Loop:** Spending all prayer → fewer miracles → less impressive city → lower prayer generation
- **Balance Point:** Maintain reserve for emergencies while using miracles to grow

---

## System Dynamics

### Inputs
- **Passive Income:** Each villager generates X prayer/second
- **Temple Bonus:** Temples multiply prayer generation in radius
- **Impressiveness Bonus:** City attractiveness adds global multiplier
- **Event Bonuses:** Festivals, victories, perfect moments

### Internal Processes
1. Sum base prayer from all worshippers
2. Apply building multipliers (temples, shrines)
3. Apply global modifiers (alignment, impressiveness)
4. Add to pool (capped at maximum if set)

### Outputs
- Prayer power available for miracles
- Visual feedback (prayer orbs rising to sky?)
- Audio cues for generation milestones

---

## Key Metrics

| Metric | Target Range | Critical Threshold |
|--------|--------------|-------------------|
| Prayer per Villager | 1-5 / second | < 0.5 (too slow) |
| Pool Maximum | 10,000-100,000 | Infinite (exploitable?) |
| Temple Multiplier | 1.5x - 3x | > 5x (too strong) |
| Impressiveness Bonus | 0% - 200% | > 500% (broken) |

**Target Values (Draft):**
- **Base Rate:** 1 prayer/second/villager
- **Small Temple:** 1.5x in 20m radius
- **Large Temple:** 2.5x in 40m radius
- **Impressiveness:** 0-100 scale → 0-100% bonus
- **Pool Cap:** 50,000 (or uncapped?)

---

## Generation Formula

**<WIP: Placeholder only - needs approval>**

**Simple Option (No temples, no alignment):**
```
Prayer Per Second = VillagerCount × BaseRate
```

**Complex Option (IF temples/alignment added):**
```
Prayer Per Second = 
  Σ(Villagers × Multipliers) 
  × <UNDEFINED: Bonus systems>
```

**⚠️ DESIGN DECISION:** Start simple or build complex from day 1?

**Current Implementation:** None - prayer system not implemented

---

## Alignment Impact

**<WIP: Alignment system not yet defined>**

### **NEEDS DESIGN DECISION:**
- Is there a good/evil alignment system?
- Per-player or per-village?
- Affects prayer generation or just visuals?
- Black & White style or different approach?

### Proposed (IF Alignment Implemented)

#### Good Path
- <FOR REVIEW> Bonus generation from happy villagers
- <UNDEFINED: Miracle cost modifiers?>

#### Evil Path  
- <FOR REVIEW> Generation from fear/sacrifice
- <UNDEFINED: How is "fear" measured?>

**⚠️ SKIP THIS SECTION** until alignment system designed

---

## Spending Categories

### Miracle Costs

**<WIP: Miracle roster undefined>**

**Confirmed Miracles:**
- Rain: `RainMiracleSystem` stub exists ✅

**Proposed Miracles (Pending Approval):**
| Miracle | Status | Cost/Cooldown |
|---------|--------|---------------|
| Water/Rain | ✅ Stub exists | <NEEDS SPEC> |
| Fire | <CONCEPT ONLY> | <NEEDS SPEC> |
| Heal | <CONCEPT ONLY> | <NEEDS SPEC> |
| Lightning | <CONCEPT ONLY> | <NEEDS SPEC> |
| Earthquake | <CONCEPT ONLY> | <NEEDS SPEC> |

**⚠️ DESIGN DECISION NEEDED:**
- Should miracles cost prayer OR just have cooldowns?
- Free miracles + cooldowns simpler (no economy)
- Prayer cost adds strategy (resource management)

**Current Implementation:** None - waiting on miracle system design

---

## Player Strategy

### Early Game (0-15 minutes)
- **Income:** 20-50 prayer/sec (small population)
- **Strategy:** 
  - Save for first temple (5,000)
  - Use small miracles sparingly
  - Focus on population growth
- **Milestone:** First temple built → 2x generation

### Mid Game (15-45 minutes)
- **Income:** 200-500 prayer/sec (medium city)
- **Strategy:**
  - Regular miracle usage
  - Second temple for coverage
  - Build impressiveness
- **Milestone:** Can afford epic miracles occasionally

### Late Game (45+ minutes)
- **Income:** 1000+ prayer/sec (large empire)
- **Strategy:**
  - Epic miracles available
  - Maintain strategic reserve
  - Spam miracles if needed
- **Milestone:** Never run out, limited by cooldowns

---

## Balancing Mechanisms

### Self-Balancing
- **Generation increases with population:** Rewards growth
- **Costs scale with power:** Prevent early spam
- **Cooldowns prevent spam:** Even with infinite prayer

### Player Balancing
- **Save vs spend:** Strategic choice
- **Where to build temples:** Placement matters
- **Good vs evil:** Different generation strategies

### System Balancing
- **Optional cap:** Prevent infinite hoarding
- **Generation scaling:** Diminishing returns at high population?
- **Emergency reserve:** UI warning when low?

---

## Failure Modes

### Death Spiral (Bankruptcy)
**Cause:** Spent all prayer, can't cast miracles, population dies  
**Effect:** No income, can't recover  
**Recovery:** 
- Never let prayer hit zero (UI warning at 10%)
- Always keep base generation from starting population
- Tutorial teaches "don't spend last 1000"

### Runaway Wealth
**Cause:** Late game city generates too much, miracles feel free  
**Effect:** No strategic choices, spam everything  
**Recovery:**
- Introduce higher-tier miracles (100k+ cost)
- Cap prayer pool at 50k
- Make miracle cooldowns the real limiter

---

## Visual Representation

### Prayer Flow Visualization
```
[Villager] → [Orb rises] → [Floats to temple/sky] → [Pool HUD updates]
                     ↓
               [Villager aura]
               (Good: golden, Evil: red)
```

### HUD Display
```
┌─────────────────────────────────────┐
│ ⚡ Prayer Power: 12,450 / 50,000   │
│ ▓▓▓▓▓▓▓▓░░░░░░░░░░░░  24%          │
│ +225 per second                     │
└─────────────────────────────────────┘
```

---

## Iteration Plan

### v1.0 (MVP)
- Basic generation: villagers → prayer
- Simple costs for miracles
- No temples, no multipliers
- **Goal:** Prove core loop works

### v2.0 (Temples & Multipliers)
- Add temples with radius multipliers
- Impressiveness bonus
- Building costs
- **Goal:** Strategic placement matters

### v3.0 (Alignment & Polish)
- Good/evil modifiers
- Visual feedback (orbs rising)
- Audio cues
- Advanced balancing
- **Goal:** Full economy depth

---

## Open Questions

1. **Should prayer have a cap?** Pro: prevents hoarding. Con: feels bad to waste generation.
2. **Should temples consume prayer to operate?** Pro: ongoing cost. Con: complexity.
3. **Should villager happiness affect prayer quality?** Pro: ties to villager system. Con: already have impressiveness.
4. **How does creature affect prayer?** Good creature boosts? Evil creature scares into prayer?

---

## References

- **Black & White 2:** Prayer power tied to belief/worship
- **Populous:** Mana from followers
- **Age of Mythology:** Divine power resource
- **Civilization (Religion):** Faith as currency

---

## Related Documentation

- Truth Sources: `Docs/TruthSources_Inventory.md#miracles`
- Miracles: `Docs/Concepts/Miracles/` (to be created)
- Villagers: `Docs/Concepts/Villagers/` (to be created)
- Buildings: `Docs/Concepts/Buildings/` (to be created)

---

## Implementation Notes

**Components Needed:**
- `PrayerPowerPool` (singleton) - Current amount, max, generation rate
- `PrayerGenerator` - Per-villager or per-building component
- `PrayerMultiplier` - Temple/building effect zones
- `MiracleCost` - Per-miracle cost and cooldown

**Systems Needed:**
- `PrayerGenerationSystem` - Sum all generators, apply multipliers
- `PrayerConsumptionSystem` - Deduct costs when miracles cast
- `PrayerUISystem` - Update HUD display
- `PrayerVisualsSystem` - Spawn orb VFX

---

**For Implementers:** Start with v1.0 (no temples), add complexity in v2.0+  
**For Designers:** Focus on miracle costs vs generation rate balance  
**For Playtesters:** Key question: "Do you feel prayer-starved, balanced, or swimming in it?"

