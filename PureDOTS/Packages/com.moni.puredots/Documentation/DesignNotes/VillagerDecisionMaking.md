# Villager Decision-Making Architecture

**Status**: Design Specification
**Last Updated**: 2025-11-28
**Related Systems**: VillagerComponents, UtilityComponents, SensorComponents, Skills, Knowledge, Jobs

---

## Overview

Villagers in PureDOTS use a **layered decision-making architecture** combining utility-based AI with context-sensitive modifiers. Every decision is scored based on:

1. **Internal State** (needs, morale, health, energy)
2. **Capabilities** (attributes, skills, knowledge)
3. **Environmental Context** (threats, opportunities, time, weather)
4. **Social Factors** (relationships, culture, beliefs, group dynamics)
5. **Personality Traits** (risk tolerance, ambition, curiosity, loyalty)

### Architecture Layers

```
┌─────────────────────────────────────────────────┐
│  DECISION LAYER                                 │
│  - Final action selection                      │
│  - Hysteresis & commitment                     │
└──────────────────┬──────────────────────────────┘
                   │
┌──────────────────▼──────────────────────────────┐
│  SCORING LAYER                                  │
│  - Utility curve evaluation                    │
│  - Multi-factor scoring                        │
│  - Priority modifiers                          │
└──────────────────┬──────────────────────────────┘
                   │
┌──────────────────▼──────────────────────────────┐
│  PERCEPTION LAYER                               │
│  - Sensor input (DetectedEntity buffer)        │
│  - Memory (recent events, known locations)     │
│  - Beliefs & knowledge state                   │
└──────────────────┬──────────────────────────────┘
                   │
┌──────────────────▼──────────────────────────────┐
│  STATE LAYER                                    │
│  - VillagerNeeds (food, energy, morale)        │
│  - VillagerAttributes (stats)                  │
│  - SkillSet & VillagerKnowledge                │
│  - VillagerBelief & social state               │
└─────────────────────────────────────────────────┘
```

---

## Stat Influences on Decisions

### Primary Attributes (0-10 range)

Each primary attribute creates a **bias** toward certain action categories:

#### **Physique** (Physical Power)
- **High Physique (7-10)**: Prefer physical labor, combat, intimidation
- **Low Physique (0-3)**: Avoid combat, prefer support roles
- **Influences**:
  - Gather (physical resources): +0.1 score per point above 5
  - Combat actions: +0.15 score per point above 5
  - Flee threshold: Lowered by 5% per point above 5
  - Fatigue rate: -2% per point

**Formula**:
```csharp
var physiqueBias = (physique - 5f) * 0.1f;
score *= (1f + physiqueBias);
```

#### **Finesse** (Precision & Coordination)
- **High Finesse (7-10)**: Prefer crafting, ranged combat, stealth
- **Low Finesse (0-3)**: Clumsy, avoid delicate tasks
- **Influences**:
  - Craft actions: +0.12 score per point above 5
  - Quality variance: -3% per point (higher consistency)
  - Critical crafting success: +2% per point
  - Ranged accuracy: +5% per point

**Formula**:
```csharp
var finesseBias = (finesse - 5f) * 0.12f;
var qualityVariance = 0.2f - (finesse * 0.03f);
```

#### **Willpower** (Mental Fortitude)
- **High Willpower (7-10)**: Resist fear, persist through difficulty, resist manipulation
- **Low Willpower (0-3)**: Easily intimidated, quick to flee
- **Influences**:
  - Flee threshold: Raised by 3% per point
  - Morale decay: -4% per point
  - Resist social pressure: +8% per point
  - Learning complex knowledge: +6% per point

**Formula**:
```csharp
var willpowerResistance = willpower * 0.08f;
var moraleDecayMultiplier = 1f - (willpower * 0.04f);
```

### Derived Attributes (0-200 range)

Derived attributes provide **granular modifiers** for specific actions:

#### **Strength** (from Physique)
- Carry capacity: +5 units per 10 points
- Melee damage: +1% per 5 points
- Building speed: +0.5% per 10 points
- Endurance: +2% per 10 points

#### **Agility** (from Finesse)
- Movement speed: +1% per 8 points
- Dodge chance: +0.5% per 10 points
- Attack speed: +1% per 12 points
- Gather speed (delicate resources): +2% per 10 points

#### **Intelligence** (from Willpower)
- Crafting quality: +1% per 10 points
- Knowledge learning rate: +2% per 10 points
- Problem-solving (stuck situations): +5% per 20 points
- Complex job execution: +1.5% per 10 points

#### **Wisdom** (from Willpower)
- Threat assessment accuracy: +3% per 15 points
- Resource quality detection: +2% per 10 points
- Social reading (detect lies, intentions): +4% per 20 points
- Long-term planning weight: +0.02 per 10 points

---

## Needs Influence on Utility Scores

Each need creates **pressure** that modifies action scores. As needs drop, survival actions gain priority.

### Hunger (0-100, higher = more fed)

**Thresholds**:
- **90-100**: Well-fed, no modifier
- **70-89**: Slight hunger, +0.1 to eating actions
- **50-69**: Hungry, +0.3 to eating, -0.1 to all others
- **30-49**: Very hungry, +0.8 to eating, -0.3 to all others
- **0-29**: Starving, +2.0 to eating, -0.7 to all others, flee if no food

**Utility Curve** (Exponential):
```csharp
// Eating action score
var hungerPressure = math.exp(-(hunger / 50f)) - 0.01f;
var eatScore = hungerPressure * 1.5f;

// Other action penalty
var actionPenalty = hunger < 70f ? math.lerp(1f, 0.3f, (70f - hunger) / 70f) : 1f;
score *= actionPenalty;
```

**Secondary Effects**:
- Work productivity: `math.saturate(hunger / 70f)` (70% hunger = 100% productivity)
- Morale influence: -1 morale per 10 hunger below 50
- Aggression: +5% per 10 hunger below 30 (desperate for food)

### Energy (0-100, higher = more rested)

**Thresholds**:
- **80-100**: Energetic, +5% productivity
- **50-79**: Normal, no modifier
- **20-49**: Tired, -15% productivity, +0.4 to rest actions
- **0-19**: Exhausted, -40% productivity, +1.5 to rest, flee more likely

**Utility Curve** (Logistic):
```csharp
// Rest action score
var energyPressure = 1f / (1f + math.exp(0.1f * (energy - 30f)));
var restScore = energyPressure * 1.2f;

// Movement speed modifier
var speedMultiplier = energy < 20f ? 0.5f : math.lerp(0.85f, 1f, energy / 80f);
```

**Secondary Effects**:
- Gather rate: `math.saturate(energy / 50f)` multiplier
- Combat effectiveness: -2% per 5 points below 50
- Decision interval: +10% reconsider delay when energy < 30 (slower thinking)

### Morale (0-100, higher = happier)

**Thresholds**:
- **80-100**: Happy, +10% productivity, +0.3 to social actions
- **50-79**: Content, normal behavior
- **30-49**: Unhappy, -10% productivity, +0.2 to idle, -0.2 to work
- **0-29**: Miserable, -30% productivity, high flee/abandon chance

**Utility Curve** (Quadratic):
```csharp
// Morale affects willingness to work
var moraleMultiplier = 0.7f + (morale / 100f) * 0.6f; // Range: 0.7 to 1.3
workScore *= moraleMultiplier;

// Social action bonus when happy
var socialBonus = morale > 70f ? (morale - 70f) / 100f : 0f;
socialScore += socialBonus;
```

**Secondary Effects**:
- Rebellion chance: +2% per 10 morale below 30
- Cooperation: +5% per 10 morale above 60
- Innovation/creativity: +3% per 10 morale above 70
- Desertion risk: Exponential below 20 morale

### Health (0-100, higher = healthier)

**Thresholds**:
- **80-100**: Healthy, no penalties
- **50-79**: Injured, -5% movement, +0.1 to rest
- **25-49**: Wounded, -20% all actions, +0.5 to rest, flee preferred
- **0-24**: Critical, -50% actions, forced rest/flee only

**Utility Curve** (Step + Linear):
```csharp
// Flee threshold based on health
var fleeThreshold = health < 50f ? 50f + (50f - health) * 0.5f : 25f;
var healthFleeBias = health < fleeThreshold ? 2f - (health / 50f) : 0f;

// Action effectiveness
var healthPenalty = health < 80f ? math.lerp(0.5f, 1f, health / 80f) : 1f;
score *= healthPenalty;
```

**Secondary Effects**:
- Death at health <= 0
- Infection chance below 30 health: +2% per tick
- Bleeding/DoT effects accelerate below 50 health
- Recovery rate: Flat +1/sec when health > 50 and hunger > 50

---

## Knowledge & Skill Influence

### Skill Level Impact (0-100 scale per skill)

Skills modify **efficiency and quality** rather than desire to perform actions:

**Harvest Skill (HarvestBotany)**:
- Gather rate: `1f + (skillLevel / 100f)` (up to 2x at max skill)
- Resource quality: -3% variance per 10 skill levels
- Spot rare resources: +2% per 10 levels
- XP gain: Higher skill = slower XP (diminishing returns)

**Processing Skill (Crafting)**:
- Craft speed: `1f + (skillLevel / 100f)`
- Item quality: +1% per 5 levels
- Material efficiency: +1% per 10 levels (less waste)
- Recipe unlock: Certain recipes require minimum skill

**Combat Skills**:
- Damage: +1% per 5 levels
- Accuracy: +2% per 10 levels
- Defense: +1% per 8 levels
- Tactical options: Unlock special moves at skill breakpoints (25, 50, 75)

**Formula for Learning Rate** (diminishing returns):
```csharp
var xpMultiplier = math.max(0.25f, 1.5f - (skillLevel / 100f));
var xpGain = baseXp * xpMultiplier * ageLearningScalar * mindScalar;
```

### Knowledge (Lessons) Impact

Knowledge provides **strategic bonuses** and unlocks new behaviors:

**Lesson Progress** (0-1 normalized):
- **0.0-0.3**: Novice, +5% related actions, awareness of concept
- **0.3-0.6**: Apprentice, +15% related actions, basic application
- **0.6-0.9**: Journeyman, +30% related actions, advanced techniques
- **0.9-1.0**: Master, +50% related actions, innovation possible

**Harvest Knowledge Example** (e.g., "Botany Fundamentals"):
```csharp
var harvestModifiers = KnowledgeLessonEffectUtility.EvaluateHarvestModifiers(
    ref lessonBlob,
    knowledge.Lessons,
    resourceId,
    qualityTier);

// Returns: YieldMultiplier, HarvestTimeMultiplier, QualityBonus
var actualYield = baseYield * harvestModifiers.YieldMultiplier;
```

**Knowledge Flags** (unlocks):
- `PrecisionHarvesting`: Can target specific quality tiers
- `QualityPreservation`: +20% quality retention during gathering
- `ResourceRecognition`: Detect resource quality before harvesting
- `EfficientProcessing`: -15% material waste in crafting

**Age-Based Learning** (from JobBehaviors.cs):
```csharp
// Children learn fastest, elders slowest
if (ageYears <= 12f)
    return math.lerp(1.75f, 1.3f, ageYears / 12f);  // 175%-130%
else if (ageYears <= 25f)
    return math.lerp(1.3f, 1f, (ageYears - 12f) / 13f);  // 130%-100%
else if (ageYears <= 45f)
    return 1f;  // Prime learning age
else if (ageYears <= 70f)
    return math.lerp(1f, 0.85f, (ageYears - 45f) / 25f);  // Decline
else
    return 0.7f;  // Elderly
```

**Mind Scalar** (Intelligence + Wisdom):
```csharp
var combined = (intelligence + wisdom) * 0.5f;
var mindScalar = math.clamp(0.6f + combined / 200f, 0.6f, 1.8f);
// Range: 60% (dull) to 180% (genius)
```

---

## Decision Flow Examples

### Example 1: Hungry Villager Choosing Next Action

**State**:
- Hunger: 25 (starving)
- Energy: 60 (normal)
- Morale: 40 (unhappy)
- Physique: 6, Finesse: 4, Willpower: 5
- Harvest Skill: 35

**Step 1: Perception**
- Sensor detects: Food resource at 30m, Enemy at 50m
- Memory: Knows food storage location
- Current job: Gathering wood

**Step 2: Candidate Actions**
Generate action candidates with base scores:

| Action | Base Score | Reasoning |
|--------|-----------|-----------|
| Eat | 0.3 | Basic survival action |
| Gather Food | 0.5 | Addresses hunger |
| Continue Wood | 0.4 | Current job |
| Flee | 0.2 | Enemy present |
| Rest | 0.3 | Energy decent |
| Socialize | 0.2 | Social creature |

**Step 3: Apply Need Modifiers**

**Hunger Pressure** (25/100 = starving):
```csharp
var hungerPressure = math.exp(-(25f / 50f)) - 0.01f = 1.604
var eatBonus = hungerPressure * 1.5f = 2.406
```

**Energy Modifier** (60/100 = normal):
```csharp
var energyPenalty = 1f; // No penalty at 60 energy
```

**Morale Modifier** (40/100 = unhappy):
```csharp
var moraleMultiplier = 0.7f + (40f / 100f) * 0.6f = 0.94
```

| Action | After Needs | Calculation |
|--------|-------------|-------------|
| **Eat** | **2.706** | 0.3 + 2.406 (hunger) |
| Gather Food | 1.170 | (0.5 + 2.406) * 0.94 * 0.4 (energy penalty on work) |
| Continue Wood | 0.376 | 0.4 * 0.94 |
| Flee | 0.200 | 0.2 (no threats close) |
| Rest | 0.300 | 0.3 (energy fine) |
| Socialize | 0.188 | 0.2 * 0.94 |

**Step 4: Apply Stat Modifiers**

**Eat Action** (Physique 6 helps with eating efficiency):
```csharp
var physiqueBias = (6f - 5f) * 0.05f = 0.05f
eatScore = 2.706 * (1f + 0.05f) = 2.841
```

**Gather Food** (Harvest skill 35, Physique 6):
```csharp
var skillBonus = 35f / 100f = 0.35f
var physiqueBias = (6f - 5f) * 0.1f = 0.1f
gatherScore = 1.170 * (1f + 0.35f + 0.1f) = 1.697
```

**Final Scores**:
| Action | Final Score |
|--------|-------------|
| **Eat** | **2.841** |
| Gather Food | 1.697 |
| Continue Wood | 0.376 |
| Flee | 0.200 |
| Rest | 0.300 |
| Socialize | 0.188 |

**Decision**: **EAT** (score 2.841 >> switch threshold 0.2)

**Step 5: Execute**
- Change state: `CurrentAction = ActionType.Eat`
- Find nearest food source (from sensors or memory)
- Navigate to food
- Consume until hunger > 70 (threshold * 0.5 multiplier)

---

### Example 2: Combat vs Flee Decision

**State**:
- Health: 35 (wounded)
- Energy: 45 (tired)
- Morale: 30 (unhappy)
- Physique: 8, Finesse: 5, Willpower: 3 (low will)
- Combat Skill: 20
- Detected: Enemy (ThreatLevel: 120, Distance: 15m, Relationship: -100)

**Step 1: Threat Assessment**

**Base Flee Threshold** (from config): 25 health

**Willpower Adjustment**:
```csharp
var willpowerMod = 3f * 0.03f = 0.09f  // Raises threshold by 9%
var adjustedFleeThreshold = 25f * (1f + 0.09f) = 27.25f
```

**Current Health**: 35 > 27.25 → Not forced to flee by health

**Threat Level**: 120 (enemy is strong)
**Own Combat Power**:
```csharp
var combatPower = physique * 10f + combatSkill = 8 * 10 + 20 = 100
var powerRatio = 100f / 120f = 0.833  // Outmatched
```

**Step 2: Score Actions**

**Fight**:
```csharp
var baseFightScore = 0.6f;
var powerMod = powerRatio = 0.833f;
var healthPenalty = health < 50f ? 0.7f + (health / 100f) = 1.05f; // Wounded penalty
var moraleMod = 0.7f + (30f / 100f) * 0.6f = 0.88f;
var fightScore = baseFightScore * powerMod * healthPenalty * moraleMod = 0.459
```

**Flee**:
```csharp
var baseFleeScore = 0.4f;
var threatBonus = (enemy.ThreatLevel - combatPower) / 100f = 0.2f;
var healthBonus = health < 50f ? (50f - health) / 50f = 0.3f;
var willpowerPenalty = 1f - (willpower / 20f) = 0.85f; // Low will = flee easier
var fleeScore = (baseFleeScore + threatBonus + healthBonus) * willpowerPenalty = 0.765
```

**Final Scores**:
| Action | Score | Reasoning |
|--------|-------|-----------|
| **Flee** | **0.765** | Outmatched, wounded, low willpower |
| Fight | 0.459 | Too risky |

**Decision**: **FLEE**

**Execution**:
- Set state: `CurrentAction = ActionType.Flee`
- Direction: Opposite of enemy position
- Speed: Base * FleeSpeedMultiplier (1.5x from config)
- Duration: FleeDuration (5 seconds) or until enemy not detected

---

### Example 3: Job Selection (Morning Routine)

**State**:
- Time: Dawn (6 AM game time)
- Hunger: 85 (well-fed from breakfast)
- Energy: 95 (full rest)
- Morale: 70 (content)
- Physique: 5, Finesse: 7, Willpower: 6
- Harvest Skill: 60, Processing Skill: 45

**Step 1: Job Candidates**

Village needs:
- Wood: 60% full (low priority)
- Food: 40% full (medium priority)
- Crafted Tools: 20% full (high priority)

**Available Jobs** (from job queue):
| Job | Priority | Distance | Reserved Units |
|-----|----------|----------|----------------|
| Gather Wood | 0.3 | 25m | 50 |
| Gather Berries | 0.6 | 40m | 30 |
| Craft Tools | 0.8 | 15m | - |
| Build Hut | 0.5 | 60m | - |

**Step 2: Score Each Job**

**Gather Wood**:
```csharp
var baseScore = 0.4f;
var priorityMod = 0.3f; // Village priority
var distancePenalty = math.max(0.5f, 1f - (25f / 100f)) = 0.75f;
var skillBonus = harvestSkill / 100f = 0.6f;
var statBonus = (physique - 5f) * 0.1f = 0f;
var score = baseScore * (1f + priorityMod + skillBonus + statBonus) * distancePenalty = 0.570
```

**Gather Berries**:
```csharp
var baseScore = 0.5f;
var priorityMod = 0.6f;
var distancePenalty = 1f - (40f / 100f) = 0.6f;
var skillBonus = 0.6f;
var score = baseScore * (1f + 0.6f + 0.6f) * 0.6f = 0.660
```

**Craft Tools**:
```csharp
var baseScore = 0.6f;
var priorityMod = 0.8f;
var distancePenalty = 1f - (15f / 100f) = 0.85f;
var skillBonus = processingSkill / 100f = 0.45f;
var statBonus = (finesse - 5f) * 0.12f = 0.24f; // Finesse helps crafting
var score = baseScore * (1f + 0.8f + 0.45f + 0.24f) * 0.85f = 1.272
```

**Build Hut**:
```csharp
var baseScore = 0.5f;
var priorityMod = 0.5f;
var distancePenalty = 1f - (60f / 100f) = 0.4f;
var skillBonus = 0f; // No build skill
var score = baseScore * (1f + 0.5f) * 0.4f = 0.300
```

**Final Selection**:
| Job | Score | Selected |
|-----|-------|----------|
| **Craft Tools** | **1.272** | **YES** |
| Gather Berries | 0.660 | No |
| Gather Wood | 0.570 | No |
| Build Hut | 0.300 | No |

**Execution**:
- Accept job ticket for "Craft Tools"
- Navigate to crafting station (15m)
- Enter crafting phase
- Produce tools until materials exhausted or job quota met

---

## Personality Trait Modifiers

Personality traits are **optional components** that skew decision weights to create distinct character archetypes.

### Trait Component Structure

```csharp
public struct PersonalityTraits : IComponentData
{
    /// <summary>
    /// Risk tolerance (0 = cautious, 1 = normal, 2 = reckless).
    /// </summary>
    public float RiskTolerance;

    /// <summary>
    /// Ambition (0 = lazy, 1 = normal, 2 = driven).
    /// </summary>
    public float Ambition;

    /// <summary>
    /// Curiosity (0 = traditional, 1 = normal, 2 = explorer).
    /// </summary>
    public float Curiosity;

    /// <summary>
    /// Loyalty (0 = selfish, 1 = normal, 2 = devoted).
    /// </summary>
    public float Loyalty;

    /// <summary>
    /// Sociability (0 = hermit, 1 = normal, 2 = social butterfly).
    /// </summary>
    public float Sociability;

    /// <summary>
    /// Greed (0 = generous, 1 = normal, 2 = greedy).
    /// </summary>
    public float Greed;

    /// <summary>
    /// Creates balanced/normal personality.
    /// </summary>
    public static PersonalityTraits Balanced => new PersonalityTraits
    {
        RiskTolerance = 1f,
        Ambition = 1f,
        Curiosity = 1f,
        Loyalty = 1f,
        Sociability = 1f,
        Greed = 1f
    };
}
```

### Trait Influence on Decisions

#### **Risk Tolerance**

**Cautious (0.0-0.7)**:
- Flee threshold: -20% (flees earlier)
- Combat engagement: -30% score
- Exploration: -40% score
- Stay near village: +20% score

**Normal (0.7-1.3)**:
- No modifiers

**Reckless (1.3-2.0)**:
- Flee threshold: +30% (stays in fight longer)
- Combat engagement: +20% score
- Exploration: +30% score
- Ignore danger: +0.15 to risky actions

**Formula**:
```csharp
var riskMod = (riskTolerance - 1f) * 0.3f;
combatScore *= (1f + riskMod);
fleeThreshold *= (1f - riskMod * 0.2f);
```

#### **Ambition**

**Lazy (0.0-0.7)**:
- Work actions: -25% score
- Rest/idle: +40% score
- Job completion: Slower by 15%
- Leadership roles: -50% score

**Driven (1.3-2.0)**:
- Work actions: +30% score
- Rest: Delayed (only when energy < 15)
- Productivity: +15%
- Skill learning: +20% XP gain

**Formula**:
```csharp
var ambitionMod = (ambition - 1f);
workScore *= (1f + ambitionMod * 0.3f);
restThreshold = math.lerp(20f, 10f, math.saturate(ambition - 1f));
```

#### **Curiosity**

**Traditional (0.0-0.7)**:
- Stick to known jobs: +20%
- Avoid new areas: +30%
- Learning new lessons: -15%
- Follow group behavior: +25%

**Explorer (1.3-2.0)**:
- Exploration actions: +50% score
- Try new job types: +30% score
- Learning new lessons: +25%
- Wander/idle: +15% (exploring)

**Formula**:
```csharp
var curiosityBonus = math.max(0f, (curiosity - 1f) * 0.5f);
exploreScore *= (1f + curiosityBonus);
newLessonProgress *= (1f + (curiosity - 1f) * 0.25f);
```

#### **Loyalty**

**Selfish (0.0-0.7)**:
- Group jobs: -30% score
- Keep resources: +40% (less sharing)
- Desertion chance: +50%
- Help others: -40%

**Devoted (1.3-2.0)**:
- Group jobs: +30% score
- Share resources: +50%
- Desertion chance: -80%
- Help others: +60%
- Die for faction: Possible at loyalty > 1.7

**Formula**:
```csharp
var loyaltyMod = (loyalty - 1f);
groupJobScore *= (1f + loyaltyMod * 0.3f);
desertionChance *= math.max(0.2f, 1f - loyaltyMod * 0.8f);
```

#### **Sociability**

**Hermit (0.0-0.7)**:
- Social actions: -60% score
- Alone time: +40% score
- Group size preference: 1-2 villagers
- Stress from crowds: +morale decay

**Social Butterfly (1.3-2.0)**:
- Social actions: +80% score
- Alone penalty: -20% morale when isolated
- Group size preference: 5+ villagers
- Conversation: +teaching/learning speed

**Formula**:
```csharp
var socialMod = (sociability - 1f);
socialScore *= (1f + socialMod * 0.8f);
var groupBonus = groupSize > 1 ? socialMod * 0.15f : 0f;
productivity *= (1f + groupBonus);
```

#### **Greed**

**Generous (0.0-0.7)**:
- Sharing probability: 80%
- Resource hoarding: -50%
- Charity actions: +60%
- Trade fairness: +30% (better deals for others)

**Greedy (1.3-2.0)**:
- Sharing probability: 20%
- Resource hoarding: +70%
- Steal chance: +(greed - 1) * 15%
- Trade fairness: -40% (demands more)

**Formula**:
```csharp
var greedMod = (greed - 1f);
var shareProbability = math.clamp(0.5f - greedMod * 0.3f, 0.1f, 0.9f);
var hoardBonus = greedMod * 0.7f;
```

---

## Social & Cultural Modifiers

### Relationship Influence

Villagers track relationships with other entities (villagers, factions, deities):

```csharp
public struct RelationshipBuffer : IBufferElementData
{
    public Entity Target;
    public sbyte Relationship; // -128 (enemy) to +127 (beloved)
    public uint LastInteractionTick;
}
```

**Relationship Effects**:

| Relationship | Effect |
|--------------|--------|
| **+100 to +127** (Beloved) | +50% cooperation, share resources freely, defend to death |
| **+50 to +99** (Friend) | +25% cooperation, help with jobs, positive morale modifier |
| **+10 to +49** (Acquaintance) | +10% cooperation, neutral interactions |
| **-9 to +9** (Neutral) | No modifiers |
| **-10 to -49** (Dislike) | -10% cooperation, avoid, snide comments |
| **-50 to -99** (Enemy) | -50% cooperation, sabotage possible, attack if safe |
| **-100 to -128** (Hated) | Attack on sight, refuse all cooperation |

**Relationship Decay/Growth**:
- Shared work: +1 per hour
- Conversation: +2 per interaction
- Insult/harm: -10 immediate
- Help in crisis: +15 immediate
- Abandonment: -20 immediate
- Natural decay toward 0: ±1 per day

### Cultural Beliefs

Belief systems modify priorities and unlock unique actions:

```csharp
public struct CulturalBeliefs : IComponentData
{
    /// <summary>
    /// Deity alignment (-1 = chaos, 0 = neutral, +1 = order).
    /// </summary>
    public float DeityAlignment;

    /// <summary>
    /// Work ethic (0 = leisure, 1 = balanced, 2 = workaholic culture).
    /// </summary>
    public float WorkEthic;

    /// <summary>
    /// Martial culture (0 = pacifist, 1 = defensive, 2 = aggressive).
    /// </summary>
    public float MartialCulture;

    /// <summary>
    /// Individualism (0 = collectivist, 1 = balanced, 2 = individualist).
    /// </summary>
    public float Individualism;
}
```

**Work Ethic** (cultural):
- **Low (0.0-0.7)**: Rest threshold +30%, idle time +50%, festivals prioritized
- **High (1.3-2.0)**: Rest threshold -20%, productivity +15%, burnout risk +20%

**Martial Culture**:
- **Pacifist (0.0-0.7)**: Combat jobs -60%, diplomacy +40%, flee +30%
- **Aggressive (1.3-2.0)**: Combat jobs +50%, training +30%, raid actions available

**Deity Alignment** (from VillagerBelief):
```csharp
public struct VillagerBelief : IComponentData
{
    public FixedString64Bytes PrimaryDeityId;
    public float Faith; // 0-1 belief strength
}
```

**Faith Effects**:
- **High Faith (0.8-1.0)**: +20% morale, +worship actions, miracle witnessing boost
- **Low Faith (0.0-0.3)**: -10% morale, skeptical of miracles, vulnerable to conversion

**Miracle Witnessing**:
```csharp
// From VillagerBehaviorConfig
var alignmentBonus = config.MiracleAlignmentBonus; // +5 alignment
var influenceRange = config.MiracleInfluenceRange; // 20m

// When miracle occurs within range:
if (distanceToMiracle < influenceRange)
{
    faith += 0.1f * (1f - distanceToMiracle / influenceRange);
    morale += 10f;
    alignment += alignmentBonus;
}
```

---

## Environmental Context Modifiers

### Time of Day

```csharp
public enum TimeOfDay : byte
{
    Dawn = 0,     // 06:00-08:00
    Morning = 1,  // 08:00-12:00
    Noon = 2,     // 12:00-14:00
    Afternoon = 3,// 14:00-18:00
    Dusk = 4,     // 18:00-20:00
    Night = 5,    // 20:00-06:00
}
```

**Time-Based Modifiers**:

| Time | Work | Social | Rest | Combat |
|------|------|--------|------|--------|
| Dawn | +10% | +5% | -20% | 0% |
| Morning | +15% | +10% | -30% | +5% |
| Noon | +5% | +15% | +10% | 0% |
| Afternoon | +10% | +5% | 0% | +5% |
| Dusk | -10% | +20% | +15% | -10% |
| Night | -30% | -10% | +50% | -20% |

**Implementation**:
```csharp
var timeMultiplier = timeOfDay switch
{
    TimeOfDay.Morning => 1.15f,
    TimeOfDay.Night => 0.7f,
    _ => 1f
};
workScore *= timeMultiplier;
```

### Weather

```csharp
public enum WeatherType : byte
{
    Clear = 0,
    Rain = 1,
    Storm = 2,
    Snow = 3,
    Fog = 4
}
```

**Weather Effects**:
- **Rain**: -20% outdoor work, +30% indoor work, +morale for some (varies)
- **Storm**: -60% outdoor work, flee to shelter +2.0 score, -visibility
- **Snow**: -30% movement speed, -40% outdoor work, +heating need
- **Fog**: -50% sensor range, +fear/caution, -10% productivity

### Danger Proximity

**Threat Detected** (from SensorState):
```csharp
public struct SensorState : IComponentData
{
    public byte HighestThreat; // 0-255
    public Entity HighestThreatEntity;
    public float NearestDistance;
}
```

**Threat Level Modifiers**:

| Threat Level | Work Penalty | Flee Bonus | Combat Consideration |
|--------------|--------------|------------|---------------------|
| 0-30 (Low) | 0% | 0% | Ignore |
| 31-60 (Medium) | -10% | +0.2 | Consider if strong |
| 61-100 (High) | -30% | +0.5 | Flee unless overwhelming |
| 101-150 (Very High) | -60% | +1.0 | Flee immediately |
| 151+ (Extreme) | -90% | +2.0 | Panic flee |

**Distance Decay**:
```csharp
var threatImpact = (highestThreat / 100f) * math.max(0f, 1f - (nearestDistance / 50f));
workScore *= (1f - threatImpact * 0.6f);
fleeScore += threatImpact * 1.0f;
```

---

## Decision Hysteresis & Commitment

To prevent action flickering, the system uses **hysteresis** and **commitment duration**:

### Switch Threshold

```csharp
public struct UtilityConfig : IComponentData
{
    public float SwitchThreshold; // Default: 0.2
}
```

**Rule**: Only switch actions if new action score > current score + threshold

**Example**:
- Current action: "Gather Wood" (score: 0.8)
- Alternative: "Craft Tools" (score: 0.95)
- Difference: 0.95 - 0.8 = 0.15
- Threshold: 0.2
- **Result**: Stay with "Gather Wood" (difference < threshold)

### Minimum Action Duration

```csharp
public struct UtilityDecisionState : IComponentData
{
    public uint ActionSelectedTick;
    public uint MinActionDurationTicks; // Minimum commitment
}
```

**Application**:
- When action selected, set `MinActionDurationTicks` (e.g., 60 ticks = ~1 second)
- No reconsideration until `currentTick >= ActionSelectedTick + MinActionDurationTicks`
- Prevents thrashing between similar-scored actions

### Interruption Conditions

Certain events **force** immediate reconsideration:

1. **Health drops below FleeHealthThreshold**: Force flee evaluation
2. **Hunger reaches starvation (< 10)**: Force eat/flee
3. **External command** (player/deity intervention): Override current action
4. **Job completion**: Natural end, select next action
5. **Target destroyed/unreachable**: Current action impossible

```csharp
if (state.Interrupted)
{
    // Bypass minimum duration, reconsider immediately
    state.MinActionDurationTicks = 0;
    ReconsiderAction(ref state, ...);
}
```

---

## Advanced Decision Patterns

### Multi-Factor Utility Example: Harvest Decision

A villager decides **which resource to harvest** based on:

1. **Village Need** (how much is needed)
2. **Distance** (travel cost)
3. **Skill Match** (efficiency)
4. **Resource Quality** (better rewards)
5. **Social Pressure** (others requesting)

**Scoring Formula**:
```csharp
public static float ScoreHarvestTarget(
    in VillagerNeeds needs,
    in VillagerAttributes attributes,
    in SkillSet skills,
    in PersonalityTraits traits,
    in ResourceSourceConfig resource,
    float distanceToResource,
    float villageNeedPriority,
    float socialPressure)
{
    // Base score from village need
    var baseScore = villageNeedPriority; // 0-1

    // Distance penalty (exponential decay)
    var distancePenalty = math.exp(-distanceToResource / 50f);

    // Skill efficiency bonus
    var skillId = resource.RequiredSkill;
    var skillLevel = skills.GetLevel(skillId);
    var skillBonus = skillLevel / 100f; // 0-1

    // Quality incentive (greed modifier)
    var qualityBonus = (resource.BaseQuality / 100f) * traits.Greed;

    // Energy feasibility
    var energyFactor = math.saturate(needs.Energy / 50f);

    // Social pressure (loyalty modifier)
    var socialFactor = socialPressure * traits.Loyalty;

    // Combine factors
    var totalScore = baseScore
        * distancePenalty
        * (1f + skillBonus + qualityBonus + socialFactor)
        * energyFactor;

    return totalScore;
}
```

**Example Values**:
- Village Need: 0.8 (high demand for wood)
- Distance: 30m → penalty = exp(-30/50) = 0.549
- Skill: 60 → bonus = 0.6
- Quality: 70 → bonus = 0.7 * 1.2 (greed) = 0.84
- Energy: 50 → factor = 1.0
- Social: 0.3 * 1.5 (loyalty) = 0.45

**Final Score**: 0.8 * 0.549 * (1 + 0.6 + 0.84 + 0.45) * 1.0 = **1.268**

### Emergent Behavior: Radical Formation

Low morale + high exposure to oppression → radicalization

```csharp
// Conditions for radicalization:
var moraleThreshold = 20f;
var oppressionExposure = 5; // Witnessed 5 oppressive acts
var personalityFactor = (traits.Loyalty < 0.5f) && (traits.RiskTolerance > 1.2f);

if (needs.Morale < moraleThreshold
    && oppressionExposure >= 5
    && personalityFactor)
{
    // Join or form radical group
    var radicalScore = 2.0f + (moraleThreshold - needs.Morale) / 10f;
    // Radicals prioritize disruption/rebellion actions
}
```

**Radical Actions** (new action types):
- Sabotage production: Score based on resentment
- Recruit others: Score based on sociability + shared grievances
- Protest: Score based on group size + safety
- Revolt: Score based on group strength vs authority

---

## Game-Specific Examples

### Godgame Villager Decision-Making

**Unique Factors**:
- Divine favor/alignment
- Miracle witnessing
- Creature interactions
- Rain/drought cycles

**Example: Rain Miracle Response**

```csharp
// Villager witnesses rain miracle
if (miracleType == MiracleType.Rain && distanceToMiracle < 30f)
{
    // Immediate effects
    needs.Morale += 15f;
    belief.Faith += 0.15f;
    alignment += config.MiracleAlignmentBonus; // +5

    // Action priority shifts
    var worshipScore = 1.5f * belief.Faith; // High faith = strong desire
    var celebrateScore = 0.8f * traits.Sociability;
    var workBonus = 0.2f; // Morale boost improves work desire

    // Potential outcomes:
    // - High faith → worship action selected
    // - High sociability → celebrate with others
    // - High ambition → work harder (inspired)
}
```

### Space4X Crew Decision-Making

**Unique Factors**:
- Ship systems status
- Oxygen/life support
- Threat level (combat, radiation)
- Crew hierarchy/roles

**Example: Emergency Repair Decision**

```csharp
// Ship hull breach detected
var hullIntegrity = 35f; // Critical
var oxygenLevel = 60f; // Dropping
var role = CrewRole.Engineer;

// Engineer scores repair highly
var roleBonus = role == CrewRole.Engineer ? 1.5f : 0.5f;

// Urgency from hull integrity
var urgencyFactor = math.saturate((100f - hullIntegrity) / 50f); // 1.3

// Personal risk (low health = hesitation)
var riskFactor = health > 50f ? 1f : 0.6f;

var repairScore = 0.8f * roleBonus * urgencyFactor * riskFactor;
// = 0.8 * 1.5 * 1.3 * 1.0 = 1.56 (high priority)

// But if health < 30 and role != Engineer:
// = 0.8 * 0.5 * 1.3 * 0.6 = 0.312 (low priority, might flee/seal section)
```

---

## Implementation Checklist

### Core Systems
- [x] `VillagerNeeds` component (hunger, energy, morale, health, temperature)
- [x] `VillagerAttributes` component (physique, finesse, willpower, derived stats)
- [x] `VillagerBelief` component (faith, deity alignment)
- [x] `UtilityDecisionState` component (current action, scores, timing)
- [x] `ActionScore` buffer (candidates with scores)
- [x] `UtilityCurveRef` blob (curve definitions for evaluation)
- [x] `SensorConfig` and `SensorState` (perception)
- [x] `DetectedEntity` buffer (threats, opportunities)
- [x] `SkillSet` component (skill levels, XP pools)
- [x] `VillagerKnowledge` component (lessons, progress)

### Optional/Future Systems
- [ ] `PersonalityTraits` component (risk, ambition, curiosity, loyalty, sociability, greed)
- [ ] `RelationshipBuffer` (entity-to-entity relationships)
- [ ] `CulturalBeliefs` component (deity alignment, work ethic, martial culture)
- [ ] `MemoryBuffer` (remembered locations, events, people)
- [ ] `EmotionalState` component (anger, fear, joy, etc.)

### Decision Systems
- [x] `VillagerAISystem` (current basic implementation)
- [ ] `UtilityScoringSystem` (evaluate all candidate actions)
- [ ] `PerceptionUpdateSystem` (populate sensor data)
- [ ] `ActionExecutionSystem` (perform selected action)
- [ ] `PersonalityModifierSystem` (apply trait biases)
- [ ] `SocialInfluenceSystem` (relationship effects, group dynamics)

### Supporting Systems
- [x] `VillagerJobSystem` (job execution, productivity calculation)
- [x] `SkillProgressionSystem` (XP gain, level-up)
- [x] `KnowledgeLearningSystem` (lesson acquisition, age/mind modifiers)
- [ ] `MoraleSystem` (morale decay/growth from events)
- [ ] `RelationshipSystem` (relationship decay/growth)
- [ ] `PersonalityGenerationSystem` (spawn villagers with random traits)

---

## References

- **Components**: `VillagerComponents.cs`, `UtilityComponents.cs`, `SensorComponents.cs`
- **Config**: `VillagerBehaviorConfig.cs`
- **Systems**: `VillagerAISystem.cs`, `VillagerJobSystems.cs`
- **Helpers**: `JobBehaviors.cs` (age/mind scalars, learning modifiers)
- **Related Docs**: `AI_Integration_Guide.md`, `SkillProgressionSystem.md`, `KnowledgeSystems.md`

---

## Future Enhancements

1. **Emotion System**: Temporary emotional states (anger, fear, joy) that modify decisions
2. **Memory System**: Remember past events/locations to inform future decisions
3. **Goal-Oriented Action Planning (GOAP)**: Long-term planning for complex multi-step goals
4. **Social Contagion**: Moods/behaviors spreading through crowds
5. **Cultural Evolution**: Cultures change based on collective behaviors over time
6. **Genetic Traits**: Inherited personality/stat biases from parents
7. **Trauma System**: Negative experiences create lasting behavioral changes
8. **Mentorship**: Villagers teach each other skills/knowledge faster through relationships

---

**End of Design Document**
