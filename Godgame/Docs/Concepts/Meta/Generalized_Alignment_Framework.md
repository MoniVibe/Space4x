# Generalized Alignment & Outlook Framework

**Status:** Draft - <WIP: Abstract design pattern>  
**Category:** Meta - Reusable System Framework  
**Scope:** Cross-Project (Godgame, Space4X, future games)  
**Created:** 2025-10-31  
**Last Updated:** 2025-10-31

**⚠️ PURPOSE:**
- Create **reusable alignment system** for any entity type
- Works for individuals AND aggregates (villages, factions, empires)
- Applicable to fantasy god game AND space 4X RTS
- Pure design pattern, not implementation

**⚠️ DESIGN PHILOSOPHY:**
- Entity-agnostic (works for villagers, ships, planets, factions)
- Scale-agnostic (individual → group → civilization)
- Genre-agnostic (fantasy, sci-fi, historical, modern)

---

## Core Concept

### What is Alignment/Outlook?

**Alignment:** Moral/ideological position on spectrum(s)  
**Outlook:** Cultural/behavioral expression of alignment  
**Disposition:** Individual/entity stance toward external forces

**Universal Pattern:**
```
Entity (individual or collective)
    ↓ Has
Alignment Values (moral/ideological position)
    ↓ Expresses through
Outlook (cultural/behavioral manifestation)
    ↓ Creates
Disposition toward Others (relationships)
```

---

## Three-Layer Architecture

### Layer 1: Individual Entities

**Examples Across Games:**
- Godgame: Individual villager
- Space4X: Individual ship, crew member, planet governor
- City Builder: Individual citizen
- Fantasy RPG: Individual NPC

**Components:**
```
EntityAlignment : IComponentData {
    // Moral/ideological values
    <VALUES: Game-specific axes>
}

EntityDisposition : IComponentData {
    // Reactions to external forces (player, factions, etc.)
    <RELATIONSHIPS: To whom? How measured?>
}

EntityMemory : IComponentData (optional) {
    // Event history affecting alignment
    <EVENTS: Recent experiences>
}
```

---

### Layer 2: Aggregate Entities (Collectives)

**Examples Across Games:**
- Godgame: Village (collection of villagers)
- Space4X: Fleet (collection of ships), Planet (collection of pops), Faction
- City Builder: District, neighborhood
- Fantasy RPG: Guild, faction, race

**Components:**
```
AggregateId : IComponentData {
    int Id;
    FixedString64Bytes Name;
    <IDENTITY: What defines this collective?>
}

AggregateCulture : IComponentData {
    // Collective alignment (aggregate or emergent)
    <VALUES: Averaged? Dominant? Voted?>
    float CultureStrength;     // How unified vs diverse
    float CultureDrift;        // How fast culture changes
}

AggregateMembers : IBufferElementData {
    Entity MemberEntity;
    <MEMBERSHIP: Weights? Roles?>
}
```

---

### Layer 3: External Forces (Influencers)

**Examples Across Games:**
- Godgame: Player god, rival gods
- Space4X: Player empire, AI empires, galactic council
- City Builder: Mayor (player), neighboring cities
- Fantasy RPG: Player, quest givers, enemy factions

**Components:**
```
ForceAlignment : IComponentData {
    // Force's moral/ideological position
    <VALUES: Same axes as entities>
}

ForceInfluence : IComponentData {
    // How much this force affects others
    float InfluenceRadius;    // Spatial or abstract
    float InfluenceStrength;  // Magnitude
    <MECHANICS: How is influence applied?>
}
```

---

## Alignment Axes (Configurable)

**<DESIGN CHOICE: Which axes matter for your game?>**

### Single Axis (Simplest)

**Good ↔ Evil:**
```
Evil (-100) ←―――[0]―――→ Good (+100)
```
**Use for:** Moral games (Godgame, Fable-style)

**Order ↔ Chaos:**
```
Chaos (-100) ←―――[0]―――→ Order (+100)
```
**Use for:** Civilization builders, empires

**Individualist ↔ Collectivist:**
```
Individual (-100) ←―――[0]―――→ Collective (+100)
```
**Use for:** Political strategy games

---

### Dual Axes (Richer)

**Moral + Social:**
```
         Good (+100)
              ↑
              |
Evil (-100) ←―+―→ Order (+100)
              |
              ↓
         Chaos (-100)
         
Creates 4 quadrants:
- Lawful Good (ordered paradise)
- Chaotic Good (free-spirited kindness)
- Lawful Evil (tyrannical order)
- Chaotic Evil (destructive anarchy)
```

**Use for:** Complex RPGs, civilization games

---

### Triple Axes (Complex)

**Economic + Military + Diplomatic:**
```
Capitalist ↔ Socialist (economic)
Pacifist ↔ Warlike (military)
Isolationist ↔ Expansionist (diplomatic)
```

**Use for:** 4X strategy (Space4X!)

---

### Custom Axes (Game-Specific)

**Space 4X Example:**
```
Technology Focus:
  Biological (-100) ←―→ Synthetic (+100)
  
Expansion Style:
  Defensive (-100) ←―→ Aggressive (+100)
  
Diplomacy:
  Xenophobic (-100) ←―→ Xenophilic (+100)
```

**Godgame Example:**
```
Divine Nature:
  Wrathful (-100) ←―→ Merciful (+100)
  
Intervention:
  Hands-off (-100) ←―→ Micromanaging (+100)
```

**<FLEXIBILITY: Define axes that matter for your game's themes>**

---

## Alignment Inheritance & Influence

### Individual → Aggregate (Bottom-Up)

**Aggregate alignment from members:**

**Option A: Simple Average**
```
Village Alignment = Average(All Villager Alignments)
```
**Pros:** Emergent, democratic  
**Cons:** Extreme individuals diluted

**Option B: Weighted Average**
```
Village Alignment = 
  Σ(Villager Alignment × Villager Influence Weight) / Total Weight
  
Influence Weight factors:
  - Leadership role (leaders matter more)
  - Population (dominant group defines culture)
  - Seniority (elders shape culture)
```
**Pros:** Realistic, leadership matters  
**Cons:** Complex weighting

**Option C: Dominant Faction**
```
Village Alignment = Majority Alignment
  - If 60% villagers are good → village is good
  - Minority adapts or leaves
```
**Pros:** Clear cultural identity  
**Cons:** Sudden flips when majority shifts

---

### External Force → Aggregate → Individual (Top-Down)

**Force influences collective and individuals:**

**God Game Example:**
```
God Alignment: +70 (Good)
    ↓ Influences
Village Culture: +45 (drifts toward god)
    ↓ Influences
Individual Villagers: 
  - High spirituality → +10 alignment (follows god)
  - Low spirituality → +2 alignment (slight drift)
  - Rebels → -5 alignment (resists god)
```

**Space 4X Example:**
```
Empire Ideology: Xenophilic +80
    ↓ Policies/propaganda
Planet Culture: Xenophilic +60 (drifts toward empire)
    ↓ Education/media
Individual Pops:
  - Conformist → +15 alignment (follows culture)
  - Independent → +5 alignment (slight shift)
  - Rebel → -10 alignment (resists)
```

**Pattern:** Force sets baseline → Collective drifts → Individuals react based on traits

---

## Disposition (Relationships)

**<CONCEPT: How entities feel about external forces>**

### Bidirectional Relationships

**Entity → Force Disposition:**
```
EntityDisposition : IComponentData {
    Entity TargetForce;      // Who this is about (player, rival faction)
    
    // Relationship metrics
    byte Loyalty;            // Will obey (0) to die for (100)
    byte Fear;               // Trusting (0) to terrified (100)
    byte Love;               // Hate (0) to adore (100)
    byte Trust;              // Distrustful (0) to believing (100)
    byte Respect;            // Contempt (0) to awe (100)
}
```

**Examples:**
```
Godgame:
  Villager → God:  Love 90, Fear 10, Loyalty 95 (devoted)
  Villager → God:  Love 5, Fear 85, Loyalty 30 (terrified, may flee)
  
Space4X:
  Planet → Empire:  Loyalty 60, Fear 40, Love 20 (compliant but not happy)
  Ship Captain → Player:  Respect 90, Trust 70, Loyalty 85 (solid officer)
```

**Universal Pattern:** Any entity can have disposition toward any force

---

### Aggregate Disposition (Collective Relationships)

**Collective → Force:**
```
AggregateCulture : IComponentData {
    Entity PrimaryInfluence;    // Dominant external force
    
    // Average dispositions of members
    byte CollectiveLoyalty;     // Overall allegiance
    byte CollectiveFear;        // Overall intimidation
    byte CollectiveLove;        // Overall affection
    
    // Stability
    float CulturalCohesion;     // 0-1: United vs divided
    float DissidentPercentage;  // % who disagree with collective
}
```

**Examples:**
```
Godgame Village Culture:
  → God: Loyalty 80, Fear 20, Love 70, Cohesion 0.9 (unified good village)
  
Space4X Planet Culture:
  → Empire: Loyalty 40, Fear 60, Love 10, Cohesion 0.5 (oppressed, divided)
  Dissidents: 30% (resistance forming)
```

---

## Alignment Shift Mechanics (Universal)

### Action → Alignment Change

**Pattern:**
```
Entity performs/witnesses Action
    ↓
Action has Alignment Weight
    ↓
IF action aligns with entity's current alignment:
    Reinforce (small shift in same direction)
ELSE:
    Conflict (shift toward action OR resist based on traits)
```

**Examples:**

**Godgame:**
```
God casts Heal on villager's friend (+10 good action)
Villager (current +30 good):
  - Aligns → shift +5 (reinforced goodness)
  
Villager (current -30 evil):
  - Conflicts → shift +2 (suspicious but affected)
```

**Space4X:**
```
Empire enforces harsh mining quotas (-15 collectivist action)
Planet (current -20 collectivist):
  - Aligns → shift -5 (reinforced authoritarianism)
  
Planet (current +40 individualist):
  - Conflicts → shift -8 (forced compliance, resentment grows)
```

**Universal Rule:** Aligned actions reinforce, conflicting actions create tension

---

### Aggregate Alignment Drift

**Pattern:**
```
Aggregate Current Alignment
    ↓
Target = Weighted influence of:
  - External force alignment
  - Member average alignment
  - Historical momentum
    ↓
Drift toward target at rate:
  DriftRate = f(CohesionStrength, InfluenceStrength, TimeConstant)
```

**Factors Affecting Drift Speed:**

**Fast Drift (Quick Cultural Change):**
- Low cohesion (divided, easily swayed)
- Strong external influence (dominant force)
- Recent founding (no tradition)
- High member spirituality/conformity

**Slow Drift (Resistant Culture):**
- High cohesion (unified, traditional)
- Weak external influence (distant/weak force)
- Ancient culture (strong tradition)
- Independent members (resist change)

**Examples:**

**Godgame:**
```
Evil village (-60) under new good god (+70)
  Cohesion: 0.4 (divided)
  Influence: 0.8 (god is powerful)
  Age: 50 ticks (young)
  
  → Drift: 5 per minute toward good
  → Reaches neutral (+0) in ~12 minutes
  → Reaches good (+50) in ~22 minutes
```

**Space4X:**
```
Individualist colony (+60) conquered by collectivist empire (-70)
  Cohesion: 0.9 (strongly unified culture)
  Influence: 0.6 (empire distant)
  Age: 1000 years (ancient tradition)
  
  → Drift: 0.5 per year toward empire
  → Takes decades to convert
  → May rebel before conversion complete
```

---

## Scale Levels (Fractal Pattern)

### Individual (Micro)
- Single entity (villager, ship, pop, character)
- Personal alignment values
- Direct experiences shape alignment

### Group (Meso)
- Collection (village, fleet, district, guild)
- Aggregate alignment (average or weighted)
- Influenced by members + external forces

### Civilization (Macro)
- Entire player/AI empire (god's domain, space empire, nation)
- Global alignment defines identity
- Shapes all subordinate groups and individuals

**Fractal Property:**
```
Civilization Alignment
    ↓ Influences (60%)
Group Alignment
    ↓ Influences (40%)
Individual Alignment
    ↑ Influences back (10-30% feedback)
```

**Examples:**

**Godgame:**
```
Micro:  Individual villager (+40 good)
Meso:   Village culture (+55 good average)
Macro:  God alignment (+70 good, shapes world)
```

**Space4X:**
```
Micro:  Individual pop/crew (+30 xenophilic)
Meso:   Planet culture (+50 xenophilic average)
Macro:  Empire ideology (+80 xenophilic, federation builder)
```

**Pattern Works at ANY Scale!**

---

## Disposition (Relationship Layer)

### Entity-to-Entity Relationships

**Universal Pattern:**
```
EntityDisposition : IComponentData {
    Entity Target;           // Who/what this is about
    
    // Relationship dimensions (choose what matters)
    byte Loyalty;            // Allegiance strength
    byte Affinity;           // Natural compatibility
    byte Respect;            // Earned regard
    byte Fear;               // Intimidation level
    byte Love;               // Affection/admiration
    byte Trust;              // Reliability belief
}
```

**Customizable:** Pick 2-5 dimensions that matter for your game

**Examples:**

**Godgame (God ↔ Villager):**
- Loyalty, Fear, Love (3 dimensions)
- Good god → High Love, Low Fear
- Evil god → High Fear, Low Love

**Space4X (Empire ↔ Faction):**
- Loyalty, Respect, Fear, Trust (4 dimensions)
- Allied faction → High Loyalty, High Trust
- Subjugated faction → High Fear, Low Loyalty

**City Builder (Citizen ↔ Mayor):**
- Approval, Trust (2 dimensions)
- Popular mayor → High Approval
- Corrupt mayor → Low Trust

---

### Aggregate Disposition (Collective Relationships)

**Pattern:**
```
AggregateDisposition = Weighted average of member dispositions

With modifiers:
  - Dominant voice (leaders/majority)
  - Recent events (war, aid, betrayal)
  - Historical relationship (ally, enemy, neutral)
```

**Examples:**

**Godgame Village → God:**
```
80 villagers total:
  - 60 high love (75 avg) = Good-aligned villagers
  - 15 neutral (50 avg)
  - 5 high fear (20 love avg) = Holdouts from evil era

Village Love toward God = 
  (60 × 75 + 15 × 50 + 5 × 20) / 80 = 66
  
Collective: "The village loves their god"
But: 5 villagers still fearful (minority, may convert or leave)
```

**Space4X Planet → Empire:**
```
10 billion pops:
  - 6B loyal (80 avg)
  - 3B neutral (50 avg)
  - 1B rebellious (10 avg)
  
Planet Loyalty = 
  (6B × 80 + 3B × 50 + 1B × 10) / 10B = 64
  
Collective: "Mostly loyal planet"
But: 1B rebellious (10% dissidents, watch for uprising)
```

---

## Applications by Game Type

### Godgame (Fantasy God Simulation)

**Entities:**
- Individual: Villagers
- Aggregate: Villages (or spatial clusters)
- Force: Player God, Rival Gods

**Alignment Axes:**
- Good ↔ Evil (primary)
- <OPTIONAL: Order ↔ Chaos, Interventionist ↔ Hands-off>

**Disposition Dimensions:**
- Villager → God: Love, Fear, Loyalty

**Use Cases:**
- God actions shift god alignment
- Villagers react based on alignment match
- Village culture emerges from collective
- Visual style reflects alignment

---

### Space 4X RTS (Strategy Game)

**Entities:**
- Individual: Pops (populations), Ship crews, Admirals, Governors
- Aggregate: Planets, Fleets, Sectors, Factions
- Force: Player Empire, AI Empires, Fallen Empires, Galactic Council

**Alignment Axes:**
- Xenophilic ↔ Xenophobic (attitude toward aliens)
- Militarist ↔ Pacifist (war vs peace)
- Authoritarian ↔ Egalitarian (government style)
- Materialist ↔ Spiritualist (science vs faith)
- <OPTIONAL: Individualist ↔ Collectivist, Expansionist ↔ Isolationist>

**Disposition Dimensions:**
- Pop → Empire: Loyalty, Approval
- Planet → Empire: Loyalty, Stability
- Empire → Empire: Opinion, Trust, Fear
- Faction → Player: Reputation, Hostility

**Use Cases:**
```
Example: Xenophilic Empire conquers Xenophobic planet

Empire Alignment: Xenophilic +80
Planet Culture: Xenophobic -70 (pre-conquest)
    ↓
Conflict! Planet doesn't want alien overlords.
    ↓
Planet Loyalty: 20 (low, forced compliance)
Planet Stability: 40 (unrest brewing)
    ↓
Over time:
  - Empire propaganda → Culture drifts toward xenophilic
  - Dissidents resist → Some pops flee or rebel
  - Generational shift → New pops born under empire (conform easier)
    ↓
After 50 years:
  Planet Culture: -20 (shifted but not fully converted)
  Loyalty: 60 (improved but fragile)
  Dissidents: 15% (persistent resistance)
```

---

### City Builder (Urban Simulation)

**Entities:**
- Individual: Citizens
- Aggregate: Neighborhoods, Districts
- Force: Player (Mayor), City Council, External Cities

**Alignment Axes:**
- Progressive ↔ Conservative
- Wealthy ↔ Working Class
- <OPTIONAL: Industrial ↔ Residential preference>

**Disposition:**
- Citizen → Mayor: Approval, Trust
- District → Policy: Support, Opposition

**Use Cases:**
- Progressive district opposes conservative mayor
- Wealthy neighborhood wants different services than working class
- Policy decisions shift district cultures over time

---

## Conflict & Mismatch Dynamics

**<KEY DESIGN OPPORTUNITY: Mismatches create interesting gameplay>**

### Alignment Conflict Pattern

```
IF: Entity Alignment far from Force Alignment (|difference| > threshold)
THEN: Tension mechanics trigger
```

**Tension Expressions:**

**Low-Stakes (Dissatisfaction):**
- Reduced productivity
- Lower morale/approval
- Visual: Grumbling, protests
- <RECOVERABLE: Player can improve relationship>

**Medium-Stakes (Resistance):**
- Active non-compliance
- Migration (leave for aligned force)
- Visual: Demonstrations, sabotage
- <REQUIRES: Player action to resolve>

**High-Stakes (Rebellion):**
- Open revolt
- Form independent faction
- Armed conflict
- <CRITICAL: Major threat, force response>

---

### Godgame Examples

**Good God (+70) meets Evil Village (-60):**
```
Tension Level: HIGH (130 point gap)

Immediate:
  - Villagers suspicious, hide from god hand
  - Low prayer generation (fear-based)
  - Buildings remain dark (culture lag)
  
Options:
  1. Force conversion (intimidation, miracles)
     → Quick but increases fear
  2. Gentle conversion (heal, bless, patience)
     → Slow but builds love
  3. Accept evil culture, drift evil yourself
     → Alignment shifts to match village
```

**Evil God (-70) loses Good Villagers:**
```
Good-aligned villagers (loyalty < 30):
  - Flee village (migration)
  - Remaining villagers: Evil-aligned or fearful
  - Village self-selects toward evil culture
  - Player either: Stops them (force) or lets them go (population loss)
```

---

### Space 4X Examples

**Pacifist Empire (+90) conquers Militarist Planet (-80):**
```
Tension: EXTREME (170 point gap)

Planet reactions:
  - Military pops: Low loyalty (want war, empire is weak)
  - Civilian pops: Mixed (appreciate peace but culture clash)
  - Garrison required to prevent uprising
  
Empire options:
  1. Re-education (propaganda, generation shift) - Slow, ethical
  2. Suppression (military occupation) - Fast, costly, shifts empire toward militarist
  3. Grant independence (release planet) - Lose asset but maintain pacifist values
  
Outcome over time:
  - Some pops migrate to militarist empires
  - Young generation adapts to pacifism
  - Elders remain militarist (minority)
  - Planet culture drifts to +20 after 100 years (still not fully converted)
```

**Xenophobic Faction meets Xenophilic Player (Diplomacy):**
```
Player: Xenophilic +80
Faction: Xenophobic -90

Disposition: Fear 70, Trust 10, Hostility 90

Faction refuses:
  - Open borders (too trusting)
  - Migration treaties (cultural threat)
  - Research agreements (share with aliens?!)
  
Player options:
  1. Cultural exchange (shift faction toward xenophilic over time)
  2. Ignore faction (neutral relations)
  3. Force compliance (war)
  
Design: Alignment differences drive diplomatic constraints!
```

---

## Generalized Component Schema

### For Individual Entities

```csharp
// Universal alignment component (configure axes per game)
EntityAlignment : IComponentData {
    // Primary axis (required)
    float PrimaryAxis;           // -100 to +100
    
    // Secondary axis (optional)
    float SecondaryAxis;         // -100 to +100
    
    // Tertiary axis (optional)
    float TertiaryAxis;          // -100 to +100
    
    // Meta
    float AlignmentStrength;     // How strongly held (0-1)
    uint LastShiftTick;          // When last changed
}

// Universal disposition component
EntityDisposition : IComponentData {
    Entity TargetEntity;         // Who this is about
    
    // Choose 2-5 dimensions that matter for your game
    byte DimensionA;             // 0-100 (e.g., Loyalty)
    byte DimensionB;             // 0-100 (e.g., Fear)
    byte DimensionC;             // 0-100 (e.g., Love)
    byte DimensionD;             // 0-100 (e.g., Trust)
    byte DimensionE;             // 0-100 (e.g., Respect)
}

// Optional: Memory/history
EntityMemory : IBufferElementData {
    uint Tick;                   // When event happened
    FixedString64Bytes EventId;  // What happened
    float AlignmentImpact;       // How much it shifted alignment
    Entity Causer;               // Who caused it (optional)
}
```

---

### For Aggregate Entities

```csharp
// Universal collective alignment
AggregateAlignment : IComponentData {
    // Collective values (aggregate of members)
    float PrimaryAxis;
    float SecondaryAxis;
    float TertiaryAxis;
    
    // Meta
    float CulturalCohesion;      // 0-1: United vs divided
    float DriftRate;             // How fast culture changes
    Entity DominantInfluence;    // Who shapes this culture most
}

// Membership tracking
AggregateMember : IBufferElementData {
    Entity MemberEntity;
    float InfluenceWeight;       // How much this member affects aggregate
    uint JoinedTick;             // When they joined
}

// Collective disposition toward external forces
AggregateDisposition : IComponentData {
    Entity TargetForce;
    
    // Average member dispositions
    byte CollectiveDimensionA;
    byte CollectiveDimensionB;
    byte CollectiveDimensionC;
    
    // Dissent tracking
    float DissidentPercentage;   // % who disagree with collective
    byte StabilityLevel;         // 0-100: Chaotic to stable
}
```

---

### For External Forces

```csharp
// Force's alignment (player, AI, faction)
ForceAlignment : IComponentData {
    // Same axes as entities
    float PrimaryAxis;
    float SecondaryAxis;
    float TertiaryAxis;
    
    // Influence properties
    float InfluenceRadius;       // How far influence reaches
    float InfluenceStrength;     // How strong (0-1)
}

// Optional: Force's reputation with others
ForceReputation : IComponentData {
    byte GlobalReputation;       // Overall standing
    FixedString64Bytes ReputationLabel;  // "Benevolent", "Tyrant", "Neutral"
}
```

---

## Migration & Dissent (Universal Patterns)

### Migration Pattern

```
IF: Entity.Loyalty to CurrentAggregate < MigrationThreshold
AND: Entity.Fear of CurrentForce < FleeThreshold (not too scared to run)
AND: Alternative exists (other aggregate with better alignment match)
THEN: Entity migrates

Destination selection:
  - Find aggregate with closest alignment match
  - Weighted by: Distance, capacity, acceptance policy
```

**Examples:**

**Godgame:**
- Villager (good +60) under evil god (-70) → Flees to neutral village
- Villager (evil -50) attracted to evil god village → Migrates in

**Space4X:**
- Pacifist pop under militarist empire → Emigrates to pacifist neighbor
- Xenophobic pop cluster → Forms separatist movement, seeks independence

---

### Rebellion Pattern

```
IF: AggregateDisposition.CollectiveLoyalty < RebellionThreshold
AND: AggregateDisposition.Fear < SuppressionThreshold (not too scared)
AND: DissidentPercentage > CriticalMass (enough rebels)
THEN: Rebellion event

Rebellion outcomes:
  - Force suppresses (increases fear, decreases love, maintains control)
  - Aggregate wins independence (forms new force)
  - Compromise (policy changes, alignment shifts)
```

**Examples:**

**Godgame:**
```
Village:
  Loyalty to god: 15 (very low)
  Fear: 25 (not scared enough)
  Dissidents: 60% (majority unhappy)
  
  → Rebellion: Villagers refuse to work, destroy altars
  
  God options:
    - Cast miracles (intimidate → raise fear → suppress)
    - Appease (heal, bless → raise love → restore loyalty)
    - Abandon village (let them go neutral)
```

**Space4X:**
```
Planet:
  Loyalty to empire: 10 (hostile)
  Fear: 20 (not suppressed)
  Dissidents: 70% (resistance strong)
  
  → Rebellion: Planet declares independence, forms new faction
  
  Empire options:
    - Military reconquest (bombardment, invasion)
    - Blockade (starve into submission)
    - Negotiate independence (lose planet, gain peace)
```

---

## Visual & Behavioral Expression

**<UNIVERSAL PATTERN: Alignment → Aesthetics>**

### Visual Mapping

**For Individual Entities:**
```
Alignment Value → Visual Variant

Example (Single Good/Evil Axis):
  -100 to -75: Darkest variant (obsidian, spikes, red)
  -75 to -25:  Dark variant (iron, sharp edges, purple)
  -25 to +25:  Neutral variant (natural, practical, gray)
  +25 to +75:  Bright variant (clean, welcoming, blue)
  +75 to +100: Brightest variant (radiant, gold, white)
```

**Godgame Application:**
- Villager clothing color: Dark → Light based on disposition
- Buildings: Material swaps based on village culture

**Space4X Application:**
- Ship designs: Aggressive → Defensive based on militarist axis
- Planet cities: Industrial → Garden based on materialist axis

---

### Behavioral Mapping

**Alignment → Behavior Modifiers:**

```
High Loyalty → Work rate +10%, flee chance -90%
Low Loyalty → Work rate -20%, flee chance +50%

High Fear → Obedience +30%, happiness -20%
Low Fear → Obedience -10%, happiness +10%

High Love → Volunteer rate +40%, prayer/tribute +30%
Low Love → Volunteer -30%, prayer/tribute -50%
```

**Universal:** Disposition values modify base behaviors

---

## System Queries (Gameplay Use)

**<DESIGN: What can gameplay systems ask?>**

### Individual Queries
```
GetEntityAlignment(entity) → Alignment values
GetDispositionToward(entity, target) → Disposition values
WillEntityFlee(entity, threat) → Bool (based on fear, loyalty)
IsEntityCompatible(entity, aggregate) → Bool (alignment match)
```

### Aggregate Queries
```
GetCultureAlignment(aggregate) → Alignment values
GetCollectiveDisposition(aggregate, force) → Disposition values
GetDissidentCount(aggregate) → Count of misaligned members
GetCulturalCohesion(aggregate) → Unity measure (0-1)
IsAggregateStable(aggregate) → Bool (rebellion risk)
```

### Force Queries
```
GetForceReputation(force) → Global reputation value
GetInfluencedEntities(force, radius) → List of affected entities
GetLoyalEntities(force) → Entities with high loyalty
GetRebellionRisk(force) → Aggregate stability assessment
```

---

## Configuration Per Game

**Design File: AlignmentConfig (per game)**

```
Game: Godgame
  Axes:
    Primary: Good (-100) to Evil (+100)
  
  Disposition Dimensions:
    - Love (0-100)
    - Fear (0-100)
    - Loyalty (0-100)
  
  Entity Types:
    - Villager (individual)
    - Village (aggregate, optional)
    - God (force)
  
  Visual Tiers: 5 (pure evil, evil, neutral, good, pure good)
  Drift Rate: Medium (villages adapt in ~10 minutes)
  Migration: Enabled (villagers can flee)
  Rebellion: Enabled (threshold: loyalty < 20, dissidents > 50%)
```

```
Game: Space4X
  Axes:
    Primary: Xenophobic (-100) to Xenophilic (+100)
    Secondary: Pacifist (-100) to Militarist (+100)
    Tertiary: Authoritarian (-100) to Egalitarian (+100)
  
  Disposition Dimensions:
    - Loyalty (0-100)
    - Approval (0-100)
    - Trust (0-100)
    - Fear (0-100)
  
  Entity Types:
    - Pop (individual)
    - Planet (aggregate)
    - Sector (mega-aggregate)
    - Empire (force)
  
  Visual Tiers: 3 per axis (27 combinations total)
  Drift Rate: Slow (planets adapt over decades)
  Migration: Enabled (pops emigrate to aligned empires)
  Rebellion: Enabled (threshold: stability < 30, dissidents > 40%)
```

---

## Open Questions (Framework Level)

### Core Design
1. **How many axes?** Single (simple) vs multi-dimensional (complex)
2. **Drift mechanics:** Linear, exponential, or stepped?
3. **Visual tiers:** How many variants per axis? (3? 5? 7?)
4. **Memory depth:** Track last 10 events? 100? Aggregate only?

### Gameplay Integration
5. **Migration rules:** Always possible or gated?
6. **Rebellion triggers:** Automatic or player-influenced?
7. **Conversion speed:** Days, months, years, generations?
8. **Force influence:** Radius-based, power-based, or policy-based?

### Performance
9. **Update frequency:** Per frame, per second, per tick?
10. **Memory budget:** How much history per entity?
11. **Aggregate calculation:** Cached or computed on-demand?

---

## Implementation Implications (When Ready)

**Truth Sources Needed:**
```
// Core alignment (1-3 float values per axis)
EntityAlignment : IComponentData

// Relationships (2-5 byte values per relationship)
EntityDisposition : IComponentData

// Aggregation (for collectives)
AggregateAlignment : IComponentData
AggregateMember : IBufferElementData

// Optional depth
EntityMemory : IBufferElementData
EntityPersonality : IComponentData (traits that affect disposition)
```

**Systems Needed:**
```
AlignmentShiftSystem     // Apply action weights to alignments
DispositionUpdateSystem  // Update relationships based on actions/alignment
CultureDriftSystem       // Aggregate alignment drift toward influences
MigrationSystem          // Handle entity movement between aggregates
RebellionSystem          // Detect and trigger revolt events
VisualStyleSystem        // Apply visuals based on alignment
```

**Performance:**
- Alignment shifts: On action (event-driven)
- Disposition updates: Per tick or per action
- Culture drift: Low frequency (1-10 Hz)
- Visual updates: On alignment tier change only

---

## Reusability Checklist

**Framework is reusable when:**
- [ ] Entity-agnostic (works for any entity type)
- [ ] Configurable axes (define what matters per game)
- [ ] Configurable dimensions (choose relationship metrics)
- [ ] Scales from individual → aggregate → civilization
- [ ] Handles conflicts and mismatches
- [ ] Visual mapping abstracted
- [ ] Behavior modifiers templated
- [ ] Migration/rebellion patterns generalized

**Current Status:** ✅ All design patterns are generalizable

---

## Next Steps

1. **Per-Game:** Define specific axes in game-specific concept docs
   - Godgame: `Alignment_System.md` (good/evil)
   - Space4X: `Empire_Ideology.md` (multiple axes)

2. **Per-Game:** Define entity types and scales
   - Godgame: Villager → Village → God
   - Space4X: Pop → Planet → Sector → Empire

3. **Per-Game:** Choose disposition dimensions
   - Godgame: Love, Fear, Loyalty (3D)
   - Space4X: Loyalty, Approval, Trust, Fear (4D)

4. **Shared:** Use this framework as template for implementation

---

**For Designers:** Use this framework to design alignment for ANY game - just configure axes and dimensions  
**For Implementers:** (Future) Single codebase can support multiple games with config-driven alignment  
**For Product:** Reusable system amortizes development cost across multiple projects

---

**Last Updated:** 2025-10-31  
**Applicability:** Godgame ✓, Space4X ✓, City Builder ✓, Any game with social dynamics ✓



