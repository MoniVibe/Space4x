# Villager Decision-Making Quick Reference

**Companion to**: `VillagerDecisionMaking.md`
**For**: Quick lookup of formulas, thresholds, and weights

---

## Stat Influence Multipliers

### Primary Attributes (Base 5, Range 0-10)

| Stat | Action Type | Formula | Effect Range |
|------|-------------|---------|--------------|
| **Physique** | Gather (physical) | `(physique - 5) * 0.1` | -50% to +50% |
| **Physique** | Combat | `(physique - 5) * 0.15` | -75% to +75% |
| **Physique** | Flee Threshold | `baseThreshold * (1 - (physique-5)*0.05)` | +25% to -25% |
| **Finesse** | Craft | `(finesse - 5) * 0.12` | -60% to +60% |
| **Finesse** | Quality Variance | `0.2 - (finesse * 0.03)` | 20% to -10% |
| **Willpower** | Flee Threshold | `baseThreshold * (1 + willpower*0.03)` | +0% to +30% |
| **Willpower** | Morale Decay | `baseDecay * (1 - willpower*0.04)` | 100% to 60% |
| **Willpower** | Resist Social | `willpower * 0.08` | 0% to 80% |

### Derived Attributes (Range 0-200)

| Stat | Effect | Per Point | Per 10 Points |
|------|--------|-----------|---------------|
| **Strength** | Carry Capacity | +0.5 units | +5 units |
| **Strength** | Melee Damage | +0.2% | +2% |
| **Agility** | Movement Speed | +0.125% | +1.25% |
| **Agility** | Dodge Chance | +0.05% | +0.5% |
| **Intelligence** | Craft Quality | +0.1% | +1% |
| **Intelligence** | Learn Rate | +0.2% | +2% |
| **Wisdom** | Threat Assessment | +0.2% | +2% |
| **Wisdom** | Quality Detection | +0.2% | +2% |

---

## Need Thresholds & Formulas

### Hunger (0-100, higher = more fed)

| Range | State | Effect |
|-------|-------|--------|
| 90-100 | Well-fed | No modifier |
| 70-89 | Slight hunger | Eat +0.1 |
| 50-69 | Hungry | Eat +0.3, Others -0.1 |
| 30-49 | Very hungry | Eat +0.8, Others -0.3 |
| 0-29 | **Starving** | Eat +2.0, Others -0.7 |

**Eating Score Formula**:
```csharp
var hungerPressure = math.exp(-(hunger / 50f)) - 0.01f;
var eatScore = hungerPressure * 1.5f;
```

**Work Productivity**:
```csharp
var productivity = math.saturate(hunger / 70f);
// 70 hunger = 100% productivity
// 35 hunger = 50% productivity
```

### Energy (0-100, higher = more rested)

| Range | State | Productivity | Rest Bonus |
|-------|-------|--------------|------------|
| 80-100 | Energetic | +5% | 0 |
| 50-79 | Normal | 100% | 0 |
| 20-49 | Tired | -15% | +0.4 |
| 0-19 | **Exhausted** | -40% | +1.5 |

**Rest Score Formula**:
```csharp
var energyPressure = 1f / (1f + math.exp(0.1f * (energy - 30f)));
var restScore = energyPressure * 1.2f;
```

**Movement Speed**:
```csharp
var speedMult = energy < 20f ? 0.5f : math.lerp(0.85f, 1f, energy / 80f);
```

### Morale (0-100, higher = happier)

| Range | State | Productivity | Rebellion Risk |
|-------|-------|--------------|----------------|
| 80-100 | Happy | +10% | 0% |
| 50-79 | Content | 100% | 0% |
| 30-49 | Unhappy | -10% | Low |
| 0-29 | **Miserable** | -30% | +2% per 10 below 30 |

**Work Multiplier**:
```csharp
var moraleMult = 0.7f + (morale / 100f) * 0.6f;
// Range: 0.7 (miserable) to 1.3 (happy)
```

### Health (0-100, higher = healthier)

| Range | State | Action Penalty | Flee Bias |
|-------|-------|----------------|-----------|
| 80-100 | Healthy | 0% | 0 |
| 50-79 | Injured | -5% | +0.1 |
| 25-49 | Wounded | -20% | +0.5 |
| 0-24 | **Critical** | -50% | +2.0 |

**Flee Threshold**:
```csharp
var fleeThreshold = health < 50f ? 50f + (50f - health) * 0.5f : 25f;
// 50 health → 50 threshold
// 25 health → 62.5 threshold
```

---

## Skill Modifiers

### Gather Rate
```csharp
var skillBonus = 1f + (skillLevel / 100f);
var gatherRate = baseRate * skillBonus;
// Skill 0: 1x, Skill 50: 1.5x, Skill 100: 2x
```

### Craft Quality
```csharp
var qualityBonus = skillLevel / 5f; // +1% per 5 levels
var finalQuality = baseQuality * (1f + qualityBonus / 100f);
```

### Learning Rate (Diminishing Returns)
```csharp
var xpMult = math.max(0.25f, 1.5f - (skillLevel / 100f));
// Skill 0: 1.5x XP, Skill 50: 1.0x, Skill 100: 0.5x
```

---

## Age-Based Learning Scalars

| Age Range | Learning Multiplier | Formula |
|-----------|---------------------|---------|
| 0-12 years | 175% → 130% | `lerp(1.75f, 1.3f, age/12f)` |
| 12-25 years | 130% → 100% | `lerp(1.3f, 1f, (age-12f)/13f)` |
| 25-45 years | **100%** | `1f` (prime) |
| 45-70 years | 100% → 85% | `lerp(1f, 0.85f, (age-45f)/25f)` |
| 70+ years | 70% | `0.7f` (elderly) |

---

## Intelligence/Wisdom Learning Scalar

```csharp
var combined = (intelligence + wisdom) * 0.5f;
var mindScalar = math.clamp(0.6f + combined / 200f, 0.6f, 1.8f);
```

| Combined Stat | Multiplier | Notes |
|---------------|------------|-------|
| 0-20 | 60% | Very dull |
| 50-100 | 85%-110% | Average |
| 150-200 | 135%-180% | Genius |

---

## Personality Trait Modifiers

### Risk Tolerance (0-2, normal = 1)

| Value | Type | Combat | Flee Threshold | Exploration |
|-------|------|--------|----------------|-------------|
| 0.0-0.7 | Cautious | -30% | -20% | -40% |
| 0.7-1.3 | Normal | 0% | 0% | 0% |
| 1.3-2.0 | Reckless | +20% | +30% | +30% |

**Formula**:
```csharp
var riskMod = (riskTolerance - 1f) * 0.3f;
combatScore *= (1f + riskMod);
```

### Ambition (0-2, normal = 1)

| Value | Type | Work | Rest Threshold | Productivity |
|-------|------|------|----------------|--------------|
| 0.0-0.7 | Lazy | -25% | 30 energy (high) | -15% |
| 0.7-1.3 | Normal | 0% | 20 energy | 0% |
| 1.3-2.0 | Driven | +30% | 10 energy (low) | +15% |

**Formula**:
```csharp
var ambitionMod = (ambition - 1f);
workScore *= (1f + ambitionMod * 0.3f);
```

### Curiosity (0-2, normal = 1)

| Value | Type | Exploration | New Lessons | Routine Preference |
|-------|------|-------------|-------------|-------------------|
| 0.0-0.7 | Traditional | -40% | -15% | +25% |
| 0.7-1.3 | Normal | 0% | 0% | 0% |
| 1.3-2.0 | Explorer | +50% | +25% | -20% |

**Formula**:
```csharp
var curiosityBonus = math.max(0f, (curiosity - 1f) * 0.5f);
exploreScore *= (1f + curiosityBonus);
```

### Loyalty (0-2, normal = 1)

| Value | Type | Group Jobs | Sharing | Desertion Chance |
|-------|------|-----------|---------|------------------|
| 0.0-0.7 | Selfish | -30% | -40% | +50% |
| 0.7-1.3 | Normal | 0% | 0% | Baseline |
| 1.3-2.0 | Devoted | +30% | +50% | -80% |

**Formula**:
```csharp
var loyaltyMod = (loyalty - 1f);
desertionChance *= math.max(0.2f, 1f - loyaltyMod * 0.8f);
```

### Sociability (0-2, normal = 1)

| Value | Type | Social Actions | Alone Penalty | Group Bonus |
|-------|------|----------------|---------------|-------------|
| 0.0-0.7 | Hermit | -60% | 0 | 0 |
| 0.7-1.3 | Normal | 0% | 0 | 0 |
| 1.3-2.0 | Social | +80% | -20% morale | +15% productivity |

**Formula**:
```csharp
var socialMod = (sociability - 1f);
socialScore *= (1f + socialMod * 0.8f);
```

### Greed (0-2, normal = 1)

| Value | Type | Sharing Probability | Hoarding | Theft Risk |
|-------|------|---------------------|----------|------------|
| 0.0-0.7 | Generous | 80% | -50% | 0% |
| 0.7-1.3 | Normal | 50% | 0% | 0% |
| 1.3-2.0 | Greedy | 20% | +70% | +15% |

**Formula**:
```csharp
var greedMod = (greed - 1f);
var shareProbability = math.clamp(0.5f - greedMod * 0.3f, 0.1f, 0.9f);
```

---

## Time of Day Multipliers

| Time | Work | Social | Rest | Combat |
|------|------|--------|------|--------|
| Dawn (06-08) | 1.1x | 1.05x | 0.8x | 1.0x |
| Morning (08-12) | **1.15x** | 1.1x | 0.7x | 1.05x |
| Noon (12-14) | 1.05x | **1.15x** | 1.1x | 1.0x |
| Afternoon (14-18) | 1.1x | 1.05x | 1.0x | 1.05x |
| Dusk (18-20) | 0.9x | **1.2x** | 1.15x | 0.9x |
| Night (20-06) | **0.7x** | 0.9x | **1.5x** | 0.8x |

---

## Weather Multipliers

| Weather | Outdoor Work | Indoor Work | Movement | Visibility |
|---------|--------------|-------------|----------|------------|
| Clear | 1.0x | 1.0x | 1.0x | 100% |
| Rain | 0.8x | 1.3x | 0.9x | 80% |
| Storm | **0.4x** | 1.2x | 0.7x | 50% |
| Snow | 0.7x | 1.1x | **0.7x** | 70% |
| Fog | 0.9x | 1.0x | 1.0x | **50%** |

---

## Threat Level Response

| Threat Level | Work Penalty | Flee Bonus | Behavior |
|--------------|--------------|------------|----------|
| 0-30 (Low) | 0% | 0 | Ignore |
| 31-60 (Medium) | -10% | +0.2 | Cautious |
| 61-100 (High) | -30% | +0.5 | Consider fleeing |
| 101-150 (Very High) | -60% | +1.0 | **Flee unless strong** |
| 151+ (Extreme) | -90% | +2.0 | **Panic flee** |

**Distance Decay**:
```csharp
var threatImpact = (threat / 100f) * math.max(0f, 1f - (distance / 50f));
```

---

## Decision Hysteresis

### Switch Threshold
```csharp
// Only switch if newScore > currentScore + threshold
var threshold = 0.2f; // Default from UtilityConfig
if (newActionScore > currentActionScore + threshold)
{
    SwitchAction(newAction);
}
```

### Minimum Action Duration
```csharp
// No reconsideration for minimum duration
var minTicks = 60; // ~1 second
if (currentTick < actionSelectedTick + minTicks)
{
    return; // Stay committed
}
```

---

## Relationship Modifiers

| Relationship | Range | Cooperation | Special Actions |
|--------------|-------|-------------|-----------------|
| Beloved | +100 to +127 | +50% | Defend to death, share all |
| Friend | +50 to +99 | +25% | Help with jobs, positive morale |
| Acquaintance | +10 to +49 | +10% | Neutral interactions |
| Neutral | -9 to +9 | 0% | No modifiers |
| Dislike | -10 to -49 | -10% | Avoid, snide comments |
| Enemy | -50 to -99 | -50% | Sabotage possible, attack if safe |
| Hated | -100 to -128 | -100% | **Attack on sight** |

**Relationship Growth/Decay**:
- Shared work: +1/hour
- Conversation: +2/interaction
- Help in crisis: +15 immediate
- Insult/harm: -10 immediate
- Abandonment: -20 immediate
- Natural decay toward 0: ±1/day

---

## Quick Calculation Examples

### "Should I eat?"
1. Check hunger: If < 70 → eating pressure increases
2. Calculate: `exp(-(hunger/50)) - 0.01` → eating score
3. If score > current action + 0.2 → **Eat**

### "Should I flee from combat?"
1. Calculate power ratio: `myPower / enemyPower`
2. Check health: If < 50 → flee bonus increases
3. Apply willpower: Low will = flee easier
4. If flee score > fight score + 0.2 → **Flee**

### "Which job should I take?"
1. For each job: `baseScore * priority * distancePenalty * (1 + skillBonus)`
2. Apply energy feasibility: `* saturate(energy/50)`
3. Apply morale: `* (0.7 + morale/100 * 0.6)`
4. Highest score wins (if > current + threshold)

### "How fast do I learn this lesson?"
1. Base delta: `amount * 0.001 * (skill/120) / difficulty`
2. Age scalar: Check table (e.g., age 25 = 1.0)
3. Mind scalar: `0.6 + (int+wis)*0.5 / 200` (capped 0.6-1.8)
4. Final: `base * age * mind`

---

## Common Formulas (Copy-Paste Ready)

### Hunger Pressure (Exponential)
```csharp
var hungerPressure = math.exp(-(hunger / 50f)) - 0.01f;
var eatScore = hungerPressure * 1.5f;
```

### Energy Rest Pressure (Logistic)
```csharp
var energyPressure = 1f / (1f + math.exp(0.1f * (energy - 30f)));
var restScore = energyPressure * 1.2f;
```

### Morale Work Modifier (Linear)
```csharp
var moraleMultiplier = 0.7f + (morale / 100f) * 0.6f;
workScore *= moraleMultiplier;
```

### Skill Productivity Bonus (Linear)
```csharp
var skillBonus = 1f + (skillLevel / 100f);
var productivity = baseProductivity * skillBonus;
```

### Age Learning Scalar (Piecewise)
```csharp
float GetAgeLearningScalar(float ageYears)
{
    if (ageYears <= 12f)
        return math.lerp(1.75f, 1.3f, ageYears / 12f);
    if (ageYears <= 25f)
        return math.lerp(1.3f, 1f, (ageYears - 12f) / 13f);
    if (ageYears <= 45f)
        return 1f;
    if (ageYears <= 70f)
        return math.lerp(1f, 0.85f, (ageYears - 45f) / 25f);
    return 0.7f;
}
```

### Mind Learning Scalar (Clamped)
```csharp
var combined = (intelligence + wisdom) * 0.5f;
var mindScalar = math.clamp(0.6f + combined / 200f, 0.6f, 1.8f);
```

### Threat Impact with Distance (Exponential Decay)
```csharp
var threatFactor = (threatLevel / 100f) * math.max(0f, 1f - (distance / 50f));
workScore *= (1f - threatFactor * 0.6f);
fleeScore += threatFactor * 1.0f;
```

### Personality-Modified Score (Generic)
```csharp
var traitModifier = (traitValue - 1f) * traitWeight;
score *= (1f + traitModifier);
```

---

**End of Quick Reference**
