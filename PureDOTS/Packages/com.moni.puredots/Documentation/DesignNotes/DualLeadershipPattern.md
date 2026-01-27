# Dual Leadership Pattern: Symbolic & Operational Roles

## Overview

The **Dual Leadership Pattern** is a design pattern that leverages existing PureDOTS systems (alignment, outlooks, cohesion, grudges, moral conflict, splintering) to create emergent leadership dynamics within aggregates. Instead of implementing a new system, it's a **configuration pattern** that assigns two distinct leadership roles to aggregate members:

- **SymbolicLeader**: The face, ideology bearer, prophet, lord (Captain, Guildmaster, Warlord)
- **OperationalLeader**: The steward, professional, logistics expert (Shipmaster, Quartermaster, High Steward)

This pattern generates rich narrative drama through the interaction of two leaders with potentially different alignments, outlooks, and personalities—all using components and systems already present in the framework.

## Goals

- ✅ **Zero new systems**: Uses existing alignment, cohesion, grudges, moral conflict, voting, and splintering
- ✅ **Emergent drama**: Captain-Shipmaster conflicts arise naturally from alignment/outlook differences
- ✅ **Cross-project**: Works identically for Space4X (ships/fleets) and Godgame (bands/guilds)
- ✅ **Simple implementation**: Just two role slots + one derived friction value per aggregate
- ✅ **Rich narratives**: Enables promotions, refusals, coups, loyalty dilemmas without scripting

## Core Concept

### Representing Dual Leadership

At the aggregate level (band, fleet, ship, guild), add two role slots to the existing structure:

```csharp
public struct AggregateLeadership : IComponentData
{
    public Entity SymbolicLeader;      // Captain, Prophet, Warlord, Guildmaster
    public Entity OperationalLeader;   // Shipmaster, Quartermaster, Steward, Road Captain
    public float CommandFriction;      // Derived: 0 (aligned) to 1 (conflicted)
}
```

These are **not special entity types**—they're normal members assigned to leadership roles.

**Example aggregate structure**:
```
Aggregate (Fleet or Band)
  members: [entityIds...]
  leadership:
    SymbolicLeader: entityId?
    OperationalLeader: entityId?
    CommandFriction: 0–1
  cohesion: 0–1
  governance: Democratic / Meritocratic / Authoritarian / Oligarchic / Anarchy
```

### Cross-Project Mapping

| Context | Aggregate Type | SymbolicLeader | OperationalLeader |
|---------|----------------|----------------|-------------------|
| **Space4X** | Ship, Fleet | Captain, Admiral | Shipmaster, Flag Officer |
| **Godgame** | Band, Guild, Cult | Prophet, Warlord, Bandleader | Quartermaster, Road Captain, High Steward |

Same data structure, different narrative dressing.

## Using Existing Systems

### 1. Alignment Distance as Conflict Driver

Leverage the existing **tri-axis alignment system**:

```csharp
AlignmentDistance =
  |Moral_sym - Moral_op| +
  |Order_sym - Order_op| +
  |Purity_sym - Purity_op|
```

Normalize by dividing by 600 (max possible distance).

**Axis-specific dissonance**:
- **MoralDissonance**: Big gap on Moral axis → conflict on atrocities vs mercy
- **OrderDissonance**: Big gap on Order axis → conflict on rules vs improvisation
- **PurityDissonance**: Big gap on Purity axis → corruption vs idealism clashes

### 2. Outlook Archetypes

Derive **leadership archetype** from existing outlook system:

| SymbolicLeader Outlook | OperationalLeader Outlook | Archetype |
|------------------------|---------------------------|-----------|
| Fanatic + Loyalist | Loyalist + Pragmatic | **The Crusader & The Steward** |
| Heroic + Righteous | Scholarly + Opportunist | **The Glory Hound & The Professional** |
| Authoritarian + Brutal | Mutinous + Opportunist | **The Tyrant & The Rebel** |
| Scholarly + Methodical | Fanatic + Devout | **The Academic & The Zealot** |

These are **labels for UI/tooltips**, not separate data. Use existing outlook computation.

### 3. Governance & Consensus Integration

Plug leadership roles into the existing **voting system**:

```csharp
// Existing governance styles:
// - Democratic: leader proposes, members vote
// - Authoritarian: leader decides (90% influence)
// - Oligarchic: council votes
// - Anarchy: no consensus

// Dual leadership modification:
// In Authoritarian:
SymbolicLeader.VoteWeight = 0.9f;
OperationalLeader.VoteWeight = 0.1f;

// If they strongly disagree:
if (AlignmentDistance > 0.6f) {
    // Apply existing moral conflict system:
    // - Add delay (hesitation)
    // - Hit cohesion
    // - Increase dissent
}

// In Meritocratic/Oligarchic:
SymbolicLeader.VoteWeight = 0.4f;
OperationalLeader.VoteWeight = 0.4f;
OtherMembers.VoteWeight = 0.2f / memberCount;

// If they disagree, membership splits along alignment/outlook lines
```

### 4. Moral Conflict & Grudge Integration

For each order issued by SymbolicLeader:

1. **OperationalLeader evaluates** using existing moral conflict system:
   ```csharp
   conflictLevel = CalculateMoralConflict(
       operationalLeader.alignment,
       operationalLeader.behavior,
       orderType  // war crime, betrayal, mercy, overwork, etc.
   );
   ```

2. **Apply existing conflict effects**:
   | Conflict Level | Delay | Morale Hit | Loyalty Impact |
   |----------------|-------|------------|----------------|
   | Minor (0.2-0.4) | 10 ticks | -5 | Loyalty to SymbolicLeader -2 |
   | Moderate (0.4-0.6) | 50 ticks | -15 | Loyalty to SymbolicLeader -5 |
   | Major (0.6-0.8) | 150 ticks | -30 | Loyalty to SymbolicLeader -10 |
   | Severe (0.8+) | 300 ticks | -50 | May refuse; crew aligns with OperationalLeader |

3. **Grudge accumulation** (existing system):
   - Professional grudge created: OperationalLeader ↔ SymbolicLeader
   - Grudge intensity based on conflict severity
   - Decays based on VengefulScore (existing behavior)

4. **Loyalty shifts**:
   - If OperationalLeader **obeys** disgusting order:
     - Loyalty to aggregate +5 (success)
     - Morale -30 (moral conflict)
     - Grudge intensity +20
   - If OperationalLeader **resists**:
     - Loyalty to SymbolicLeader -15
     - Crew aligns more with OperationalLeader (+10 loyalty)

5. **Mutiny/Splintering** triggered by existing thresholds:
   - Low cohesion (<0.2)
   - High dissent (>50% members)
   - High grudges (intensity >75)
   - Misaligned loyalties

## Command Friction System

### Derived Value

Add a single derived value per aggregate with dual leadership:

```csharp
CommandFriction =
    k1 * NormalizedAlignmentDistance +
    k2 * (professionalGrudges between leaders) +
    k3 * (outlookClash: Fanatic vs Mutinous = 1.0, Loyalist vs Loyalist = 0.0) +
    -k4 * Bond/Respect (if tracked)

// Example weights:
k1 = 0.4  // Alignment distance
k2 = 0.3  // Grudges
k3 = 0.2  // Outlook clash
k4 = 0.1  // Bond (future)
```

### Effects of Command Friction

**High CommandFriction (>0.6)**:
- Initiative penalty: -20% to aggregate initiative
- Cohesion penalty: -0.15 to cohesion
- Splintering chance: +30% when cohesion <0.3
- Order execution delays: +50% to hesitation duration

**Low CommandFriction (<0.3)**:
- Initiative bonus: +10% to aggregate initiative
- Cohesion bonus: +0.1 to cohesion
- "Special maneuvers" unlock: efficiency buffs for aggregate
- Order execution: -25% hesitation duration

**Medium CommandFriction (0.3-0.6)**:
- Neutral modifiers
- Narrative flavor in tooltips

### Implementation

Tiny system that runs periodically (every 100 ticks):

```csharp
[BurstCompile]
public partial struct UpdateCommandFrictionSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // Only update every 100 ticks
        if (state.WorldUnmanaged.Time.ElapsedTime % 100 != 0) return;

        foreach (var (leadership, cohesion, members) in
            SystemAPI.Query<RefRW<AggregateLeadership>, RefRO<AggregateCohesion>>()
                .WithAll<AggregateMember>())
        {
            if (leadership.ValueRO.SymbolicLeader == Entity.Null ||
                leadership.ValueRO.OperationalLeader == Entity.Null)
                continue;

            // Read alignments
            var symAlignment = SystemAPI.GetComponent<VillagerAlignment>(leadership.ValueRO.SymbolicLeader);
            var opAlignment = SystemAPI.GetComponent<VillagerAlignment>(leadership.ValueRO.OperationalLeader);

            // Calculate alignment distance
            float alignmentDistance = (
                math.abs(symAlignment.MoralAxis - opAlignment.MoralAxis) +
                math.abs(symAlignment.OrderAxis - opAlignment.OrderAxis) +
                math.abs(symAlignment.PurityAxis - opAlignment.PurityAxis)
            ) / 600f;

            // Calculate grudge intensity (existing EntityGrudge buffer)
            float grudgeIntensity = CalculateGrudgeBetween(
                leadership.ValueRO.SymbolicLeader,
                leadership.ValueRO.OperationalLeader
            );

            // Calculate outlook clash (existing outlook system)
            float outlookClash = CalculateOutlookClash(
                GetOutlook(leadership.ValueRO.SymbolicLeader),
                GetOutlook(leadership.ValueRO.OperationalLeader)
            );

            // Compute friction
            leadership.ValueRW.CommandFriction = math.saturate(
                0.4f * alignmentDistance +
                0.3f * grudgeIntensity +
                0.2f * outlookClash
            );
        }
    }
}
```

## Narrative Flows

### Promotion Flow

**Trigger conditions** (Operational → Symbolic):
1. SymbolicLeader slot opens (death, retirement, new ship)
2. OperationalLeader has:
   - High alignment match with aggregate (>0.8)
   - High leadership strength (Wisdom + Will + Alignment)
   - High loyalty to aggregate (>150)
   - Decent reputation (>50)

**Decision logic** (existing systems):
```csharp
ambitionScore = (100 - loyaltyToCurrentSymbolic) * 0.01f;
alignmentFit = 1.0f - alignmentDistance;
promotionUtility = ambitionScore * 0.5f + alignmentFit * 0.3f + reputation * 0.2f;

if (promotionUtility > 0.6f) {
    AcceptPromotion();
} else {
    RefusePromotion();
}
```

**Refusal effects**:
- Loyalty to current SymbolicLeader +20
- Cohesion +0.1
- Reputation "Devoted Steward" +10
- Narrative event: "Quartermaster refuses promotion to stay with beloved captain"

**Acceptance effects**:
- OperationalLeader → new aggregate's SymbolicLeader
- New OperationalLeader elected from senior members
- Loyalty transferred to new aggregate
- Narrative event: "Shipmaster promoted to Captain of new vessel"

### Coup/Mutiny Flow

**Trigger conditions**:
1. CommandFriction >0.7 for 500+ ticks
2. OperationalLeader loyalty to SymbolicLeader <50
3. Cohesion <0.3
4. Dissent >60% members

**Execution** (uses existing splintering):
```csharp
// OperationalLeader + aligned members decide:
// "Our loyalty to crew/morality/faction > loyalty to this leader"

// Trigger splintering behavior:
SplinterAggregate(
    aggregate,
    leader: operationalLeader,  // Becomes new SymbolicLeader
    alignedMembers: FindMembersByAlignment(operationalLeader.alignment, threshold: 0.7f)
);

// Outcomes:
// 1. New aggregate forms with:
//    - Old OperationalLeader as SymbolicLeader
//    - Alignment centered on their values
//    - Members who aligned with them
// 2. Original aggregate:
//    - Old SymbolicLeader becomes normal member, prisoner, or flees
//    - Remaining members (loyalists)
//    - Recalculates alignment/cohesion
```

**Narrative examples**:
- "Shipmaster stages mutiny, declares herself Captain of reformed crew"
- "Quartermaster leads breakaway band of reformers to found gentler sect"
- "High Steward ousts weak prince, assumes regency with popular support"

### Loyal Steward Arc

**Scenario**: Low ambition + high loyalty

```csharp
OperationalLeader has:
    - Loyalty to SymbolicLeader: 180
    - Ambition (inferred from behavior): Low (craven + forgiving)
    - CommandFriction: Low (0.2)

// Offered promotion to Captain of new ship
promotionUtility = (100 - 180) * 0.01f + 0.8f * 0.3f + 0.6f * 0.2f
                 = -0.8f + 0.24f + 0.12f = -0.44f

// Refuses promotion
RefusePromotion();

// Benefits:
Loyalty to SymbolicLeader += 20  // Now 200 (max)
Cohesion += 0.1
Reputation += 10 ("Devoted Steward" tag)

// Narrative:
"Shipmaster [Name] refuses promotion, choosing to remain at Captain [Name]'s side.
'My place is here,' she said simply. Crew morale soars."

// Later, if SymbolicLeader dies in battle:
if (symbolicLeader.isDead && operationalLeader.loyalty > 150) {
    // Loyal steward stays with doomed aggregate
    // Narrative: "Dies defending fallen captain's legacy"
    // Local saint/martyr status
}
```

## Integration with Existing Systems

### 1. Initiative System

```csharp
// Existing formula:
baseInitiative = 0.4f + morale*0.2f + cohesion*0.2f + members/100*0.1f - stress*0.15f;

// Add CommandFriction modifier:
frictionModifier = math.lerp(1.1f, 0.8f, commandFriction);  // Low friction = +10%, High = -20%
finalInitiative = baseInitiative * frictionModifier;
```

### 2. Cohesion System

```csharp
// Existing calculation:
cohesion = alignmentVariance*0.3f + loyaltyAverage*0.3f + leadershipStrength*0.2f - dissent*0.4f;

// Add CommandFriction penalty:
frictionPenalty = commandFriction * 0.15f;  // Up to -0.15 at max friction
finalCohesion = math.saturate(cohesion - frictionPenalty);
```

### 3. Splintering System

```csharp
// Existing thresholds:
if (cohesion < 0.2f && dissent > 0.5f) {
    CheckSplintering();
}

// Add CommandFriction boost:
splinterChance = baseSplinterChance * (1.0f + commandFriction * 0.5f);

// On splintering, if friction high:
if (commandFriction > 0.6f) {
    // Default to OperationalLeader as new SymbolicLeader
    newAggregateLeader = operationalLeader;
}
```

### 4. Voting System

```csharp
// Existing consensus voting:
foreach (var decision in aggregateDecisions) {
    float symVote = SymbolicLeader.EvaluateDecision(decision);
    float opVote = OperationalLeader.EvaluateDecision(decision);

    // Weight votes by governance:
    if (governance == Authoritarian) {
        finalDecision = symVote * 0.9f + opVote * 0.1f;

        // If leaders strongly disagree:
        if (math.abs(symVote - opVote) > 0.5f) {
            // Apply moral conflict:
            ApplyMoralConflict(operationalLeader, decision);
            commandFriction += 0.05f;
        }
    }
    else if (governance == Oligarchic) {
        finalDecision = symVote * 0.4f + opVote * 0.4f + memberAverage * 0.2f;

        // If leaders split, members become tiebreaker
        if (math.abs(symVote - opVote) > 0.5f) {
            // Members align with their preferred leader
            AlignMembersToLeader(symVote > opVote ? symbolicLeader : operationalLeader);
        }
    }
}
```

## Minimal Implementation Checklist

### Phase 1: Core Structure (1-2 hours)

- [ ] Add `AggregateLeadership` component:
  ```csharp
  public struct AggregateLeadership : IComponentData
  {
      public Entity SymbolicLeader;
      public Entity OperationalLeader;
      public float CommandFriction;
  }
  ```

- [ ] Add to aggregate archetypes (Band, Guild, Fleet, Ship)

- [ ] Initialize in formation systems:
  ```csharp
  // In BandFormationSystem, GuildFormationSystem, etc.
  leadership = new AggregateLeadership {
      SymbolicLeader = electLeader(members, LeadershipType.Symbolic),
      OperationalLeader = electLeader(members, LeadershipType.Operational),
      CommandFriction = 0.0f
  };
  ```

### Phase 2: Friction Calculation (2-3 hours)

- [ ] Implement `UpdateCommandFrictionSystem`:
  - Read SymbolicLeader + OperationalLeader alignments
  - Calculate alignment distance
  - Read grudge intensity between leaders
  - Calculate outlook clash
  - Write CommandFriction value

- [ ] Schedule system in gameplay group (runs every 100 ticks)

### Phase 3: Integrate with Existing Systems (3-4 hours)

- [ ] Modify cohesion calculation:
  ```csharp
  cohesion -= commandFriction * 0.15f;
  ```

- [ ] Modify initiative calculation:
  ```csharp
  initiative *= math.lerp(1.1f, 0.8f, commandFriction);
  ```

- [ ] Modify splintering logic:
  ```csharp
  if (commandFriction > 0.6f) {
      newLeader = operationalLeader;
  }
  ```

### Phase 4: Promotion & Refusal (4-5 hours)

- [ ] Add promotion offer logic (triggered when SymbolicLeader slot opens):
  ```csharp
  if (aggregate.SymbolicLeader == Entity.Null && aggregate.OperationalLeader != Entity.Null) {
      OfferPromotion(aggregate.OperationalLeader);
  }
  ```

- [ ] Implement promotion utility calculation

- [ ] Handle acceptance:
  - Reassign to new aggregate as SymbolicLeader
  - Elect new OperationalLeader for old aggregate

- [ ] Handle refusal:
  - Loyalty +20 to current SymbolicLeader
  - Cohesion +0.1
  - Reputation tag "Devoted Steward"

### Phase 5: UI & Narrative (2-3 hours)

- [ ] Add tooltip for CommandFriction:
  ```
  "Command Friction: 0.72 (High Conflict)
  Captain [Name] (Fanatic, Lawful Good) vs
  Shipmaster [Name] (Opportunist, Chaotic Neutral)

  Effects:
  - Initiative: -18%
  - Cohesion: -0.11
  - Splintering Risk: High"
  ```

- [ ] Add archetype labels based on outlook pairing

- [ ] Generate narrative events for:
  - Promotion offered/accepted/refused
  - Coup/mutiny success
  - Loyal steward death

## Testing Strategy

### Unit Tests

```csharp
[Test]
public void CommandFriction_HighAlignmentDistance_IncreasedFriction()
{
    // Setup: Captain (Good +80, Lawful +60) + Shipmaster (Evil -70, Chaotic -50)
    var captain = CreateEntity(alignment: new(80, 60, 0));
    var shipmaster = CreateEntity(alignment: new(-70, -50, 0));

    var friction = CalculateCommandFriction(captain, shipmaster);

    Assert.Greater(friction, 0.6f);  // High friction expected
}

[Test]
public void Promotion_LowLoyalty_Accepts()
{
    var shipmaster = CreateEntity(loyalty: 40, ambition: 0.7f);
    var result = EvaluatePromotion(shipmaster);

    Assert.AreEqual(PromotionResult.Accepted, result);
}

[Test]
public void Promotion_HighLoyalty_Refuses()
{
    var shipmaster = CreateEntity(loyalty: 180, ambition: 0.2f);
    var result = EvaluatePromotion(shipmaster);

    Assert.AreEqual(PromotionResult.Refused, result);
}
```

### Integration Tests

```csharp
[Test]
public void Mutiny_HighFrictionLowCohesion_Splinters()
{
    // Setup: Fleet with high friction, low cohesion
    var fleet = CreateFleet(
        symbolicLeader: CreateEntity(alignment: new(80, 60, 70)),
        operationalLeader: CreateEntity(alignment: new(-60, -50, -80)),
        cohesion: 0.18f
    );

    // Simulate 500 ticks of high friction
    for (int i = 0; i < 50; i++) {
        UpdateCommandFriction();
        UpdateCohesion();
        CheckSplintering();
    }

    Assert.IsTrue(HasSplintered(fleet));
    Assert.AreEqual(GetNewLeader(splinteredFleet), operationalLeader);
}
```

### Simulation Tests

```csharp
[Test]
public void LoyalSteward_RefusesPromotion_StaysWithCaptain()
{
    // Setup: High loyalty shipmaster offered promotion
    var captain = CreateCaptain();
    var shipmaster = CreateShipmaster(loyalty: 180);
    var fleet = CreateFleet(captain, shipmaster);

    // Kill captain in battle
    KillEntity(captain);

    // Offer promotion to new ship
    OfferPromotion(shipmaster, newShip);

    // Should refuse and stay with dying fleet
    Assert.AreEqual(PromotionResult.Refused, shipmaster.LastPromotionResponse);
    Assert.AreEqual(fleet, GetAggregate(shipmaster));
}
```

## Cross-Project Examples

### Space4X: Captain & Shipmaster

**Scenario**: Mining frigate encounters defenseless refugee transport

```
Fleet: Mining Frigate "Endeavor"
  SymbolicLeader: Captain Aria (Good +60, Lawful +50, Integrity +40)
  OperationalLeader: Shipmaster Chen (Neutral 0, Pragmatic, Opportunist)
  CommandFriction: 0.35 (Medium)
  Governance: Meritocratic

Order: "Destroy refugee ship to prevent intel leak"
  Type: AttackDefenseless

Captain Aria (issuer):
  - Alignment: Good +60 → conflict 0.8 (would never order this under normal circumstances)
  - Actually issued by faction commander, not captain

Shipmaster Chen (executor):
  - MoralConflict = (goodness + forgiveness) / 2 = (0.5 + 0.6) / 2 = 0.55 (Moderate)
  - Delay: 50 ticks
  - Morale: -15
  - Loyalty to Captain: -5 (knows captain didn't want this)

Outcome:
  - Shipmaster hesitates, crew morale drops
  - Captain publicly registers dissent with faction
  - CommandFriction increases to 0.45 (both unhappy with order)
  - Grudge created: Faction Commander ↔ Shipmaster (professional)
  - Narrative: "Shipmaster Chen executes orders with reluctance.
                Captain Aria's log: 'This action violates everything we stand for.'"
```

### Godgame: Prophet & Steward

**Scenario**: Cult faces famine, Prophet orders sacrifice

```
Aggregate: Cult of the Radiant Dawn
  SymbolicLeader: Prophet Elara (Fanatic, Pure +80, Good +40)
  OperationalLeader: High Steward Marcus (Scholarly, Order +60, Good +70)
  CommandFriction: 0.58 (Medium-High)
  Governance: Authoritarian

Order: "Sacrifice 10 villagers to appease gods and end famine"
  Type: ExecuteInnocents

Prophet Elara (issuer):
  - Alignment: Pure +80, Good +40 → believes sacrifice will save more lives
  - Fanaticism: High

High Steward Marcus (executor):
  - MoralConflict = (lawfulness + goodness) / 2 = (0.8 + 0.85) / 2 = 0.825 (Severe)
  - Delay: 300 ticks
  - Morale: -50
  - Loyalty to Prophet: -15
  - May refuse if loyalty <100

Current Loyalty: 120 (borderline)

Decision:
  - Steward delays, publicly questions order
  - Cult members split:
    - Fanatics (40%) support Prophet
    - Moderates (60%) align with Steward
  - Cohesion drops to 0.22 (near splintering)

Outcome (2 possibilities):

  1. Steward Complies (low roll):
     - Loyalty to Prophet: 120 - 15 = 105 (still loyal)
     - Morale: -50 (devastated)
     - Grudge: Steward ↔ Prophet intensity +80 (Vendetta)
     - CommandFriction: 0.72 (High)
     - Narrative: "High Steward carries out sacrifice with tears.
                   'May the gods forgive us,' he whispers."

  2. Steward Refuses (high roll):
     - Loyalty to Prophet: 120 - 30 = 90 (broken)
     - Splinter event triggered
     - Steward becomes SymbolicLeader of "Reformed Dawn" (60% members)
     - Prophet leads "True Dawn" (40% members)
     - Narrative: "High Steward Marcus defies Prophet: 'I will not murder innocents.
                   Those who value life, come with me.'"
```

## Future Extensions

### Phase 2: Bond & Respect Tracking

Add relationship component between leaders:

```csharp
public struct LeadershipBond : IComponentData
{
    public float MutualRespect;  // 0-1, built through shared victories
    public float PersonalBond;   // 0-1, built through time together
    public uint PartnershipDuration;  // Ticks working together
}
```

Reduces CommandFriction:
```csharp
bondModifier = -0.1f * (mutualRespect * 0.6f + personalBond * 0.4f);
commandFriction = math.saturate(baseFriction + bondModifier);
```

### Phase 3: Special Maneuvers

Unlock abilities when CommandFriction low + Bond high:

```csharp
if (commandFriction < 0.2f && mutualRespect > 0.8f) {
    UnlockManeuver("Coordinated Strike");  // +30% damage, perfect timing
    UnlockManeuver("Emergency Evasion");   // -50% damage taken, trust-based
}
```

### Phase 4: Mentorship

Long-serving OperationalLeaders mentor junior SymbolicLeaders:

```csharp
if (partnershipDuration > 10000 && symbolicLeader.experience < 0.3f) {
    ApplyMentorshipBonus(symbolicLeader, operationalLeader);
    // SymbolicLeader gains skills/outlook influence from OperationalLeader
}
```

## Technical Considerations

### Performance

- **CommandFriction calculation**: Runs every 100 ticks, O(n) aggregates, Burst-compatible
- **Memory overhead**: +12 bytes per aggregate (2 Entity refs + 1 float)
- **Grudge queries**: Existing EntityGrudge buffer, no new allocations

### Determinism

- All calculations use existing deterministic systems
- Promotion/refusal decisions use seeded random for utility calculations
- Splintering uses existing deterministic splintering logic

### Rewind Support

- CommandFriction is **derived**, recalculated on playback
- Leadership role assignments stored in history buffer
- Promotion/refusal events recorded as discrete events

### Burst Compatibility

All systems Burst-compatible:
```csharp
[BurstCompile]
public partial struct UpdateCommandFrictionSystem : ISystem { ... }
```

## Summary

The **Dual Leadership Pattern** is not a new system—it's a **configuration pattern** that assigns two distinct roles to aggregate members and lets existing systems (alignment, cohesion, moral conflict, grudges, voting, splintering) generate emergent drama.

**Total implementation cost**: ~12-17 hours across 5 phases

**Narrative ROI**: Rich stories about:
- Loyal stewards who refuse promotion
- Shipmaster mutinies against tyrannical captains
- Professional disagreements escalating to coups
- Aligned leadership unlocking special abilities

All without writing a single "if captain disagrees with shipmaster" hardcoded branch.
