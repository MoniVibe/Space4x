# Miracle System Vision

**Status:** Draft - <WIP: No miracle implementation exists>  
**Category:** System - Divine Powers  
**Scope:** Global  
**Created:** 2025-10-31  
**Last Updated:** 2025-10-31

**⚠️ CURRENT STATE:**
- 🟡 `RainMiracleSystem.cs` file exists but is **EMPTY** (0 lines of code)
- 🟡 `MiracleRuntimeState` referenced in registry bridge but **NEVER USED**
- ❌ No miracle components defined
- ❌ No activation system
- ❌ No miracle effects
- ❌ No gesture recognition
- ❌ No cooldown tracking

**⚠️ CRITICAL DESIGN DECISIONS NEEDED:**
1. **Resource Model:** Prayer power cost OR cooldown-only OR free?
2. **Activation:** Button-click OR hand gestures OR both?
3. **Targeting:** Mouse position OR area selection OR auto-target?
4. **Scope:** 3-5 miracles for MVP OR full 10+ roster?
5. **Miracle List:** Which miracles are actually in scope?

---

## Purpose

**Primary Goal:** God powers that directly impact the world  
**Secondary Goals:**
- Express player agency (solve problems with divine intervention)
- Create spectacular moments (epic visual effects)
- Balance player power (cooldowns/costs prevent spam)

**Inspiration:** 
- Black & White 2: "Miracles: Fire, Water, Heal, Shield, Lightning, Meteor, Verdant; Epics: Earthquake, Hurricane, Volcano"
- Legacy spec: "Data-driven effects with area query, cost curve vs followers, VFX hooks"

---

## System Overview

### Components

1. **Miracle Types** <UNDEFINED: How many? Which ones?>
   - Role: Available god powers
   - Type: Catalog/registry
   - Truth Source: <NEEDS DESIGN>

2. **Miracle Runtime State** <REFERENCED BUT UNUSED>
   - Role: Active miracle tracking
   - Type: Component per miracle instance?
   - Truth Source: `MiracleRuntimeState` mentioned in bridge, never implemented

3. **Activation Input** <UNDEFINED>
   - Role: How player triggers miracles
   - Type: <NEEDS SPEC: Gestures? Buttons? Both?>

4. **Effect System** <UNDEFINED>
   - Role: What miracles actually DO
   - Type: <NEEDS SPEC: Per-miracle systems? Generic effect applier?>

5. **Cooldown Tracker** <UNDEFINED>
   - Role: Prevent spam
   - Type: Per-miracle timer or global?

### Connections

```
<WIP: Placeholder connections - needs design>

Player Input → [Activation] → Miracle trigger
                                    ↓
                              [Check cost?]
                                    ↓
                              [Check cooldown?]
                                    ↓
                              [Spawn effect]
                                    ↓
                              [Apply to targets]
                                    ↓
                              [Set cooldown]
                                    ↓
                         [Consume prayer? OR free?]
```

### Feedback Loops

- **Positive:** <UNDEFINED: Do miracles generate prayer? (impressive acts)>
- **Negative:** <UNDEFINED: Costs/cooldowns limit spam>
- **Balance:** <NEEDS DESIGN>

---

## System Dynamics

### Inputs
- Player activation: <NEEDS SPEC: Input method?>
- Target location: Mouse cursor position? Area selection?
- Available prayer: <UNDEFINED: Is prayer system even implemented?>

### Internal Processes
1. <UNDEFINED: Miracle selection UI?>
2. <UNDEFINED: Activation validation (cost, cooldown)?>
3. <UNDEFINED: Effect spawning?>
4. <UNDEFINED: Area-of-effect calculation?>
5. <UNDEFINED: Target filtering (friendly vs enemy)?>

### Outputs
- Visual effects (particles, shaders, camera shake)
- Gameplay effects (heal, damage, terrain change)
- Audio (dramatic sounds, music cues)
- <UNDEFINED: Prayer consumption?>
- <UNDEFINED: Cooldown start?>

---

## Miracle Roster

**<WIP: Miracle list not finalized>**

### Currently in Code
- **Rain/Water:** `RainMiracleSystem.cs` exists but **EMPTY** 🟡

### From Legacy Inspiration (Black & White 2)
**Everyday Miracles:**
- Water <PARTIAL: File exists, not implemented>
- Fire <CONCEPT ONLY>
- Heal <CONCEPT ONLY>
- Shield <CONCEPT ONLY>
- Lightning <CONCEPT ONLY>
- Meteor <CONCEPT ONLY>
- Verdant (growth) <CONCEPT ONLY>

**Epic Miracles:**
- Earthquake <CONCEPT ONLY>
- Hurricane <CONCEPT ONLY>
- Volcano <CONCEPT ONLY>
- Siren <CONCEPT ONLY>

**⚠️ DESIGN DECISION:** Which miracles for MVP? Recommend 3-5 only.

### Proposed MVP Shortlist (Needs Approval)
1. **Water** - Utility (extinguish fires, refresh villagers)
2. **Heal** - Support (restore villager health)
3. **Fire** - Offensive (damage, clear obstacles)
4. <NEEDS DECISION: 2 more for variety?>

---

## Activation Methods

**✅ DECIDED: Black & White 2 style dispensation (button/menu based)**

### Primary Method: Button/Menu (Black & White 2 Style)
- UI button for each miracle
- Click button → select parameters (intensity, mode) → click target location
- **Pros:** 
  - Familiar from Black & White 2
  - Simple, precise
  - Works with existing UI systems
- **Cons:** 
  - Less "gestural" than original Black & White
  - Requires UI overlay

### Cancel Gesture: Side-to-Side Shake
- Shake mouse/hand side-to-side to cancel active miracle selection
- Provides tactile feedback without complex gesture recognition
- **Implementation:** Track rapid horizontal mouse movement (threshold: 3+ direction changes in 0.5s)

### Future Dispensation Methods (Maybe)
- <DESIGN QUESTION: What "added dispensation methods" beyond buttons?>
- <FOR REVIEW: Radial menu? Hotkeys? Context-sensitive auto-aim?>
- <FOR REVIEW: Voice commands for accessibility?>

**Current Implementation:** ❌ None - awaiting basic button UI

---

## Key Mechanics

### Targeting
**<NEEDS SPEC>**
- Point-and-click (single location)?
- Area selection (drag circle)?
- Auto-target (nearest valid)?
- Smart targeting (find all enemies in radius)?

### Range
**<NEEDS SPEC>**
- Global (anywhere on map)?
- Limited by camera view?
- Limited by influence ring?
- Per-miracle range?

### Duration
**<FOR REVIEW>**
- Instant (lightning strike)?
- Over time (rain for 10 seconds)?
- Persistent (shield until dispelled)?

### Intensity
**<FOR REVIEW>**
- Fixed power?
- Scales with prayer spent?
- Player-controlled charge (like slingshot)?

---

## Resource Model

**<CRITICAL DECISION NEEDED>**

### Option A: Prayer Power Cost
- Each miracle costs prayer (e.g., 100-10,000)
- Strategic resource management
- **Requires:** Prayer system implemented first
- **Pros:** Economic depth, choices matter
- **Cons:** Complex, blocks miracles if no prayer

### Option B: Cooldown Only
- Miracles are free
- Limited by time (e.g., 30s-5min cooldowns)
- **Requires:** Only timer tracking
- **Pros:** Simple, always available
- **Cons:** Less strategic depth

### Option C: Hybrid
- Small miracles: cooldown only
- Epic miracles: prayer cost + long cooldown
- **Pros:** Balance complexity and accessibility
- **Cons:** Two systems to balance

**Current Implementation:** ❌ None - prayer system doesn't exist yet

---

## State Machine

**<WIP: Miracle lifecycle undefined>**

### Proposed States
1. **Available:** Ready to cast - Entry: Cooldown expired - Exit: Activation
2. **Activating:** Player targeting - Entry: Input started - Exit: Confirmed or canceled
3. **Casting:** VFX playing - Entry: Activation confirmed - Exit: Duration complete
4. **Active:** Effect ongoing - Entry: Cast complete - Exit: Duration expires
5. **Cooldown:** Recharging - Entry: Effect ended - Exit: Cooldown timer done

### Transitions
```
Available → Activating [player input started]
Activating → Available [canceled]
Activating → Casting [target confirmed]
Casting → Active [for sustained effects]
Casting → Cooldown [for instant effects]
Active → Cooldown [duration expires]
Cooldown → Available [timer reaches 0]
```

**<NEEDS IMPLEMENTATION: None of this exists>**

---

## Key Metrics

**<PLACEHOLDER VALUES - NOT FINAL>**

| Metric | Target Range | Critical Threshold |
|--------|--------------|-------------------|
| Miracles per minute | 1-3 | > 10 (spam), < 0.5 (boring) |
| Player usage rate | 60-80% use miracles | < 30% (not engaging) |
| Strategic variety | Use 3+ different miracles | Only use 1 (imbalanced) |

---

## Balancing

- **Self:** <UNDEFINED: What naturally limits miracles?>
- **Player:** <NEEDS SPEC: Can choose when/where to cast>
- **System:** Cooldowns + <MAYBE> prayer costs

---

## Scale & Scope

### Small Miracles (Tactical)
- Single target or small area (5-10m radius)
- Quick cast, short cooldown (5-30s)
- Examples: Heal single villager, small fire

### Medium Miracles (Strategic)
- Medium area (20-30m radius)
- Moderate cooldown (30-120s)
- Examples: Water area, shield group

### Epic Miracles (Game-Changing)
- Large area (50-100m radius)
- Long cooldown (5-10 minutes)
- Examples: Earthquake, volcano, hurricane

**<NEEDS DECISION: Which tiers for MVP?>**

---

## Time Dynamics

### Short Term (Seconds)
- Activation delay: <NEEDS SPEC: Instant or wind-up?>
- Effect duration: <NEEDS SPEC: 1s instant or 10s sustained?>

### Medium Term (Minutes)
- Cooldown recovery
- Player timing decisions

### Long Term (Hours)
- <FOR REVIEW: Miracle usage patterns over session?>
- <UNDEFINED: Do miracles unlock/upgrade?>

---

## Failure Modes

- **Death Spiral:** <UNDEFINED: Can't use miracles when needed most?>
- **Stagnation:** <UNDEFINED: Never use miracles (too precious syndrome)?>
- **Runaway:** Spam miracles, trivialize gameplay - Recovery: Meaningful cooldowns

---

## Player Interaction

- **Observable:** <NEEDS SPEC: Miracle UI? Cooldown indicators?>
- **Control Points:** Activation trigger, target selection
- **Learning Curve:** Beginner (discover first miracle) → Intermediate (strategic timing) → Expert (combo miracles?)

---

## Systemic Interactions

### Dependencies
- Prayer System: <UNDEFINED: If using prayer costs>
- Input System: ✅ `InputReaderSystem` exists
- VFX System: <UNDEFINED: Particle systems?>
- Villager Reactions: <UNDEFINED: Do villagers respond to miracles?>

### Influences
- Combat: <UNDEFINED: Offensive miracles damage?>
- Villager Morale: <FOR REVIEW: Miracles boost morale?>
- Environment: <FOR REVIEW: Weather miracles affect climate?>

### Synergies
- Hand + Miracle: <FOR REVIEW: Can hold resources while casting?>
- Bands + Miracle: <FOR REVIEW: Battlefield support?>

---

## Exploits

- <UNDEFINED: Can't identify exploits without implementation>

---

## Tests

- [ ] <UNDEFINED: Activation mechanics>
- [ ] <UNDEFINED: Effect application>
- [ ] <UNDEFINED: Cooldown tracking>
- [ ] <UNDEFINED: VFX spawning>
- [ ] <UNDEFINED: Target filtering>

---

## Performance

- **Complexity:** <NEEDS SPEC: Per-frame checks? Event-driven?>
- **Max entities:** <NEEDS SPEC: How many active miracle effects?>
- **Update freq:** <NEEDS SPEC: Continuous or pulse-based?>

---

## Visual Representation

**<WIP: No VFX implementation>**

### Proposed Visual Style (Pending Approval)

**Water/Rain:**
- Sky darkens, rain particles, puddles appear
- <NEEDS SPEC: Duration? Particle count?>

**Fire:**
- Flames spawn, spread over time, burn targets
- <NEEDS SPEC: Fire propagation mechanics?>

**Heal:**
- Golden glow on target, particle sparkles
- <NEEDS SPEC: Single target or area?>

**Lightning:**
- Sky flash, bolt strikes, electric arcs
- <NEEDS SPEC: Single strike or chain?>

**Earthquake:**
- Screen shake, ground cracks, buildings damaged
- <NEEDS SPEC: Destruction mechanics?>

---

## Iteration Plan

- **v1.0 (MVP):** <DECISION POINT> Pick 1-3 miracles, button activation, cooldown-only
- **v2.0:** <FOR REVIEW> Add 3-5 more miracles, gesture input optional
- **v3.0:** <FOR REVIEW> Epic miracles, prayer costs, advanced VFX

**⚠️ CANNOT START** until miracle roster and activation method decided

---

## Open Questions

### Critical (Must Answer First)
1. **How many miracles for MVP?** 3? 5? 10?
2. ✅ **Activation method:** Button/menu (Black & White 2 style) + side-to-side shake to cancel
3. **Resource model?** Prayer costs, cooldowns, or free?
4. **Targeting system?** Point-click, area select, auto?

### Important (Can Defer)
5. Do miracles unlock progressively or all available from start?
6. Do miracles have upgrade paths (level 1→2→3)?
7. Can miracles combo/interact (water + lightning)?
8. Do miracles affect environment persistently (lasting fire)?
9. How do villagers react to miracles (awe, fear)?
10. Can player cancel miracle mid-cast?

### Nice to Have
11. Gesture customization (player defines own gestures)?
12. Miracle history/stats tracking?
13. Miracle skins/visual variants?

---

## Miracle Candidates (For Discussion)

**From Legacy Inspiration:**

### Support Miracles
- **Water/Rain** - Refresh villagers, extinguish fires, grow crops
  - Status: 🟡 File exists, empty
  - Priority: <FOR REVIEW: High (utility)>
  
- **Heal** - Restore villager health
  - Status: ❌ Not started
  - Priority: <FOR REVIEW: High (villager value)>
  
- **Shield** - Protect area from damage
  - Status: ❌ Not started
  - Priority: <FOR REVIEW: Medium (combat dependency)>

### Offensive Miracles
- **Fire** - Damage, clear obstacles
  - Status: ❌ Not started
  - Priority: <FOR REVIEW: High (versatile)>
  
- **Lightning** - Precision strike
  - Status: ❌ Not started
  - Priority: <FOR REVIEW: Medium>
  
- **Meteor** - Area damage
  - Status: ❌ Not started
  - Priority: <FOR REVIEW: Low (spectacle)>

### Environmental Miracles
- **Verdant** - Rapid plant growth
  - Status: ❌ Not started
  - Priority: <FOR REVIEW: Low (needs vegetation system)>
  - Dependency: ❌ `VegetationGrowthSystem` is empty stub

### Epic Miracles (Late Game)
- **Earthquake** - Devastation
- **Hurricane** - Area denial
- **Volcano** - Apocalyptic
  - Status: ❌ All not started
  - Priority: <FOR REVIEW: Post-MVP?>

**⚠️ RECOMMENDATION:** Start with 3 miracles max for MVP - Water, Heal, Fire

---

## Activation Flow

**<NEEDS COMPLETE DESIGN>**

### Proposed (Button-Based MVP)
```
Player clicks miracle button in UI
    ↓
<NEEDS SPEC: Check availability?>
    ↓
<NEEDS SPEC: Check prayer cost?>
    ↓
Cursor changes (miracle icon)
    ↓
Player clicks target location
    ↓
<NEEDS SPEC: Validation (in range? valid target?)>
    ↓
Spawn miracle effect entity
    ↓
Apply effects over time/instantly
    ↓
Set cooldown timer
    ↓
<NEEDS SPEC: Consume prayer?>
```

### Alternative (Gesture-Based)
```
Player draws gesture (circle, zigzag, etc.)
    ↓
<NEEDS IMPLEMENTATION: Gesture recognition>
    ↓
Match gesture to miracle type
    ↓
[Continue as above]
```

**Current Implementation:** ❌ None

---

## Effect Types

**<FOR REVIEW: How do miracles actually affect the world?>**

### Buff Effects
- Heal: Modify `VillagerNeeds.Health` ✅ (component exists)
- Shield: <UNDEFINED: Damage reduction component?>
- Blessing: <UNDEFINED: Stat boost component?>

### Damage Effects
- Fire: <UNDEFINED: Damage over time component?>
- Lightning: <UNDEFINED: Instant damage?>
- Earthquake: <UNDEFINED: Area damage + terrain deformation?>

### Environmental Effects
- Rain: <UNDEFINED: Moisture system interaction?>
- Verdant: <UNDEFINED: Vegetation growth trigger?>
- Tornado: <UNDEFINED: Physics force field?>

**Current Implementation:** ❌ None

---

## Parameters

**<PLACEHOLDER - NOT FINAL>**

| Miracle | Cost | Cooldown | Radius | Duration | Effect |
|---------|------|----------|--------|----------|--------|
| Water | <SPEC> | <SPEC> | <SPEC> | <SPEC> | <SPEC> |
| Heal | <SPEC> | <SPEC> | <SPEC> | <SPEC> | <SPEC> |
| Fire | <SPEC> | <SPEC> | <SPEC> | <SPEC> | <SPEC> |

**⚠️ CANNOT DEFINE** until miracle roster and mechanics decided

---

## Example

**<WIP: Hypothetical scenario>**

**Given:** Player has 5000 prayer, Water miracle available  
**When:** <UNDEFINED: How does player activate?>  
**Then:** <UNDEFINED: What actually happens?>

**EXAMPLE BLOCKED** until basic miracle flow designed

---

## Player Feedback

**<NEEDS DESIGN>**

- **Visual:** <SPEC: VFX style? Particles? Shaders?>
- **Audio:** <SPEC: Sound effects? Music cues?>
- **UI:** <SPEC: Cooldown UI? Cost display?>

---

## Balance

**<CANNOT BALANCE WITHOUT IMPLEMENTATION>**

- **Early:** <UNDEFINED>
- **Mid:** <UNDEFINED>
- **Late:** <UNDEFINED>

### Exploits
- <CANNOT IDENTIFY> without knowing mechanics

---

## Interaction Matrix

| Other Mechanic | Relationship | Notes |
|----------------|--------------|-------|
| Prayer System | Dependency? | <UNDEFINED: If using costs> |
| Hand State | Conflict? | <SPEC: Can cast while holding?> |
| Combat | Synergy | <UNDEFINED: Combat exists?> |
| Villagers | Target | ✅ Can affect villagers (Heal, Damage) |

---

## Technical

- **Max entities:** <SPEC: Active miracle effects simultaneously?>
- **Update freq:** <SPEC: Per frame? Event-driven?>
- **Data needs:** <UNDEFINED: Miracle components?>

---

## Tests

- [ ] <BLOCKED: No mechanics to test>
- [ ] <BLOCKED: Activation system first>
- [ ] <BLOCKED: Effect system first>

---

## Performance

- **Complexity:** <UNDEFINED>
- **Max entities:** <UNDEFINED>
- **Update freq:** <UNDEFINED>

---

## Visual Representation

**<CONCEPT SKETCH ONLY>**

```
[Player] → [Activation input?] → [Miracle UI?]
                                        ↓
                                  [Target selection?]
                                        ↓
                                  [Effect spawn?]
                                        ↓
                           [Apply to targets in area?]
```

**ALL STEPS UNDEFINED**

---

## Iteration Plan

- **v0.1 (Design):** ← **WE ARE HERE** - Define mechanics, activation, roster
- **v1.0 (MVP):** 1-3 miracles, button activation, cooldown-only
- **v2.0:** 5-7 miracles, gesture input optional, VFX polish
- **v3.0:** Epic miracles, prayer costs, environmental persistence

**⚠️ STUCK AT v0.1** until design decisions made

---

## Open Questions

1. **Resource model:** Prayer cost or cooldown-only? (CRITICAL)
2. **Activation:** Buttons or gestures or both? (CRITICAL)
3. **Miracle count:** How many for MVP? (CRITICAL)
4. **Targeting:** How does player aim? (IMPORTANT)
5. **Roster:** Which miracles first? (IMPORTANT)
6. **VFX scope:** Simple or spectacular? (IMPORTANT)
7. **Progression:** Unlocking system? (NICE TO HAVE)
8. **Combos:** Do miracles interact? (NICE TO HAVE)

---

## References

- **Black & White 2:** Gesture-based miracles, prayer power costs
- **Populous:** Mana-based god powers
- **From the Dust:** Elemental interaction (water + lava)
- Legacy: `C:\Users\Moni\Documents\claudeprojects\godgame\truthsources\` (no miracle-specific doc found)
- Legacy general: "Data-driven effects with area query, cost curve vs followers, VFX hooks"

---

## Related Documentation

- Truth Sources: `Docs/TruthSources_Inventory.md#miracles` (Status: ❌ Not Implemented)
- Prayer Power: `Docs/Concepts/Core/Prayer_Power.md` (also <WIP>)
- First Miracle Experience: `Docs/Concepts/Experiences/First_Miracle.md`

---

## Current Truth Source Status

**From Inventory:**

```
### 5. Miracles (Divine Powers)

Status: ❌ Partially Implemented (RainMiracleSystem exists but no truth source tracking)  
Expected Components: MiracleRuntimeState, MiracleCooldown, MiracleEffect  
Registry: None (needed: MiracleRegistry)  
Telemetry: None

Proposed Truth Sources:
1. MiracleId - Miracle type identifier
   - Fields: Type (enum), PlayerId
   - <NEEDS DESIGN: Component structure?>
   
2. MiracleRuntimeState - Current state
   - Fields: State (Ready/Active/Cooldown), ActivationTick, DurationRemaining
   - Referenced in: GodgameRegistryBridgeSystem (lookup exists but unused) ⚠️
   
3. MiracleCooldown - Cooldown tracking
   - Fields: CooldownDuration, RemainingTicks, ChargesAvailable
   - <NEEDS IMPLEMENTATION>
   
4. MiracleEffect - Active effect tracking
   - Fields: EffectType, TargetArea, Intensity, TicksRemaining
   - <NEEDS IMPLEMENTATION>
```

**ALL PROPOSED, NONE IMPLEMENTED**

---

## Implementation Notes

**Truth Sources:** `Docs/TruthSources_Inventory.md#miracles`

**Components:** <ALL NEED CREATION>

**Systems:** 
- `RainMiracleSystem.cs` ← **EMPTY FILE** 🟡
- <NEEDS: Activation system, effect system, cooldown system>

**Authoring:** <DOES NOT EXIST>

---

**For Implementers:** STOP. Do not implement until design decisions made (see Critical Open Questions)  
**For Designers:** START HERE. Answer the 3 critical questions (resource model, activation, roster)  
**For Product:** This is a CORE FEATURE. Needs dedicated design session to define mechanics.

---

**NEXT STEP:** Design workshop to answer critical questions, then update this document with concrete specs.

