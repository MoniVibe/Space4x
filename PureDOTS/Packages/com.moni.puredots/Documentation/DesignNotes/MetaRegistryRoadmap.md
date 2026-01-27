# Meta Registry Implementation Roadmap

## Overview

This document outlines the implementation plan for meta registries (`FactionRegistry`, `ClimateHazardRegistry`, `AreaEffectRegistry`, `CultureAlignmentRegistry`) that provide game-agnostic, high-level abstractions for faction management, global hazards, area effects, and cultural systems.

## Current State

**Stubs**: `PureDOTS/Packages/com.moni.puredots/Runtime/Runtime/MetaRegistryStubs.cs`
- Empty struct definitions for registries and entries
- Stub systems in `MetaRegistryStubSystems.cs` (disabled)

**Pattern Reference**: Existing registries (`ResourceRegistry`, `VillagerRegistry`, etc.) follow consistent patterns:
- Singleton component (`TRegistry : IComponentData`) with summary fields
- Entry buffer (`TRegistryEntry : IBufferElementData`) with per-entity data
- Registry system rebuilding entries every frame
- `RegistryMetadata` component for health monitoring
- Spatial sync integration (optional)

## Registry Schemas

### 1. FactionRegistry

**Purpose**: Track factions/empires, their territories, resources, and diplomatic state.

**Schema**:

```csharp
public struct FactionRegistry : IComponentData
{
    public int FactionCount;
    public int TotalTerritoryCells;
    public float TotalResources;
    public uint LastUpdateTick;
}

public struct FactionRegistryEntry : IBufferElementData, IRegistryEntry
{
    // Hot fields (frequently updated)
    public Entity FactionEntity;
    public float3 TerritoryCenter; // Weighted center of controlled cells
    public int CellId; // Primary territory cell
    public uint SpatialVersion;
    public uint LastMutationTick;
    
    // Cold fields (rarely updated)
    public ushort FactionId;
    public FixedString64Bytes FactionName;
    public byte FactionType; // Player, AI, Neutral, etc.
    public float ResourceStockpile;
    public int PopulationCount;
    public int TerritoryCellCount;
    public byte DiplomaticStatus; // Flags: AtWar, Allied, Neutral, etc.
    public FixedString128Bytes Description;
}
```

**Authoring**: `FactionAuthoring` MonoBehaviour → `FactionEntity` with `FactionId`, `FactionName`, etc.

**System**: `FactionRegistrySystem` queries entities with `FactionId` component, aggregates territory from spatial grid.

**Use Cases**:
- Godgame: Villager factions, divine alignment
- Space4X: Empires, colonies, diplomatic relations

### 2. ClimateHazardRegistry

**Purpose**: Track global climate hazards (storms, droughts, heat waves) affecting regions.

**Schema**:

```csharp
public struct ClimateHazardRegistry : IComponentData
{
    public int ActiveHazardCount;
    public float GlobalHazardIntensity; // 0-1 aggregate
    public uint LastUpdateTick;
}

public struct ClimateHazardRegistryEntry : IBufferElementData, IRegistryEntry
{
    // Hot fields
    public Entity HazardEntity;
    public float3 Position; // Center of hazard
    public int CellId;
    public uint SpatialVersion;
    public uint LastMutationTick;
    public float CurrentIntensity; // 0-1
    public uint ExpirationTick;
    
    // Cold fields
    public byte HazardType; // Storm, Drought, HeatWave, Blizzard, etc.
    public float Radius;
    public float MaxIntensity;
    public uint StartTick;
    public uint DurationTicks;
    public FixedString64Bytes HazardName;
    public byte AffectedEnvironmentChannels; // Bitmask: Moisture, Temperature, Wind, etc.
}
```

**Authoring**: `ClimateHazardAuthoring` → spawns hazard entities with `ClimateHazardState` component.

**System**: `ClimateHazardRegistrySystem` queries `ClimateHazardState` entities, integrates with `EnvironmentSystemGroup`.

**Use Cases**:
- Godgame: Divine weather effects, natural disasters
- Space4X: Space storms, asteroid impacts, radiation zones

### 3. AreaEffectRegistry

**Purpose**: Track area-based effects (buffs, debuffs, slow fields, time manipulation zones).

**Schema**:

```csharp
public struct AreaEffectRegistry : IComponentData
{
    public int ActiveEffectCount;
    public uint LastUpdateTick;
}

public struct AreaEffectRegistryEntry : IBufferElementData, IRegistryEntry
{
    // Hot fields
    public Entity EffectEntity;
    public float3 Position;
    public int CellId;
    public uint SpatialVersion;
    public uint LastMutationTick;
    public float CurrentStrength; // Modifier strength (0-1 or multiplier)
    public uint ExpirationTick;
    
    // Cold fields
    public byte EffectType; // Buff, Debuff, SlowField, TimeDilation, etc.
    public float Radius;
    public float MaxStrength;
    public Entity OwnerEntity; // Entity that created the effect
    public ushort EffectId; // Reference to effect catalog
    public byte AffectedArchetypes; // Bitmask: Villagers, Resources, Creatures, etc.
    public FixedString64Bytes EffectName;
}
```

**Authoring**: `AreaEffectAuthoring` → spawns effect entities with `AreaEffectState` component.

**System**: `AreaEffectRegistrySystem` queries `AreaEffectState` entities, integrates with affected systems (villager AI, movement, etc.).

**Use Cases**:
- Godgame: Miracle auras, blessing zones
- Space4X: Shield generators, repair fields, sensor jammers

### 4. CultureAlignmentRegistry

**Purpose**: Track cultural/alignment state (villager loyalty, faction affinity, ideological shifts).

**Schema**:

```csharp
public struct CultureAlignmentRegistry : IComponentData
{
    public int CultureCount;
    public float GlobalAlignmentScore; // -1 to 1 aggregate
    public uint LastUpdateTick;
}

public struct CultureAlignmentRegistryEntry : IBufferElementData, IRegistryEntry
{
    // Hot fields
    public Entity CultureEntity; // May be per-village or global
    public float3 RegionCenter;
    public int CellId;
    public uint SpatialVersion;
    public uint LastMutationTick;
    public float CurrentAlignment; // -1 (hostile) to 1 (loyal)
    public float AlignmentVelocity; // Rate of change per tick
    
    // Cold fields
    public ushort CultureId;
    public FixedString64Bytes CultureName;
    public byte CultureType; // Tribal, Religious, Political, etc.
    public int MemberCount; // Villagers/entities belonging to this culture
    public float BaseAlignment;
    public byte AlignmentFlags; // Flags: Shifting, Stable, Volatile, etc.
    public FixedString128Bytes Description;
}
```

**Authoring**: `CultureAuthoring` → spawns culture entities with `CultureState` component.

**System**: `CultureAlignmentRegistrySystem` queries `CultureState` entities, integrates with villager morale systems.

**Use Cases**:
- Godgame: Villager loyalty, divine alignment, faction tensions
- Space4X: Colony loyalty, empire cohesion, ideological conflicts

## Implementation Phases

### Phase 1: Foundation (Week 1-2)

**Tasks**:
1. Define component structs for each registry (`FactionState`, `ClimateHazardState`, `AreaEffectState`, `CultureState`)
2. Create entry structs following hot/cold split pattern (see `Docs/DesignNotes/RegistryHotColdSplits.md`)
3. Add registry singleton components to `CoreSingletonBootstrapSystem`
4. Create stub registry systems (enabled but empty logic)

**Deliverables**:
- `Runtime/Runtime/MetaRegistries/` directory with component definitions
- Updated `CoreSingletonBootstrapSystem` with meta registry singletons
- Stub systems in `Systems/MetaRegistries/`

**Dependencies**: None

### Phase 2: FactionRegistry (Week 3-4)

**Tasks**:
1. Implement `FactionRegistrySystem` following `ResourceRegistrySystem` pattern
2. Add `FactionAuthoring` MonoBehaviour and baker
3. Integrate with spatial grid (territory tracking)
4. Add tests (`FactionRegistryTests.cs`)

**Deliverables**:
- Functional `FactionRegistrySystem`
- Authoring support
- Integration tests

**Status:** ✅ Implemented. Authoring lives in `Runtime/Authoring/Meta/FactionAuthoring.cs` with optional `FactionProfileAsset`. Deterministic coverage available via `MetaRegistryTests.FactionRegistry_TracksFactionEntities`.

**Dependencies**: Phase 1

### Phase 3: ClimateHazardRegistry (Week 5-6)

**Tasks**:
1. Implement `ClimateHazardRegistrySystem`
2. Integrate with `EnvironmentSystemGroup` (hazards affect moisture/temperature grids)
3. Add `ClimateHazardAuthoring` and spawn/expiration logic
4. Add tests

**Deliverables**:
- Functional `ClimateHazardRegistrySystem`
- Environment integration
- Tests

**Status:** ✅ Implemented. `ClimateHazardAuthoring` + `ClimateHazardProfileAsset` seed hazards; integration tests cover registry rebuild determinism.

**Dependencies**: Phase 1, `EnvironmentSystemGroup` stable

### Phase 4: AreaEffectRegistry (Week 7-8)

**Tasks**:
1. Implement `AreaEffectRegistrySystem`
2. Integrate with affected systems (villager movement, AI, etc.)
3. Add `AreaEffectAuthoring` and effect application logic
4. Add tests

**Deliverables**:
- Functional `AreaEffectRegistrySystem`
- System integrations
- Tests

**Status:** ✅ Implemented. Area effect authoring and profiles land in `Runtime/Authoring/Meta/`. Tests validate registry buffers and metrics.

**Dependencies**: Phase 1, `VillagerSystemGroup` stable

### Phase 5: CultureAlignmentRegistry (Week 9-10)

**Tasks**:
1. Implement `CultureAlignmentRegistrySystem`
2. Integrate with villager morale/alignment systems
3. Add `CultureAuthoring` and alignment calculation logic
4. Add tests

**Deliverables**:
- Functional `CultureAlignmentRegistrySystem`
- Villager integration
- Tests

**Status:** ✅ Implemented via `CultureAuthoring` / `CultureProfileAsset`. Integration test ensures alignment metrics populate the registry.

**Dependencies**: Phase 1, `VillagerSystemGroup` stable

### Phase 6: Integration & Polish (Week 11-12)

**Tasks**:
1. Add registry health monitoring for all meta registries
2. Integrate with `RegistryDirectory` and instrumentation
3. Add spatial sync validation
4. Documentation and examples

**Deliverables**:
- Complete integration
- Documentation
- Example scenes

**Status:** 🔄 In Progress. HUD/telemetry now surface faction/hazard/effect/culture metrics (see `DebugDisplaySystem` Phase 2 update). Sample assets can be generated via **PureDOTS ➜ Samples ➜ Create Meta Registry Samples**.

**Dependencies**: All previous phases

## Content Neutrality

**Guidelines**:
- Registry entry fields use generic names (`FactionId`, `HazardType`, `EffectType`) not game-specific terms
- Authoring components allow designers to define game-specific values via catalogs
- Systems are content-agnostic; game-specific logic lives in game bridges

**Example**: `FactionType` is a `byte` enum that games define (e.g., Godgame: `Divine`, `Mortal`, `Neutral`; Space4X: `Empire`, `Pirate`, `Trader`)

## Migration Path

**For Existing Games**:
1. Games using custom faction/hazard systems should migrate to meta registries
2. Authoring components map existing data to registry entries
3. Systems query registries instead of custom lookups

**Breaking Changes**: None (meta registries are additive)

## Testing Strategy

**Unit Tests**:
- Registry rebuild determinism
- Entry sorting and spatial sync

**Integration Tests**:
- Faction territory tracking
- Hazard environment integration
- Effect application to entities
- Culture alignment calculations

**Performance Tests**:
- Registry rebuild times (<0.5ms per registry)
- Spatial query performance with meta registries

## References

- `Docs/DesignNotes/RegistryHotColdSplits.md` - Registry data layout guidelines
- `Docs/DesignNotes/RegistryLifecycle.md` - Deterministic rebuild strategy
- `Runtime/Systems/ResourceRegistrySystem.cs` - Reference implementation
- `Runtime/Systems/MetaRegistryStubSystems.cs` - Current stubs

