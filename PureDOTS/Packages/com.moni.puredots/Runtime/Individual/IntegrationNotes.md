# SimIndividual Integration Notes

This document describes how to integrate SimIndividual components into existing systems.

## AI Behavior Modules Integration

To wire `BehaviorTuning`, `PersonalityAxes`, `AlignmentTriplet`, and `MoraleState` into AI utility evaluation:

1. In `AIUtilityEvaluationSystem`, read `BehaviorTuning` component
2. Multiply utility scores by `BehaviorTuning` biases:
   - Combat utilities × `AggressionBias`
   - Social utilities × `SocialBias`
   - Resource utilities × `GreedBias`
   - Exploration utilities × `CuriosityBias`
   - Order-following utilities × `ObedienceBias`
3. Use `PersonalityAxes` for additional modifiers (e.g., `RiskTolerance` affects risk-taking decisions)
4. Use `MoraleState.Current` to modify utility scores (low morale reduces all utilities)
5. Use `AlignmentTriplet` for alignment-based utility modifiers

Example:
```csharp
if (SystemAPI.HasComponent<BehaviorTuning>(entity))
{
    var tuning = SystemAPI.GetComponent<BehaviorTuning>(entity);
    combatUtility *= tuning.AggressionBias;
    socialUtility *= tuning.SocialBias;
}
```

## Combat Loop Integration

To wire `InitiativeState` and `MoraleState` into CombatLoop:

1. Use `InitiativeState.Ready` flag to gate combat actions (only Ready entities can act)
2. Use `InitiativeState.Current` for turn order sorting
3. Use `MoraleState.Current` for cohesion calculations:
   - Low morale reduces cohesion
   - High morale increases cohesion
4. Use `MoraleState.Panic` to trigger mutiny checks (panic > threshold = mutiny risk)
5. Replace ad-hoc `InitiativeThreshold` with `InitiativeState.ActionCost`

Example:
```csharp
if (SystemAPI.HasComponent<InitiativeState>(entity))
{
    var initiative = SystemAPI.GetComponent<InitiativeState>(entity);
    if (!initiative.Ready) return; // Skip this entity
    
    // Use initiative.Current for turn order
    // Consume initiative after action
}
```

## Entity Hierarchy Integration

To wire `AllegianceEntry` and `AssetHolding` into ownership systems:

1. Query `AllegianceEntry` buffer to find all organizations an individual belongs to
2. Use `AllegianceEntry.OwnershipShare` for asset distribution calculations
3. Query `AssetHolding` buffer to find all assets owned by an individual
4. Use `AssetHolding.OwnershipShare` for inheritance calculations
5. Propagate ownership changes through `AllegianceEntry` links

Example:
```csharp
if (SystemAPI.HasBuffer<AllegianceEntry>(entity))
{
    var allegiances = SystemAPI.GetBuffer<AllegianceEntry>(entity);
    foreach (var allegiance in allegiances)
    {
        // Process organization membership
        // Use allegiance.OwnershipShare for asset calculations
    }
}
```

