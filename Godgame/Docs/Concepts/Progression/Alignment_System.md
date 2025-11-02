# Alignment & Outlook System

**Status:** Draft - <WIP: Pure concept, no implementation>  
**Category:** System - Player Progression  
**Scope:** Global (affects entire player god identity)  
**Created:** 2025-10-31  
**Last Updated:** 2025-10-31

**⚠️ CURRENT STATE:**
- ❌ No alignment components exist
- ❌ No good/evil tracking
- ❌ No outlook system
- ❌ No visual variation based on alignment
- ❌ Complete greenfield design

**⚠️ CRITICAL DESIGN DECISIONS:**
1. **Is alignment even in scope?** Good vs Evil or skip entirely?
2. **Who has alignment?** Player god? Individual villagers? Villages?
3. **How is it measured?** Actions? Choices? Buildings? Miracles?
4. **What does it affect?** Visuals only? Mechanics? Both?
5. **Is it reversible?** Can player change alignment or locked in?

---

## Purpose

**Primary Goal:** Player choices shape their god's identity and worldview  
**Secondary Goals:**
- Replayability (different playstyles feel different)
- Moral expression (player's values reflected)
- Visual diversity (good vs evil looks distinct)
- Strategic variety (different powers/bonuses)

**Inspiration:**
- **Black & White 2:** "Alignment: good vs evil changes visuals and options, affected by actions (warfare skews evil)"
- **Fable:** Actions shape character appearance and NPC reactions
- **Dishonored:** Chaos system affects world state and ending

---

## System Overview

### Components

1. **God Alignment** <UNDEFINED: Does this exist?>
   - Role: Player's moral standing
   - Type: Global singleton value
   - Range: <FOR REVIEW: -100 (pure evil) to +100 (pure good)?>

2. **Village Outlook** <UNDEFINED: Per-village or global?>
   - Role: Settlement's cultural identity
   - Type: <CLARIFICATION: Influenced by god or independent?>

3. **Villager Disposition** <UNDEFINED: Individual personalities?>
   - Role: How villagers react to god's alignment
   - Type: <FOR REVIEW: Loyalty, fear, love metrics?>

4. **Alignment Actions** <UNDEFINED: What shifts alignment?>
   - Role: Track moral choices
   - Type: Action catalog (help, harm, create, destroy)

### Connections

```
<WIP: Conceptual flow only>

Player Actions → [Moral weight] → Alignment shift
                                        ↓
                                  God Alignment
                                  /           \
                            Affects:         Affects:
                          /      |       \         \
                  Visuals  Miracles  Villagers  Buildings
                     ↓        ↓         ↓          ↓
                  Colors   Costs    Reactions   Style
```

### Feedback Loops

- **Positive (Good):** Help villagers → happiness → prosperity → more followers → more prayer
- **Positive (Evil):** Intimidate villagers → fear → obedience → forced labor → rapid expansion
- **Negative:** Extreme alignment limits options (too good = can't use offensive miracles effectively)
- **Balance:** Neutral path has flexibility but no bonuses

---

## Alignment Measurement

**<NEEDS COMPLETE DESIGN>**

### How Does Alignment Shift?

**Proposed Actions (Examples):**

#### Good Actions (+alignment)
- Cast heal miracle on villagers: +5
- Bless newborn villagers: +3
- Build temple/shrine: +10
- Protect village from enemy: +8
- <FOR REVIEW> Resurrect dead villager: +15
- <FOR REVIEW> Create food/water in famine: +12

#### Evil Actions (-alignment)
- Cast offensive miracle on own villagers: -15
- Sacrifice villager for power: -20
- Destroy own buildings: -10
- Ignore villager prayers/needs: -2 (per incident)
- <FOR REVIEW> Throw villager to death: -8
- <FOR REVIEW> Start aggressive war: -12

#### Neutral Actions (0)
- Build storehouse: 0
- Gather resources: 0
- Organize villagers: 0
- Defensive combat: 0

**Design Questions:**
1. Should ALL actions have alignment value or only key moral choices?
2. Can player see alignment score or is it hidden?
3. Is there a "point of no return" (locked into good/evil)?
4. Do opposite actions cancel out or is shift permanent?

---

## Alignment Effects

**<FOR REVIEW: What actually changes?>**

### Visual Changes (Most Important for Player Identity)

#### Good God (Alignment > 50)
**Sky/Atmosphere:**
- Bright, clear skies
- Golden sunlight
- Rainbows after rain miracles
- Gentle weather

**Buildings:**
- Bright wood tones
- White/cream stone
- Flower decorations
- Gardens and fountains
- Round, welcoming architecture

**Villagers:**
- Happy animations (skipping, dancing)
- Bright clothing colors
- Worship poses (hands raised in joy)

**God Hand:**
- Golden glow
- Particle sparkles (fairy dust)
- Gentle pickup/release animations

**Miracles:**
- Soft VFX (healing light, gentle rain)
- Warm color palette (gold, white, green)
- Harmonious sound effects

#### Evil God (Alignment < -50)
**Sky/Atmosphere:**
- Dark, stormy clouds
- Red/purple tint
- Lightning storms
- Oppressive weather

**Buildings:**
- Dark stone (obsidian, iron)
- Sharp angles, spikes
- Skulls and chains decorations
- Imposing towers
- Fortified, intimidating architecture

**Villagers:**
- Fearful animations (cowering, trembling)
- Dark, ragged clothing
- Worship poses (bowing in fear)

**God Hand:**
- Dark purple/red glow
- Smoke tendrils
- Forceful, aggressive animations

**Miracles:**
- Harsh VFX (flames, lightning, dark energy)
- Cool/dark color palette (red, black, purple)
- Discordant, ominous sounds

#### Neutral (Alignment -20 to +20)
**Balanced:**
- Natural tones
- Practical architecture
- Mixed villager moods
- Standard effects

---

### Mechanical Changes (Gameplay Impact)

**<CLARIFICATION NEEDED: Should alignment affect mechanics or just visuals?>**

#### Option A: Visual Only
- Alignment changes appearance, NOT gameplay
- **Pros:** Pure expression, no balance issues
- **Cons:** Feels shallow (cosmetic only)

#### Option B: Mechanical Bonuses
**Good Path:**
- Prayer generation: +20% from happy villagers
- Heal miracles: -25% prayer cost
- Villager productivity: +10%
- **Penalty:** Offensive miracles +50% cost

**Evil Path:**
- Prayer from fear: Generate from intimidation
- Offensive miracles: -25% cost
- Combat effectiveness: +15%
- **Penalty:** Heal miracles +50% cost, villager morale always lower

**Pros:** Meaningful choice, affects strategy  
**Cons:** Balance nightmare, forces playstyle

#### Option C: Unlocks Different Content
- Good path unlocks: Heal shrine, paradise garden, angelic creature skin
- Evil path unlocks: Sacrifice altar, torture chamber, demonic creature skin
- **Pros:** Replayability, distinct experiences
- **Cons:** Doubles content requirements

**<NEEDS DECISION: Which option or hybrid?>**

---

## Alignment Tracking

**<NEEDS SPEC: How is alignment stored?>**

### Single Axis (Simple)
```
Evil (-100) ←―――――――――[0]―――――――――→ Good (+100)
```
- Single value, easy to understand
- Clear good vs evil
- **Issue:** What about lawful/chaotic?

### Two Axes (Complex)
```
        Good (+100)
             ↑
             |
Evil (-100) ←+→ Lawful (+100)
             |
             ↓
        Chaotic (-100)
```
- Richer personality (lawful good, chaotic evil, etc.)
- More nuanced choices
- **Issue:** Complex UI, harder to balance

### Trait-Based (Very Complex)
- Multiple sliders: Merciful, Wrathful, Orderly, Playful, etc.
- Rich personality
- **Issue:** Overwhelming for player

**<RECOMMENDATION: Start with single axis (Good ↔ Evil), add complexity if needed>**

---

## Villager Reactions

**<FOR REVIEW: How do villagers respond to god's alignment?>**

### Good God
- Villagers worship willingly
- Happy animations near god hand
- Prayers are grateful ("Thank you, merciful one")
- High morale baseline (+10)

### Evil God
- Villagers worship out of fear
- Cowering when god hand approaches
- Prayers are desperate ("Please spare us!")
- Low morale baseline (-10) BUT productivity from fear?

### Neutral God
- Villagers pragmatic
- Standard reactions
- Balanced morale

**Design Question:** Should villagers have individual alignment preferences?
- Some villagers prefer good god (flee from evil)
- Some villagers prefer evil god (attracted to power)
- Creates migration dynamics?

---

## Alignment Progression

**<NEEDS SPEC: Does alignment lock or can it change?>**

### Option A: Locked Path (Choose Once)
- At game start or early moment: "Choose your nature"
- Commit to good or evil
- Actions reinforce chosen path
- **Pros:** Clear identity, easier to balance
- **Cons:** No redemption arc, less dynamic

### Option B: Dynamic (Always Shifting)
- Start neutral
- Every action shifts alignment
- Can swing from good to evil and back
- **Pros:** Reactive, redemption possible
- **Cons:** May feel inconsistent, harder to commit to aesthetic

### Option C: Hybrid (Soft Lock)
- Start neutral, actions shift gradually
- Thresholds (+50 or -50) "lock" into path
- Returning to neutral is possible but difficult
- **Pros:** Best of both, commitment with flexibility
- **Cons:** Complex to explain

**<RECOMMENDATION: Option C - soft lock at thresholds>**

---

## Visual Transitions

**<NEEDS SPEC: How do visuals change when alignment shifts?>**

### Instant Flip
- Cross threshold → immediate visual change
- **Pros:** Clear feedback
- **Cons:** Jarring, no gradual descent into evil

### Gradual Transition
- Buildings slowly darken/brighten over time
- Villagers clothes change color gradually
- Sky shifts tone each session
- **Pros:** Smooth, believable corruption/redemption
- **Cons:** Slow feedback, may not notice

### Hybrid (Staged)
- Small shifts at alignment checkpoints (-75, -50, -25, 0, +25, +50, +75)
- Each stage has distinct visual set
- **Pros:** Clear stages, gradual progression
- **Cons:** 7 visual variants needed per asset

**<RECOMMENDATION: Staged approach with 3-5 tiers>**

---

## Alignment Tiers (Proposed)

### Pure Good (+75 to +100)
**Identity:** Benevolent Creator  
**Visuals:** Radiant gold, white marble, gardens everywhere  
**Mechanics:** <FOR REVIEW> Heal miracles free, offensive miracles disabled?  
**Villager Mood:** Ecstatic (base +20 morale)

### Good (+25 to +75)
**Identity:** Caring Protector  
**Visuals:** Bright wood, clean stone, flowers  
**Mechanics:** Heal bonus, prayer bonus from happiness  
**Villager Mood:** Happy (base +10 morale)

### Neutral (-25 to +25)
**Identity:** Pragmatic Overseer  
**Visuals:** Natural materials, balanced aesthetics  
**Mechanics:** No bonuses or penalties  
**Villager Mood:** Neutral (base 0 morale)

### Evil (-75 to -25)
**Identity:** Demanding Tyrant  
**Visuals:** Dark stone, iron, minimal decoration  
**Mechanics:** Offensive bonus, prayer from fear  
**Villager Mood:** Fearful (base -10 morale, but productivity +10%?)

### Pure Evil (-100 to -75)
**Identity:** Wrathful Destroyer  
**Visuals:** Obsidian, spikes, skulls, fire  
**Mechanics:** <FOR REVIEW> Offensive miracles free, healing disabled?  
**Villager Mood:** Terrified (base -20 morale, high productivity from fear)

---

## Scale & Scope

### Individual Villagers
**<FOR REVIEW: Do villagers have personal alignment?>**
- Some attracted to good god (migrate in)
- Some attracted to evil god (seek power)
- Personality affects job preferences?

### Village Culture
**<FOR REVIEW: Does village develop collective outlook?>**
- Good god → village becomes paradise
- Evil god → village becomes dark fortress
- Mixed signals → conflicted culture?

### God's Nature
**Confirmed:** Player's alignment is primary
- Persistent across sessions (save/load)
- Shapes entire game world
- Defines available strategies

---

## Time Dynamics

### Short Term (Minutes)
- Single actions shift alignment slightly (±1 to ±5)
- Immediate feedback (visual pulse, UI indicator)

### Medium Term (Hours)
- Cross thresholds (±25, ±50, ±75)
- Visual transitions trigger
- New abilities unlock/lock

### Long Term (Campaign)
- Alignment defines identity
- End-game visuals reflect player's journey
- Victory conditions may depend on alignment

---

## Failure Modes

- **Death Spiral:** Evil god → all villagers flee → no prayer → can't recover - Recovery: <NEEDS SPEC: Minimum villager count? Auto-spawn refugees?>
- **Stagnation:** Stuck at neutral, no identity → boring - Recovery: Encourage stronger actions
- **Confusion:** Player doesn't understand what shifts alignment - Recovery: Clear feedback, alignment tutorial

---

## Player Interaction

- **Observable:** Visual changes, villager reactions, alignment UI bar/indicator
- **Control Points:** Every major action (miracles, building choices, villager treatment)
- **Learning Curve:** Beginner (discover alignment exists) → Intermediate (intentional path choice) → Expert (min-max bonuses)

---

## Systemic Interactions

### Dependencies
- Miracle System: <UNDEFINED> Miracles should have alignment weights
- Villager Mood: ✅ Exists - can be affected by god alignment
- Building System: <UNDEFINED> Buildings should reflect alignment
- Prayer Generation: <UNDEFINED> Different generation per alignment?

### Influences
- Building Aesthetics: Good = bright, Evil = dark
- Miracle Costs/Effects: Bonuses per alignment
- Villager Behavior: Reactions to god
- End Game: Victory conditions or endings

### Synergies
- Alignment + Miracles = different playstyles
- Alignment + Buildings = visual identity
- Alignment + Villagers = relationship dynamics

---

## Exploits

- Min-max farming (do good actions for bonus, then switch to evil) - Fix: <NEEDS SPEC: Soft lock prevents rapid switching?>
- Neutral forever (avoid commitment, keep flexibility) - Fix: Intentional or encourage stronger choices?

---

## Tests

**<DESIGN TESTS - No code yet>**
- [ ] Playtest: Can player identify their alignment from visuals alone?
- [ ] Playtest: Does good path feel distinct from evil?
- [ ] Playtest: Are alignment shifts satisfying or frustrating?
- [ ] Balance: Is neutral viable or always inferior?

---

## Performance

**<N/A for design phase>**
- Alignment is likely singleton value
- Visual swaps are art content load
- No major performance concerns expected

---

## Visual Representation

### Alignment Spectrum

```
Pure Evil  Evil   Neutral   Good   Pure Good
   ↓        ↓        ↓        ↓        ↓
  -100    -50       0       +50     +100
   │        │        │        │        │
  [🔥]    [⚔️]     [⚖️]     [✨]     [☀️]
   │        │        │        │        │
Destroy  Conquer  Balance  Protect Create
```

### Visual Tier Transitions

```
Stage 1: Neutral (Starting)
  - Natural wood/stone
  - Mixed colors
  - Balanced aesthetics

    ↓ Player acts good (+50)
    
Stage 2: Good
  - Brightening
  - Flowers appear
  - Villagers smile
  
    ↓ Continue good (+75)
    
Stage 3: Pure Good  
  - Radiant
  - Paradise aesthetics
  - Everything glows

OR

    ↓ Player acts evil (-50)
    
Stage 2: Evil
  - Darkening
  - Spikes appear
  - Villagers cower
  
    ↓ Continue evil (-75)
    
Stage 3: Pure Evil
  - Hellish
  - Fire and obsidian
  - Oppressive
```

---

## Iteration Plan

- **v1.0 (MVP):** <DECISION POINT> Is alignment in MVP or post-launch?
  - If YES: Simple good/evil visual variants (3 tiers), no mechanical bonuses
  - If NO: Defer entire system to v2.0+

- **v2.0:** Full alignment tracking, mechanical bonuses, 5 tiers

- **v3.0:** Individual villager dispositions, village culture, complex interactions

**⚠️ RECOMMEND:** Skip alignment for MVP (too complex), add in content update

---

## Open Questions

### Critical (Scope Decision)
1. **Is alignment in MVP scope?** Or post-launch feature?
2. **Visual only or mechanical?** Cosmetic vs gameplay impact?
3. **Single axis or multi-dimensional?** Good/evil vs good/evil/law/chaos?

### Important (If In Scope)
4. How many visual tiers? (3? 5? 7?)
5. What's the default starting state? (Neutral? Player chooses?)
6. Is alignment reversible? (Redemption arcs possible?)
7. How much alignment shift per action? (Granular or dramatic?)
8. Does alignment affect victory conditions?

### Nice to Have (Polish)
9. Alignment-specific narrator voice/tone?
10. Creature appearance based on god alignment?
11. NPC gods in multiplayer have alignment?
12. Achievements for pure good/evil playthroughs?

---

## Example Scenarios (Design Fiction)

### Scenario 1: Descent into Evil
> You start neutral. Your village is attacked. You cast Fire miracle on enemies (+0 neutral defense). 
> 
> It works! But you get carried away. You cast Fire on enemy village, killing civilians (-15 evil action).
> 
> Your alignment shifts to -15. Sky darkens slightly. Villagers look nervous.
> 
> You think: "Power is effective." You sacrifice a weak villager for prayer bonus (-20).
> 
> Alignment: -35. Buildings start darkening. Some villagers flee. Others bow in fear.
> 
> You've crossed into evil territory. The visual transformation has begun.

### Scenario 2: Path of Light
> Village is starving. You cast Water to help crops (+5 good). You Heal sick villagers (+5 each).
> 
> Alignment: +25. Flowers bloom around buildings. Villagers sing.
> 
> You build temple instead of armory (+10 good). You protect but don't attack enemies (+8).
> 
> Alignment: +53 (crossed good threshold). Everything brightens. Paradise aesthetic unlocks.
> 
> Villagers worship joyfully. You feel like a benevolent creator.

---

## References

- **Black & White 2:** Alignment system with visual/mechanical changes
- **Fable:** Actions shape character (horns, halo)
- **Dishonored:** Chaos system affects world
- **InFamous:** Hero vs Villain path with different powers
- Legacy: `generaltruth.md` - "Alignment: good vs evil changes visuals and options"

---

## Related Documentation

- Truth Sources: `Docs/TruthSources_Inventory.md` (no alignment components yet)
- Prayer Power: `Docs/Concepts/Core/Prayer_Power.md` (mentions alignment bonuses - <WIP>)
- Buildings: `Docs/Concepts/Buildings/Needs_Driven_Construction.md` (visual styles - <WIP>)
- Miracles: `Docs/Concepts/Miracles/Miracle_System_Vision.md` (cost modifiers - <WIP>)

---

## Truth Source Implications (If Implemented)

**Would Need:**
```
GodAlignment : IComponentData (singleton) {
    float AlignmentValue;  // -100 to +100
    byte AlignmentTier;    // 0-4 (pure evil to pure good)
    uint LastShiftTick;
}

AlignmentAction : IBufferElementData {
    FixedString64Bytes ActionId;
    float AlignmentDelta;  // ±value
    uint Tick;
}

VillagerDisposition : IComponentData {
    float LoyaltyToGod;    // Affected by alignment match
    byte FearLevel;        // High if evil god
    byte LoveLevel;        // High if good god
}

BuildingStyle : IComponentData {
    byte VisualVariant;    // 0-4 based on alignment tier
}
```

**Would Affect:**
- Building visuals (material swaps)
- Miracle costs (read alignment, modify cost)
- Villager mood (base morale shift)
- Prayer generation (happiness vs fear)

---

**For Designers:** CRITICAL - Decide if alignment in scope before building dependent systems (buildings, miracles)  
**For Product:** This is a MAJOR FEATURE - significant art/design/code cost. Recommend MVP skip, add later.  
**For Narrative:** If alignment in scope, affects entire game tone and player story.



