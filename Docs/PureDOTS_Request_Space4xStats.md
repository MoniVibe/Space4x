# PureDOTS Engine Request: Space4x Stat Simulation Support

**Date**: 2025-01-XX  
**Status**: Request for Engine Features  
**Priority**: High (Core Progression System)

## Summary

Space4x requires PureDOTS engine features to fully simulate its stat-driven progression system. Individual entities (captains, officers, crew) have rich stat profiles (`IndividualStats`, `PhysiqueFinesseWill`, `ExpertiseEntry`, `ServiceTrait`, `PreordainProfile`) that influence gameplay systems, but several engine-level capabilities are needed to make this simulation complete, deterministic, and rewind-compatible.

## Space4x Stat Simulation Goals

### Current Implementation
- **IndividualStats**: Command, Tactics, Logistics, Diplomacy, Engineering, Resolve (0-100 scale)
- **PhysiqueFinesseWill**: Physique, Finesse, Will with inclinations (1-10) and GeneralXP pool
- **ExpertiseEntry Buffer**: CarrierCommand, Espionage, Logistics, Psionic, Beastmastery with tiers (0-255)
- **ServiceTrait Buffer**: ReactorWhisperer, StrikeWingMentor, TacticalSavant, LogisticsMaestro, PirateBane
- **PreordainProfile**: CombatAce, LogisticsMaven, DiplomaticEnvoy, EngineeringSavant career tracks

### Stat Influence Targets
- **Command**: Max pilots/crews, morale bonuses, command point replenishment (mana-like), formation coordination
- **Tactics**: Special ability count/cooldowns, targeting accuracy, engagement timing
- **Logistics**: Cargo transfer speed, utility vessel speeds, dock/hangar throughput
- **Diplomacy**: Agreement success rates, relation modifiers, interception/stance decisions
- **Engineering**: Repair/refit speeds, costs, jam chance reduction, system complexity reduction
- **Resolve**: Morale thresholds, recall thresholds, action speed, risk tolerance
- **Physique/Finesse/Will**: Strike craft performance, crew task efficiency, psionic abilities

## Required PureDOTS Features

### 1. Scenario Runner Stat Seeding

**Requirement**: Ability to seed individual entity stats from scenario JSON.

**Current Gap**: ScenarioRunner can spawn entities but doesn't support stat initialization from JSON.

**Requested API**:
```csharp
// In scenario JSON:
{
  "entities": {
    "captain_01": {
      "archetype": "Space4X.Captain",
      "stats": {
        "command": 75,
        "tactics": 60,
        "logistics": 50,
        "diplomacy": 80,
        "engineering": 45,
        "resolve": 70
      },
      "physique": {
        "physique": 65,
        "finesse": 70,
        "will": 55,
        "physiqueInclination": 7,
        "finesseInclination": 8,
        "willInclination": 5
      },
      "expertise": [
        {"type": "CarrierCommand", "tier": 5},
        {"type": "Logistics", "tier": 3}
      ],
      "traits": ["ReactorWhisperer", "TacticalSavant"]
    }
  }
}
```

**Implementation Notes**:
- Extend `ScenarioRunner` spawner to accept stat dictionaries
- Map JSON stat keys to component fields via reflection or explicit mapping
- Support nested stat structures (IndividualStats, PhysiqueFinesseWill)
- Support buffer initialization (ExpertiseEntry, ServiceTrait)

**Priority**: High (needed for deterministic scenario testing)

---

### 2. Registry Continuity for Stat Progression

**Requirement**: Stat progression (XP accumulation, stat increases) must be rewind-compatible and persist across registry snapshots.

**Current Gap**: Registry snapshots don't explicitly track stat progression state. XP pools and stat modifications need deterministic replay.

**Requested Features**:
- **Stat History Buffer**: Track stat changes over time for rewind replay
  ```csharp
  public struct StatHistorySample : IBufferElementData
  {
      public uint Tick;
      public half Command;
      public half Tactics;
      // ... other stats
      public float GeneralXP;
  }
  ```
- **XP Accumulation Log**: Command log entries for XP gains/spending
  ```csharp
  public struct StatXPCommandLogEntry : IBufferElementData, ICommandLogEntry
  {
      public uint Tick;
      public Entity TargetEntity;
      public StatType StatType; // Command, Tactics, etc. or Physique/Finesse/Will
      public float XPAmount;
      public StatXPChangeType ChangeType; // Gain, Spend, Reset
  }
  ```
- **Registry Snapshot Integration**: Include stat progression state in registry continuity snapshots

**Priority**: High (required for rewind determinism)

---

### 3. Presentation Bindings for Stat Display

**Requirement**: Telemetry-driven presentation bindings for stat displays (HUD, character sheets, fleet rosters).

**Current Gap**: Presentation system can bind to telemetry metrics but doesn't have structured stat display components.

**Requested Features**:
- **Stat Display Component**: Structured component for UI stat displays
  ```csharp
  public struct StatDisplayBinding : IComponentData
  {
      public FixedString64Bytes EntityId; // Reference to entity with stats
      public StatDisplayMode Mode; // Current, Max, Average, Trend
      public StatType[] VisibleStats; // Which stats to display
  }
  ```
- **Stat Telemetry Aggregation**: Aggregate stat metrics for fleet/aggregate displays
  ```csharp
  // Telemetry keys:
  // space4x.stats.command.avg (fleet average)
  // space4x.stats.command.max (fleet maximum)
  // space4x.stats.command.min (fleet minimum)
  // space4x.stats.command.trend (improving/declining)
  ```

**Priority**: Medium (UI polish, not blocking gameplay)

---

### 4. Telemetry Endpoints for Stat Influence

**Requirement**: Telemetry system must support stat influence metrics (how stats affect gameplay outcomes).

**Current Gap**: Telemetry can publish metrics but doesn't have structured support for "stat influence" events.

**Requested Features**:
- **Stat Influence Events**: Structured telemetry for stat-driven outcomes
  ```csharp
  // Example telemetry keys:
  // space4x.stats.commandInfluence.formationRadius (how command affects formation)
  // space4x.stats.tacticsInfluence.targetingAccuracy (how tactics affects targeting)
  // space4x.stats.logisticsInfluence.transferSpeed (how logistics affects transfers)
  // space4x.stats.engineeringInfluence.repairSpeed (how engineering affects repair)
  // space4x.stats.resolveInfluence.engagementTime (how resolve affects engagement)
  ```
- **Stat Modifier Tracking**: Track stat modifiers from traits/expertise/augmentations
  ```csharp
  public struct StatModifierTelemetry : IBufferElementData
  {
      public FixedString64Bytes EntityId;
      public StatType StatType;
      public float BaseValue;
      public float ModifiedValue;
      public FixedString64Bytes ModifierSource; // "ServiceTrait.ReactorWhisperer", "Expertise.CarrierCommand", etc.
  }
  ```

**Priority**: Medium (tuning/debugging support)

---

### 5. Rewind Guarantees for Stat-Driven Outcomes

**Requirement**: Stat-driven gameplay outcomes must replay identically during rewind.

**Current Gap**: Stat modifiers use floating-point math that may have non-deterministic behavior. Need guarantees that stat calculations are deterministic.

**Requested Features**:
- **Deterministic Stat Math**: Ensure stat calculations use deterministic floating-point operations
- **Stat Calculation Logging**: Log stat calculations to command log for replay verification
- **Stat State Snapshots**: Include stat state in rewind snapshots

**Implementation Notes**:
- Use `half` types for stats (already done) to reduce precision issues
- Ensure stat lookups are deterministic (entity order, component access order)
- Verify stat modifiers are applied in consistent order

**Priority**: High (required for rewind determinism)

---

### 6. Aggregate Stat Queries

**Requirement**: Ability to query aggregate stat values (fleet average command, max tactics, etc.) efficiently.

**Current Gap**: No built-in support for aggregating stats across entity groups.

**Requested Features**:
- **Stat Aggregation System**: System that computes aggregate stats for entity groups
  ```csharp
  public struct FleetStatAggregate : IComponentData
  {
      public half AvgCommand;
      public half MaxCommand;
      public half MinCommand;
      public half AvgTactics;
      // ... other aggregates
  }
  ```
- **Efficient Queries**: Use spatial queries or registry lookups to compute aggregates
- **Update Frequency Control**: Configurable update frequency (every N ticks) to avoid performance issues

**Priority**: Medium (performance optimization, not blocking)

---

### 7. Trait/Contract Persistence

**Requirement**: Service traits and contracts must persist across registry snapshots and rewind.

**Current Gap**: Traits and contracts are stored in buffers but may not be properly snapshotted.

**Requested Features**:
- **Trait Snapshot Support**: Ensure `ServiceTrait` buffer is included in registry snapshots
- **Contract Component**: Structured contract component for service agreements
  ```csharp
  public struct ServiceContract : IComponentData
  {
      public FixedString64Bytes EmployerId; // Fleet, manufacturer, guild
      public ContractType Type; // Fleet, Manufacturer, MercenaryGuild
      public uint StartTick;
      public uint DurationTicks; // 1-5 years in ticks
      public uint ExpiryTick;
      public byte IsActive;
  }
  ```
- **Contract History**: Track contract changes for rewind replay

**Priority**: Medium (narrative/progression feature)

---

## Blocking Gaps

### Critical (Must Have)
1. **Scenario Runner Stat Seeding**: Cannot test stat-driven scenarios without manual entity setup
2. **Registry Continuity for Stats**: Stat progression breaks rewind determinism
3. **Rewind Guarantees**: Stat calculations must be deterministic

### Important (Should Have)
4. **Telemetry Endpoints**: Need stat influence metrics for tuning
5. **Aggregate Stat Queries**: Performance optimization for fleet-level stat displays

### Nice to Have (Future)
6. **Presentation Bindings**: UI polish for stat displays
7. **Trait/Contract Persistence**: Advanced narrative features

## Suggested Milestones

### Milestone 1: Core Stat Support (Week 1-2)
- [ ] Scenario Runner stat seeding from JSON
- [ ] Registry continuity for stat progression
- [ ] Rewind guarantees for stat calculations

### Milestone 2: Telemetry & Aggregation (Week 3-4)
- [ ] Stat influence telemetry endpoints
- [ ] Aggregate stat query system
- [ ] Stat modifier tracking

### Milestone 3: Presentation & Contracts (Week 5-6)
- [ ] Stat display presentation bindings
- [ ] Contract persistence system
- [ ] Trait history tracking

## Owners & Coordination

**Space4x Team**: Implements stat-driven gameplay systems, creates request doc, tests engine features  
**PureDOTS Team**: Implements engine features, provides API documentation, coordinates with Space4x on testing

## Testing Requirements

- **Determinism Tests**: Verify stat calculations replay identically during rewind
- **Scenario Tests**: Verify stat seeding from JSON produces expected entity states
- **Performance Tests**: Verify aggregate stat queries don't impact frame time
- **Telemetry Tests**: Verify stat influence metrics are published correctly

## References

- `Docs/PrefabMaker_Requirements_Assessment.md` - Stat system requirements
- `Docs/Conceptualization/Mechanics/AceOfficerProgression.md` - Stat progression design
- `Assets/Scripts/Space4x/Registry/ModuleDataSchemas.cs` - Stat component definitions
- `Assets/Scripts/Space4x/Systems/AI/*` - Stat-driven gameplay systems

