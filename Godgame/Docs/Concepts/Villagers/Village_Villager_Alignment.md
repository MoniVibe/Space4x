# Village, Villager & Alignment Interplay

**Status:** Draft - <WIP: Core identity concepts>  
**Category:** System - Social Dynamics  
**Scope:** Individual → Village → God relationship  
**Created:** 2025-10-31  
**Last Updated:** 2025-10-31

**⚠️ CURRENT STATE:**
- ✅ Individual villagers exist with identity (`VillagerId`, `VillagerMood`)
- ❌ No "Village" entity exists (just individual villagers)
- ❌ No alignment system exists
- ❌ No personality/disposition system
- ❌ No village culture/collective identity

**⚠️ FUNDAMENTAL QUESTIONS:**
1. **What IS a village?** Entity? Conceptual grouping? Spatial bounds?
2. **Do villagers have personalities?** Or just functional attributes?
3. **Is there village-level culture?** Or just individuals?
4. **How does god alignment affect villagers?** Direct? Through village?
5. **Can villagers disagree with god?** Flee? Rebel? Or always obedient?

---

## Purpose

**Primary Goal:** Create living, reactive social layer between god and world  
**Secondary Goals:**
- Villages feel like communities (not just villager collections)
- Individual villagers feel unique (names, traits, memories)
- God-villager relationship has depth (worship, fear, love, resentment)
- Alignment creates meaningful cultural shifts

**Inspiration:**
- **Black & White:** Villagers react to god's actions with fear or love
- **Dwarf Fortress:** Individual personalities and preferences
- **Rimworld:** Colonist moods and relationships
- **The Sims:** Individual needs and autonomy

---

## System Overview

### Components

1. **Individual Villager** ✅ EXISTS (Partial)
   - Role: Autonomous agent with needs and job
   - Type: Entity with identity
   - Truth Source: `VillagerId`, `VillagerNeeds`, `VillagerMood`, `VillagerJob`

2. **Village** <UNDEFINED: Does this exist?>
   - Role: Collective identity, spatial grouping?
   - Type: <CLARIFICATION: Entity? Abstract concept? Spatial zone?>

3. **Villager Personality** <UNDEFINED>
   - Role: Individual traits, preferences
   - Type: <FOR REVIEW: Brave/cowardly, loyal/independent, etc.?>

4. **Village Culture** <UNDEFINED>
   - Role: Collective outlook, traditions
   - Type: <FOR REVIEW: Aggregate of villagers or separate entity?>

5. **God-Villager Relationship** <UNDEFINED>
   - Role: How villager feels about god
   - Type: <FOR REVIEW: Love, fear, loyalty, resentment?>

### Connections

```
<WIP: Proposed relationship model>

God Alignment (Global)
        ↓
   Influences
        ↓
Village Culture (Collective)
        ↓
   Influences
        ↓
Individual Villagers (Agents)
        ↓
   React based on
        ↓
Personal Disposition + Village Culture + God Actions
        ↓
   Express through
        ↓
Behavior (Work rate, worship, flee, rebel?)
```

### Feedback Loops

- **Positive (Good God):** Kind actions → happy villagers → productive → thriving village → more prayer → more miracles → more kindness
- **Positive (Evil God):** Intimidation → fearful villagers → obedient → forced productivity → prayer from fear → more intimidation
- **Negative (Mixed):** Inconsistent actions → confused villagers → low morale → rebellion → need to reconquer?
- **Balance:** Consistent alignment (good OR evil) creates stable society

---

## Three Layers of Identity

### Layer 1: God (Player)

**Exists:** ❌ No alignment system yet  
**Proposed:** Global god alignment value (-100 evil to +100 good)

**Affects:**
- Miracle availability/costs
- Building visual styles
- Villager base reactions
- World atmosphere (sky, weather)

**Player Expression:**
- "I am a merciful protector"
- "I am a demanding tyrant"  
- "I am a pragmatic overseer"

---

### Layer 2: Village (Collective)

**Exists:** ❌ No village entity  
**Concept:** <NEEDS DESIGN>

**Option A: Village is Just Spatial**
- No village entity
- "Village" = villagers within X radius of town center
- Culture = aggregate of individual villagers
- **Pros:** Simple, emergent
- **Cons:** No persistent village identity

**Option B: Village is Entity**
- Village has components: `VillageId`, `VillageCulture`, `VillageReputation`
- Villagers belong to village (parent entity)
- Village has collective stats
- **Pros:** Clear identity, can have village-level mechanics (diplomacy?)
- **Cons:** More complex, what if villagers scatter?

**Option C: Village is Hybrid**
- Virtual concept (no entity) but tracked culturally
- Culture = aggregate moods + building types + god influence
- Emergent identity without hard entity
- **Pros:** Flexible, emergent
- **Cons:** May feel vague

**<CLARIFICATION NEEDED: Is there even a "village" in this game?>**

**Possible Village Attributes (If Implemented):**
```
VillageCulture {
    Dominant Outlook       // Good-leaning, evil-leaning, neutral
    Collective Morale      // Average villager moods
    Prosperity Level       // Resources, buildings, health
    Worship Intensity      // How much they pray
    Fear Level            // How intimidated by god
    Love Level            // How much they adore god
    Loyalty               // Will they obey or rebel?
}
```

---

### Layer 3: Individual Villager

**Exists:** ✅ Basic identity and needs  
**Truth Sources:**
- `VillagerId` - Unique ID, faction
- `VillagerMood` - Current morale (0-100)
- `VillagerNeeds` - Health, energy
- `VillagerJob` - Current task
- `VillagerDisciplineState` - Profession/skill

**Missing (Design Concept):**
```
VillagerPersonality {
    // Core traits (if implemented)
    byte Bravery;          // Cowardly (0) to Heroic (100)
    byte Independence;     // Obedient (0) to Rebellious (100)
    byte Spirituality;     // Pragmatic (0) to Devout (100)
    byte Contentedness;    // Easily upset (0) to Easygoing (100)
    
    // God relationship
    byte LoyaltyToGod;     // Will flee (0) to Die for god (100)
    byte FearOfGod;        // Trusting (0) to Terrified (100)
    byte LoveForGod;       // Indifferent (0) to Worshipful (100)
}

VillagerMemory {
    // Recent experiences (if implemented)
    byte HealsReceived;          // God healed me
    byte HarmedByGod;            // God hurt me
    byte MiraclesWitnessed;      // Saw god's power
    uint TicksSinceGodInteraction; // How long since direct contact
}
```

**<NEEDS DECISION: Are villagers just functional units or do they have personalities?>**

---

## Alignment Interplay

### God → Village → Villager Flow

**Scenario 1: Good God, Good Village, Happy Villagers**
```
God casts Heal frequently (+good actions)
    ↓
God Alignment: +60 (Good)
    ↓ Influences
Village Culture: Good-leaning (collective +40 alignment)
    ↓ Influences
Individual Villagers:
  - Base morale +10 (happy god)
  - Worship willingly (high love, low fear)
  - Productive but not forced
  - Build temples and gardens
  - Visual: Bright clothes, smiles
```

**Scenario 2: Evil God, Evil Village, Fearful Villagers**
```
God sacrifices villagers, casts destructive miracles (-evil actions)
    ↓
God Alignment: -70 (Evil)
    ↓ Influences
Village Culture: Evil-leaning (collective -50 alignment)
    ↓ Influences
Individual Villagers:
  - Base morale -15 (terrified)
  - Worship from fear (low love, high fear)
  - Highly productive (fear-driven)
  - Build fortifications and altars
  - Visual: Dark clothes, cowering
```

**Scenario 3: Good God, Evil Village (Mismatch - Interesting!)**
```
<FOR REVIEW: What happens when god and village misalign?>

New good god takes over evil village
    ↓
God Alignment: +60 (Good)
Village Culture: Still -40 (Evil, slow to change)
    ↓
Individual Villagers:
  - Confused (culture says fear, god says love)
  - Some convert (spirituality high → trust new god)
  - Some remain fearful (low spirituality → expect punishment)
  - Culture slowly shifts toward good (takes time)
  - Visual: Mixed (some bright, some dark during transition)
```

**Design Question:** How fast does village culture shift? Instant? Gradual? Never fully?

---

## Villager Reactions to God

**<NEEDS SPEC: How do villagers respond to god's presence/actions?>**

### When God Hand Approaches

**Good God + High Love Villager:**
- Look up with joy
- Reach toward hand
- Animation: Excited, hopeful
- <FOR REVIEW: Special voice lines? "Our protector!">

**Evil God + High Fear Villager:**
- Cower, flee
- Hide behind objects
- Animation: Trembling, backing away
- <FOR REVIEW: Voice lines? "Please no!">

**Neutral God + Pragmatic Villager:**
- Pause work, observe
- Curious but cautious
- Animation: Watch and wait
- <FOR REVIEW: Voice: "What does the god want?">

**Mismatch (Good God + Evil-Aligned Villager):**
- Suspicious (doesn't trust kindness)
- Slow to warm up
- Animation: Hesitant approach
- <FOR REVIEW: Conversion over time?>

---

## Village Identity

**<FUNDAMENTAL DESIGN QUESTION: What IS a "village"?>**

### Option A: No Village Entity (Emergent)
- "Village" = collection of villagers near buildings
- No explicit village entity
- Culture = average of individual villagers
- Boundaries = spatial proximity

**Pros:**
- Simple, no new entity type
- Emergent culture from individuals
- Flexible (villages merge, split naturally)

**Cons:**
- No persistent village identity
- Hard to show "village" UI/stats
- Village diplomacy impossible (no entity to reference)

---

### Option B: Village as Entity (Structured)

**Village Entity Has:**
```
VillageId : IComponentData {
    int VillageId;
    FixedString64Bytes VillageName;  // "Oakshire", "Ironhold"
    int FoundingTick;
}

VillageCulture : IComponentData {
    float CollectiveAlignment;   // Average of god influence + villager dispositions
    float Prosperity;            // Resources, buildings, health
    byte ArchitectureStyle;      // Influenced by alignment
    float WorshipIntensity;      // How much they pray
}

VillagePopulation : IComponentData {
    int TotalVillagers;
    int Children;      // <FOR REVIEW: Age system?>
    int Adults;
    int Elders;        // <FOR REVIEW: Aging?>
}

VillageBounds : IComponentData {
    float3 Center;     // Town center location
    float Radius;      // Influence radius
}

// Buffer of member villagers
VillageMember : IBufferElementData {
    Entity VillagerEntity;
    uint JoinedTick;   // When they joined this village
}
```

**Pros:**
- Clear identity (name, stats, culture)
- Can track village-level progression
- Diplomacy possible (village vs village)
- UI can show village stats

**Cons:**
- More complex
- What if villager leaves? (migration mechanics)
- Performance (extra entity per settlement)

---

### Option C: Hybrid (Spatial + Cultural)
- No village entity, but track culture per spatial cell
- Spatial grid cells have culture values
- Villagers in cell influence cell culture
- God actions in cell affect cell culture
- Buildings in cell shaped by culture

**Pros:**
- Emergent village boundaries
- No hard entity, but tracked identity
- Can have multiple "villages" in one area
- Culture spreads spatially

**Cons:**
- Complex (culture per grid cell)
- May feel disconnected

**<RECOMMENDATION: Start with Option A (no village entity), add Option B if needed>**

---

## Villager Personality & Disposition

**<NEEDS DESIGN: Are villagers individuals or just units?>**

### Current State (Truth Sources)
**What villagers HAVE now:**
- ✅ `VillagerId` - Unique ID
- ✅ `VillagerMood` - Current morale (functional)
- ✅ `VillagerNeeds` - Health, energy (functional)
- ✅ `VillagerDisciplineState` - Profession (Forester, Miner)
- ✅ `VillagerJob` - Current task
- ✅ `VillagerAIState` - AI state machine

**What villagers DON'T have:**
- ❌ Personality traits (brave, cowardly, spiritual)
- ❌ Relationship to god (love, fear, loyalty)
- ❌ Memory of events (god healed me, god hurt me)
- ❌ Individual names (just "Villager-123")
- ❌ Preferences (likes/dislikes)

### Minimal Personality (Functional Diversity)

**<FOR REVIEW: MVP could just have functional roles>**
```
Villager = VillagerJob.Type + VillagerDisciplineState
  - Forester acts like forester (no personality)
  - Miner acts like miner
  - Builder acts like builder
  
Visual diversity:
  - Different clothing per job
  - Different animations per job
  - No deeper personality needed
```

**Pros:** Simple, already mostly implemented  
**Cons:** Villagers feel like units, not people

---

### Rich Personality (Individual Characters)

**<FOR REVIEW: Full sim could give each villager traits>**
```
VillagerPersonality : IComponentData {
    FixedString64Bytes Name;    // "Alden", "Brianna", "Cedric"
    
    // Core traits (0-100 scale)
    byte Bravery;        // Affects combat, flee threshold
    byte Independence;   // Obeys orders vs does own thing
    byte Spirituality;   // Prayer rate, miracle reactions
    byte Sociability;    // Works alone vs in groups
    byte Contentment;    // Base mood modifier
}

VillagerGodRelation : IComponentData {
    // How this villager feels about god
    byte LoyaltyToGod;   // 0-100: Will they flee if unhappy?
    byte FearOfGod;      // 0-100: Intimidated by god's power
    byte LoveForGod;     // 0-100: Adoration, willing worship
    byte Trust;          // 0-100: Believes god will protect them
}

VillagerMemory : IComponentData {
    // Recent god interactions (last 10?)
    byte DirectHealsReceived;      // God healed ME specifically
    byte NearbyHealsWitnessed;     // Saw god heal others
    byte HarmedByGod;              // God hurt me (sacrifice, throw)
    byte MiraclesWitnessed;        // Saw god's power
    uint TicksSinceGodContact;     // How long since direct interaction
}
```

**Pros:** Deep simulation, villagers feel alive, memorable moments  
**Cons:** Complex, expensive (memory/processing), hard to balance

---

### Middle Ground (Dispositions, Not Full Personality)

**<RECOMMENDATION: Simplified traits affecting god relationship>**
```
VillagerDisposition : IComponentData {
    // Just 2-3 traits that matter for god relationship
    byte Loyalty;        // 0-100: Will stay vs flee
    byte GodAffinity;    // Prefers good god (100) or evil god (0)
    
    // Derived from god actions + personality seed
    byte FearLevel;      // Current fear of god
    byte LoveLevel;      // Current love for god
}
```

**Pros:** Adds depth without explosion of complexity  
**Cons:** Less rich than full personality

**<NEEDS DECISION: Minimal, middle, or full personality system?>**

---

## God Alignment → Villager Reaction Matrix

**<DESIGN CONCEPT: How alignment affects individual villagers>**

| God Alignment | Villager Disposition | Reaction | Behavior |
|---------------|---------------------|----------|----------|
| Good (+60) | Good-aligned (high spirituality) | **Love** | Joyful, productive, volunteer for tasks |
| Good (+60) | Evil-aligned (low spirituality) | **Suspicion** | Slow to trust, lower productivity initially |
| Good (+60) | Neutral | **Contentment** | Standard behavior, gradual trust |
| Evil (-60) | Evil-aligned (high independence) | **Respect** | Obedient, productive from intimidation |
| Evil (-60) | Good-aligned (high spirituality) | **Fear/Flee** | Want to escape, low morale, may run away |
| Evil (-60) | Neutral | **Compliance** | Obey but unhappy, functional fear |
| Neutral (0) | Any | **Pragmatic** | Work-focused, transactional worship |

**Emergent Behaviors:**
- Good god attracts good villagers (migration in)
- Evil god loses good villagers (flee to neutral lands)
- Villages self-sort over time based on god alignment

**<FOR REVIEW: Is migration a feature or too complex?>**

---

## Village Culture Emergence

**<CONCEPT: How does collective culture form?>**

### Without Village Entity (Aggregate)

**Village Culture = Function of:**
1. **God Alignment** (strongest influence) - Sets baseline tone
2. **Building Types** (architectural expression) - Temples vs altars, gardens vs fortifications
3. **Villager Average Disposition** (collective mood) - Happy villagers = positive culture
4. **Historical Events** (memory) - Was village attacked? Saved by miracle?

**Calculation:**
```
Village "Culture" (conceptual) = 
  (God Alignment × 0.5) +
  (Average Villager Mood × 0.3) +  
  (Building Style Score × 0.2)
  
Range: -100 (hellish) to +100 (paradise)
```

**Visual Expression:**
- Culture -80 to -100: Pure evil aesthetic (all dark)
- Culture -40 to -80: Evil leaning (mostly dark, some neutral)
- Culture -20 to +20: Mixed (natural, practical)
- Culture +40 to +80: Good leaning (mostly bright, some neutral)
- Culture +80 to +100: Pure good aesthetic (paradise)

**No village entity needed - just visual rules based on aggregate!**

---

### With Village Entity (Tracked)

**Village entity actively tracks:**
```
// Historical memory
VillageHistory : IBufferElementData {
    uint Tick;
    EventType Event;  // Attack, Miracle, Feast, Sacrifice
    float AlignmentImpact;
}

// Current culture state
VillageCulture : IComponentData {
    float CollectiveAlignment;  // Influenced by god but drifts
    float AlignmentDrift;       // How fast culture changes
}
```

**Culture Updates:**
```
Each tick:
  Target = God Alignment
  Current = Village Collective Alignment
  Drift = lerp(Current, Target, DriftSpeed × deltaTime)
  
DriftSpeed factors:
  - Villager spirituality (high = faster drift)
  - Village age (older = slower drift, tradition)
  - Recent events (miracle = faster shift)
```

**Creates lag:** Good god takes time to "convert" evil village culture

---

## Reactions to God Actions

**<DESIGN SCENARIOS>**

### God Heals Sick Villager

**Good-Aligned Villager:**
- Reaction: Gratitude, love increases
- Behavior: Works harder, prays more
- Memory: "God saved me" (+loyalty)
- Cultural impact: +2 village alignment

**Evil-Aligned Villager:**
- Reaction: Surprise, suspicion
- Behavior: Unchanged (doesn't trust kindness)
- Memory: "God healed me... why?" (neutral)
- Cultural impact: +0.5 (small shift)

**Neutral Villager:**
- Reaction: Appreciation
- Behavior: Slight mood boost
- Memory: "God helped when needed" (+small loyalty)
- Cultural impact: +1

---

### God Throws Villager Across Map (Comedy/Cruelty)

**Good-Aligned Villager:**
- Reaction: Shock, fear increases
- Behavior: Avoids god hand, may flee village
- Memory: "God threw me!" (-loyalty)
- Cultural impact: -5 village alignment (cruel act)

**Evil-Aligned Villager:**
- Reaction: Intimidated but respects power
- Behavior: More obedient
- Memory: "God is powerful" (+fear, -love)
- Cultural impact: -2 (evil action but expected)

**Neutral Villager:**
- Reaction: Annoyed if gentle, terrified if lethal
- Behavior: Depends on outcome (survived? injured?)
- Memory: "God is unpredictable" (±small shift)
- Cultural impact: -3

**Design Question:** Should throw always be cruel or can it be playful/helpful? (Throwing villager TO safety?)

---

## Migration & Dissent

**<BIG DESIGN QUESTION: Can villagers leave if unhappy?>**

### Option A: No Migration (Captive)
- Villagers stuck in village
- Low loyalty = low productivity, but can't flee
- **Pros:** Simple, no population loss
- **Cons:** No consequence for poor god behavior

### Option B: Free Migration (Autonomous)
```
IF: Villager.LoyaltyToGod < 20
AND: Villager.FearOfGod < 30 (not too scared to run)
AND: Alternative village exists (neutral or aligned with villager)
THEN: Villager flees (walks off map or to other village)
```

**Pros:** Real consequence, villagers feel autonomous  
**Cons:** Can lose population, complex (where do they go?)

### Option C: Rebellion (Dramatic)
```
IF: Many villagers unhappy (>50% loyalty < 20)
AND: God alignment conflicts with village culture
THEN: Rebellion event
  - Villagers stop working
  - Some attack god's buildings
  - Require: Appeasement (miracle) or Force (intimidation)
```

**Pros:** Dramatic moments, high stakes  
**Cons:** Can be frustrating, requires suppression mechanics

**<NEEDS DECISION: Are villagers subjects or slaves?>**

---

## Visual Expression

### Individual Villager Visuals

**Based on their reaction to god:**

**High Love (Good God):**
- Bright, clean clothes
- Flower crowns or decorations
- Upright posture
- Happy animations (skip, wave at god)

**High Fear (Evil God):**
- Dark, ragged clothes  
- Hunched posture
- Furtive glances at sky
- Nervous animations (check over shoulder)

**Mixed (Confused/Transitional):**
- Mismatched clothing
- Uncertain posture
- Conflicted animations

---

### Village Aesthetic

**Based on collective culture:**

**Good Culture Villages:**
- Buildings: Bright wood, white stone, flowers
- Layout: Organized streets, central temple, open plazas
- Decorations: Gardens, fountains, banners
- Sounds: Happy chatter, music, birds
- Lighting: Warm, inviting

**Evil Culture Villages:**
- Buildings: Dark stone, iron, spikes
- Layout: Fortified clusters, imposing central tower
- Decorations: Skulls, chains, braziers
- Sounds: Silence, chains rattling, ominous drums
- Lighting: Dark, shadows, fire

**Neutral Villages:**
- Buildings: Natural wood/stone mix
- Layout: Organic, practical
- Decorations: Minimal, functional
- Sounds: Work sounds, normal life
- Lighting: Natural daylight

---

## Morale vs Alignment

**<CLARIFICATION: How do these interact?>**

### Morale (Existing: VillagerMood)
- **What it is:** Immediate happiness/sadness
- **Affects:** Productivity, prayer generation, animations
- **Changes:** Quickly (events, needs met/unmet)
- **Range:** 0-100

### Alignment/Disposition (Proposed)
- **What it is:** Moral outlook, god relationship
- **Affects:** Behavior choices, loyalty, flee threshold
- **Changes:** Slowly (accumulated experiences)
- **Range:** -100 to +100 (or 0-100 per component)

**Relationship:**
```
Good-aligned villager under evil god:
  - Alignment: Prefers good (hard to change)
  - Morale: Low (god conflicts with values)
  - Behavior: Unhappy, may flee
  
Evil-aligned villager under evil god:
  - Alignment: Matches god (comfortable)
  - Morale: Can be high (god aligns with values)
  - Behavior: Obedient, productive
```

**Design:** Alignment = personality/values, Morale = current happiness

---

## Iteration Plan

- **v0.1 (Design):** ← **WE ARE HERE** - Define relationships, decide on systems
- **v1.0 (MVP - Recommend SKIP):** <IF implemented> God alignment only, simple visual changes
- **v2.0:** Village culture tracking, villager dispositions
- **v3.0:** Full personality, migration, rebellion

**⚠️ SCOPE RECOMMENDATION:**
- **MVP:** Skip alignment entirely, focus on core gameplay
- **v2.0 Update:** Add alignment as content expansion
- **v3.0+:** Deepen with personalities, migration

**Why Skip MVP:**
- Doubles art requirements (good/evil variants)
- Adds balance complexity
- Villagers functional without it
- Can add later without breaking existing content

---

## Open Questions

### Foundational (Must Answer)
1. **Is there a "village" entity?** Or just spatial grouping of villagers?
2. **Do villagers have personalities?** Beyond functional jobs?
3. **Is alignment in scope at all?** Or defer to post-launch?
4. **God alignment vs village culture?** Both? Just god?

### If Alignment In Scope
5. How fast does alignment shift?
6. Can alignment be reset/changed?
7. Visual only or mechanical bonuses?
8. How many tiers/stages?
9. Can villagers migrate/rebel?
10. Does alignment affect victory conditions?

### Individual Villager Depth
11. Named villagers or just IDs?
12. Memory system (remember god actions)?
13. Traits (brave, spiritual, independent)?
14. Relationships between villagers?
15. Aging/lifecycle?

---

## Design Scenarios (Exploring Possibilities)

### Scenario: The Beloved Village
> You've been a good god for 2 hours. Your village of 80 villagers adores you.
> 
> Buildings are bright and flowery. Villagers skip to work singing. Prayer flows constantly.
> 
> An enemy attacks. You cast Shield to protect them. They cheer.
> 
> One villager gets separated. You gently pick them up (hand), carry them to safety.
> 
> That villager's loyalty shoots to 100. They build a shrine to you. Others see and are inspired.
> 
> Village culture: +85 (Paradise). Everyone happy, productive, devoted.

### Scenario: The Feared Settlement
> You've been cruel. Sacrificed villagers for power. Cast destructive miracles.
> 
> Buildings are dark fortresses. Villagers work in silence, fearful.
> 
> Prayer comes from fear, not love. Productivity is high (intimidation) but morale is low.
> 
> You pick up a villager. They tremble. You set them down gently (mercy?).
> 
> That villager is confused. Fear drops slightly. Tells others. Tiny shift toward neutral.
> 
> Village culture: -70 (Dark). Oppressive but functional. Will they rebel eventually?

### Scenario: The Conflicted Community
> You start good (+40). Build temples, heal villagers. Then you get impatient.
> 
> You throw a slow villager. Others see. Alignment drops to +25.
> 
> Half villagers still love you (spirituality high, forgive mistakes).
> Half villagers now uncertain (was that an accident or god's nature?).
> 
> Village culture: +20 (Neutral-leaning-good). Mixed visuals. Some bright, some dim.
> 
> Do you commit to good (heal to regain trust) or embrace chaos (keep acting inconsistently)?

**Design Value:** These scenarios show system potential without committing to implementation!

---

## References

- **Black & White:** Individual villager reactions to god, alignment from actions
- **Dwarf Fortress:** Deep personality simulation
- **Rimworld:** Mood and relationship systems
- **Fable:** Alignment affects world and NPC reactions
- Legacy: `generaltruth.md` - "Alignment affects visuals and options"

---

## Related Documentation

- Alignment System: `Docs/Concepts/Progression/Alignment_System.md` (god alignment)
- Villager Needs: Truth sources ✅ `VillagerMood`, `VillagerNeeds` exist
- Village Culture: <UNDEFINED: Needs design if separate from god alignment>
- Prayer Power: `Docs/Concepts/Core/Prayer_Power.md` (mentions alignment bonuses)

---

## Truth Source Status

**Currently Exist:**
- ✅ `VillagerId` - Individual identity
- ✅ `VillagerMood` - Current morale (could represent disposition?)
- ✅ `VillagerNeeds` - Functional needs

**Would Need (If Personality/Alignment Implemented):**
- ❌ `VillagerPersonality` - Traits
- ❌ `VillagerDisposition` - God relationship
- ❌ `VillagerMemory` - Event tracking
- ❌ `VillageId` - If village entities exist
- ❌ `VillageCulture` - Collective identity
- ❌ `GodAlignment` - Player alignment value

**ALL CONCEPTUAL - NONE IMPLEMENTED**

---

**For Designers:** CRITICAL - Define relationship model (god → village → villager) before building systems  
**For Product:** Major scope decision - Functional villagers (MVP) vs Personality sim (v2.0+)  
**For Narrative:** If personalities in scope, enables emergent stories and player attachment to individuals



