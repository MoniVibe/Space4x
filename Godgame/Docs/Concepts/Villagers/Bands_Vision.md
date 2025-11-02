# Bands (Villager Military Groups)

**Status:** Draft - <WIP: Combat system undefined>  
**Category:** System - Combat & Military  
**Scope:** Squad-level (5-20 villagers)  
**Created:** 2025-10-31  
**Last Updated:** 2025-10-31

**⚠️ CLARIFICATION NEEDED:**
- Is there a combat system in scope for MVP?
- Are bands military-only or general grouping (work crews)?
- Player-controlled (RTS) or autonomous (god game)?
- Formation types - simple or complex?

---

## Purpose

**Primary Goal:** Group villagers into coordinated units for combat or large-scale tasks  
**Secondary Goals:**
- Enable tactical gameplay (formations, focus fire)
- Make armies feel cohesive vs individual mobs
- Provide morale bonuses (strength in numbers)

**Inspiration Source:** 
- Black & White 2: "Recruit from villagers at Armory, form platoons (swordsmen/archers)"
- Legacy spec: "Squad-level BT, influence-map tactics, flow fields for massed units"

---

## System Overview

### Components

1. **Band Entity:** Group container - Holds metadata, formation, morale
2. **Band Members:** Individual villagers - Reference to parent band
3. **Formation:** Spatial arrangement - Line, wedge, shield wall, skirmish
4. **Band Leader:** <UNDEFINED: Do bands have leaders?> - Command unit
5. **Band Morale:** Group psychology - Affects combat effectiveness

### Connections

```
Villager (idle) → [Recruit action] → Band Member
Band Members → [Form up] → Formation shape
Formation + Movement → [Flow field] → Coordinated motion
Band Morale → [Affects] → Individual combat stats
Band + Enemy Band → [Engagement] → Combat resolution
```

### Feedback Loops

- **Positive:** Victories boost morale → better performance → more victories
- **Negative:** Casualties lower morale → break formation → more casualties
- **Balance:** Retreat mechanics prevent death spirals

---

## System Dynamics

### Inputs
- Player commands: <CLARIFICATION NEEDED: Direct RTS orders or indirect influence?>
- Villager availability: From `VillagerAvailability` component ✅
- Enemy presence: <UNDEFINED: Enemy system?>
- Terrain: <UNDEFINED: Pathfinding integration?>

### Internal Processes
1. <FOR REVIEW> Recruitment: Villagers join band at armory/barracks
2. <FOR REVIEW> Formation: Members arrange in tactical pattern
3. <FOR REVIEW> Movement: Group moves as unit (flow field or leader follow?)
4. <FOR REVIEW> Combat: Engagements resolved (see Combat system)
5. <FOR REVIEW> Morale: Aggregated from members, affects group behavior

### Outputs
- Coordinated movement (not individual wandering)
- Combat effectiveness multipliers
- Morale state (routing, holding, charging)
- Formation visuals (shield walls look cool)

---

## State Machine

**<WIP: Band states not defined>**

### Proposed States
1. **Forming:** Gathering members - Entry: Recruitment started - Exit: Min size reached
2. **Idle:** Standing ready - Entry: No orders - Exit: Order received or enemy sighted
3. **Moving:** Traveling to location - Entry: Move order - Exit: Destination reached
4. **Engaged:** In combat - Entry: Enemy in range - Exit: Enemy defeated/fled
5. **Routing:** Fleeing - Entry: Morale broke - Exit: Rally distance reached
6. **Disbanded:** Dissolving - Entry: Player order or all members dead - Exit: N/A

### Transitions
```
Forming → Idle [min members recruited]
Idle → Moving [move order OR enemy sighted]
Moving → Engaged [contact with enemy]
Engaged → Routing [morale < threshold]
Routing → Idle [rally, morale restored]
Any → Disbanded [player command OR casualties > 80%]
```

---

## Key Metrics

| Metric | Target Range | Critical Threshold |
|--------|--------------|-------------------|
| Band size | 5-20 villagers | < 3 (ineffective), > 50 (micro nightmare) |
| Formation cohesion | 80-100% in position | < 50% (broken formation) |
| Morale | 40-100 | < 20 (routing imminent) |
| Combat multiplier | 1.2x - 2.0x | < 1.0x (worse than individuals?) |

---

## Balancing

- **Self:** Morale degrades with casualties, restores with victories
- **Player:** Can boost morale with miracles, disbanding to cut losses
- **System:** Formation bonuses balance individual vs group tactics

---

## Scale & Scope

### Small Scale (Single Band: 5-10 villagers)
- Skirmish unit, raiding party
- Fast, mobile, weak to focused fire

### Medium Scale (Multiple Bands: 20-50 villagers total)
- Coordinated army
- Combined arms (melee + ranged)
- Tactical depth (flanking, focus fire)

### Large Scale (Full Army: 50+ villagers)
- Epic battles
- <UNDEFINED: Performance limits? Max band count?>

---

## Time Dynamics

### Short Term (Seconds)
- Formation adjustments during movement
- Combat exchanges (attack/defend cycles)

### Medium Term (Minutes)
- Morale shifts from battle outcomes
- Recruitment/reinforcement

### Long Term (Hours)
- Veteran bands with experience bonuses <FOR REVIEW>
- Persistent band identities <FOR REVIEW>

---

## Failure Modes

- **Death Spiral:** Low morale → poor performance → more casualties → routing - Recovery: Rally mechanics, reinforcements
- **Stagnation:** Bands stuck in combat forever (no resolution) - Recovery: <UNDEFINED: Combat resolution mechanics?>
- **Runaway:** One super-band dominates (no strategic choice) - Recovery: Upkeep costs? Size penalties?

---

## Player Interaction

- **Observable:** Formation shapes, morale indicators, combat status
- **Control Points:** <CLARIFICATION NEEDED: RTS-style orders or god game influence?>
- **Learning Curve:** Beginner (gather army) → Intermediate (use formations) → Expert (combined arms tactics)

---

## Systemic Interactions

### Dependencies
- Combat System: <UNDEFINED: How is combat resolved?>
- Villager Availability: ✅ `VillagerAvailability` component exists
- Pathfinding: <UNDEFINED: Unity NavMesh? Flow fields?>
- Morale System: 🟡 `VillagerMood` exists for individuals

### Influences
- Village Population: Recruiting reduces worker pool
- Resource Economy: Soldiers don't gather/build
- Prayer Generation: <UNDEFINED: Do soldiers generate prayer?>

### Synergies
- Miracles + Bands: Battlefield support (heal, shield, offensive)
- Creature + Bands: <UNDEFINED: Does creature lead bands?>

---

## Exploits

- Infinite recruitment (entire village becomes army) → Severity: High - Fix: <NEEDS SPEC: Recruitment limits? Upkeep?>
- Formation kiting (no melee can catch) → Severity: Medium - Fix: <NEEDS SPEC: Movement speed vs formation integrity?>

---

## Tests

- [ ] <UNDEFINED: Recruitment mechanics>
- [ ] <UNDEFINED: Formation maintains shape during movement>
- [ ] <UNDEFINED: Morale breaks at threshold>
- [ ] <UNDEFINED: Disbanded members return to civilian pool>

---

## Performance

- **Complexity:** O(n) per band member for formation, O(n²) potential for combat resolution
- **Max entities:** <NEEDS SPEC: 100 villagers in combat? 500?>
- **Update freq:** <NEEDS SPEC: Per frame? 10 Hz? Formation vs combat>

---

## Visual Representation

### Formation Types

**<FOR REVIEW: Which formations?>**

```
Line Formation (melee)
[V] [V] [V] [V] [V]

Wedge (charge)
    [V]
   [V][V]
  [V]  [V]

Shield Wall (defense)
[S][S][S][S][S]
[V][V][V][V][V]

Skirmish (ranged)
  [A]   [A]
[A]       [A]
    [A]
```

### Data Flow

```
<WIP: Placeholder - needs combat system design>

[Player command?] → [BandAI?] → [Formation calculation]
                                        ↓
[Members] → [Update positions] → [Visual cohesion]
                                        ↓
[Enemy contact?] → [Combat resolution?] → [Casualties?]
                                        ↓
[Morale update] → [State change?]
```

---

## Iteration Plan

- **v1.0 (MVP):** <UNDEFINED: Is combat even in MVP?> - If yes: Simple grouping, no formations
- **v2.0:** <FOR REVIEW> Formation types, basic morale
- **v3.0:** <FOR REVIEW> Advanced tactics, veterancy, combined arms

**⚠️ DESIGN DECISION NEEDED:** Is combat/bands in initial scope or post-launch?

---

## Open Questions

1. **Are bands in MVP scope?** Combat system is stub only
2. **Player control model:** RTS orders vs god influence vs fully autonomous?
3. **Formation mechanics:** Simple shape or complex tactical bonuses?
4. **Morale vs individual mood:** Separate systems or linked?
5. **Recruitment:** Manual (player selects) or automatic (AI decides)?
6. **Disbanding:** Returns villagers to civilian pool?
7. **Experience/Veterancy:** Do bands remember victories?
8. **Unit types:** Just "villagers" or specialized (swordsmen, archers)?
9. **Upkeep/Costs:** Free army or maintenance costs?
10. **Band size limits:** Min/max per band?

---

## References

- **Black & White 2:** Army platoons with formations
- **Total War:** Formation bonuses and morale system
- **Age of Empires:** Unit grouping and control
- Legacy spec: `generaltruth.md` - "squad-level BT, flow fields for massed units"

---

## Related Documentation

- Truth Sources: `Docs/TruthSources_Inventory.md#bands` (currently ❌ Not Implemented)
- Combat System: `Docs/Concepts/Combat/` (to be created)
- Villager Availability: Truth source ✅ exists
- Morale: Truth source ✅ `VillagerMood` exists for individuals

---

## Current Truth Source Status

**From Truth Sources Inventory:**

```
### 4. Bands (Villager Groups)

Status: ❌ Not Implemented  
Expected Components: Band, BandMembership, BandMorale, BandFormation  
Registry: None (needed: BandRegistry)  
Telemetry: None

Proposed Truth Sources:
1. BandId - Unique band identifier
   - Fields: Value (int), FactionId, LeaderId
   - <NEEDS SPEC: How are bands created/destroyed?>
   
2. BandComposition - Member tracking
   - Fields: MemberCount, MaxMembers, AverageDisciplineLevel
   - <NEEDS SPEC: Member list as buffer or separate component?>
   
3. BandMorale - Group morale state
   - Fields: CurrentMorale, MoraleModifiers
   - <CLARIFICATION: Use VillagerMood aggregate or separate?>
   
4. BandFormation - Tactical positioning
   - Fields: FormationType, Spacing, Facing
   - <UNDEFINED: Formation types list?>
   
5. BandMembershipElement (buffer) - Member entities
   - Fields: VillagerEntity, Role
   - <FOR REVIEW: Roles like leader, standard bearer?>
```

**Required Systems (Proposed):**
- `BandFormationSystem` - Maintain formation shapes
- `BandMoraleSystem` - Aggregate morale from members
- `GodgameBandSyncSystem` - Mirror to registry component
- `GodgameBandRegistryBridge` - Publish to shared registry

**⚠️ ALL SYSTEMS ARE STUBS OR NON-EXISTENT**

---

## Implementation Notes

**Truth Sources:** `Docs/TruthSources_Inventory.md#bands`

**Components:** <ALL PROPOSED, NONE IMPLEMENTED>

**Systems:** `BandSystem.cs` exists but is **EMPTY**

**Authoring:** <DOES NOT EXIST>

---

**For Implementers:** DO NOT START until combat system scope defined  
**For Designers:** FIRST decide: Is combat in MVP? If yes, this is high priority. If no, defer entirely.  
**For Product:** KEY DECISION: God game (indirect) or RTS (direct control)?

