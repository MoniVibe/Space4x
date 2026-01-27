# Dual Leadership Pattern - Quick Reference

## TL;DR

Add **two role slots** to aggregates (SymbolicLeader, OperationalLeader). No new systems—just use existing alignment, cohesion, moral conflict, and grudges. Drama emerges from their differences.

## Core Component

```csharp
public struct AggregateLeadership : IComponentData
{
    public Entity SymbolicLeader;      // Captain, Prophet, Warlord, Guildmaster
    public Entity OperationalLeader;   // Shipmaster, Quartermaster, Steward
    public float CommandFriction;      // 0 (aligned) to 1 (conflicted)
}
```

## Command Friction Formula

```csharp
CommandFriction =
    0.4 * NormalizedAlignmentDistance +
    0.3 * GrudgeIntensity +
    0.2 * OutlookClash
```

## Effects

| Friction Level | Initiative | Cohesion | Splintering Risk |
|----------------|------------|----------|------------------|
| Low (<0.3) | +10% | +0.1 | Normal |
| Medium (0.3-0.6) | Normal | Normal | Normal |
| High (>0.6) | -20% | -0.15 | +30% |

## Common Archetypes

| SymbolicLeader | OperationalLeader | Archetype |
|----------------|-------------------|-----------|
| Fanatic + Loyalist | Loyalist + Pragmatic | Crusader & Steward |
| Heroic + Righteous | Scholarly + Opportunist | Glory Hound & Professional |
| Authoritarian + Brutal | Mutinous + Opportunist | Tyrant & Rebel |

## Promotion Logic

```csharp
promotionUtility =
    (100 - loyaltyToCurrentLeader) * 0.5 +
    alignmentFit * 0.3 +
    reputation * 0.2

if (promotionUtility > 0.6) → Accept
else → Refuse (loyalty +20, cohesion +0.1)
```

## Coup Triggers

1. CommandFriction >0.7 for 500+ ticks
2. OperationalLeader loyalty to SymbolicLeader <50
3. Cohesion <0.3
4. Dissent >60%

→ Splintering with OperationalLeader as new SymbolicLeader

## Cross-Project Usage

| Game | Aggregate | SymbolicLeader | OperationalLeader |
|------|-----------|----------------|-------------------|
| **Space4X** | Ship, Fleet | Captain, Admiral | Shipmaster, Flag Officer |
| **Godgame** | Band, Guild | Prophet, Warlord | Quartermaster, Steward |

## Integration Points

- **Alignment**: Distance drives friction
- **Cohesion**: Reduced by friction
- **Initiative**: Modified by friction
- **Moral Conflict**: OperationalLeader evaluates SymbolicLeader orders
- **Grudges**: Accumulate between leaders
- **Voting**: Weight votes by governance type
- **Splintering**: OperationalLeader becomes new leader on mutiny

## Implementation Checklist

- [ ] Add `AggregateLeadership` component
- [ ] Initialize in formation systems (elect leaders)
- [ ] Create `UpdateCommandFrictionSystem` (runs every 100 ticks)
- [ ] Integrate friction into cohesion/initiative calculations
- [ ] Add promotion offer logic
- [ ] Add refusal/acceptance handling
- [ ] Hook into splintering system

**Estimated time**: 12-17 hours across 5 phases

## See Also

- [Full Documentation](DualLeadershipPattern.md)
- [Faction & Guild System](FactionAndGuildSystem.md)
- [Alignment System Summary](../../Docs/BehaviorAlignment_Summary.md)
