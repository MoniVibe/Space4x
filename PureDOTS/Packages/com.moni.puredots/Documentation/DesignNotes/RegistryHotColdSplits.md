# Registry Hot/Cold Splits

## Overview

Registries use DOTS-friendly layouts with hot (frequently updated) vs. cold (rarely updated) field splits to keep main chunks lean and improve cache performance.

## Hot Fields (Frequently Updated)

These fields are updated every frame or multiple times per frame:
- **Entity references** (`Entity SourceEntity`, `Entity TargetEntity`) - Used for queries and lookups
- **Position** (`float3 Position`) - Updated when entities move
- **State values** (`float UnitsRemaining`, `byte ActiveTickets`, `byte ClaimFlags`) - Updated during gameplay
- **Spatial metadata** (`int CellId`, `uint SpatialVersion`) - Updated when spatial grid rebuilds
- **Timestamps** (`uint LastMutationTick`, `uint LastUpdateTick`) - Updated for determinism tracking

## Cold Fields (Rarely Updated)

These fields change infrequently:
- **Type indices** (`ushort ResourceTypeIndex`, `ushort VillagerTypeIndex`) - Set at spawn, rarely changes
- **Config data** (`float Capacity`, `float GatherRate`) - Set from authoring, rarely changes
- **Metadata** (`ushort FamilyIndex`, `ResourceTier Tier`) - Derived from catalog, stable
- **Registry aggregates** (`int TotalResources`, `float TotalCapacity`) - Computed during rebuild

## Layout Guidelines

1. **Hot fields first**: Place frequently accessed fields at the start of structs
2. **Pack efficiently**: Use `byte`, `ushort` where possible to reduce size
3. **Separate concerns**: Keep hot/cold data in separate components when appropriate
4. **Cache-friendly**: Group related hot fields together for better cache locality

## Current Registry Entry Layouts

### ResourceRegistryEntry
```csharp
public struct ResourceRegistryEntry : IBufferElementData
{
    // Hot fields (updated frequently)
    public Entity SourceEntity;         // Entity reference
    public float3 Position;             // Position (updated on move)
    public float UnitsRemaining;        // State (updated during gathering)
    public byte ActiveTickets;          // Reservations (updated during job assignment)
    public byte ClaimFlags;             // Flags (updated during interactions)
    public uint LastMutationTick;       // Timestamp (updated on mutations)
    public int CellId;                  // Spatial cell (updated on grid rebuild)
    public uint SpatialVersion;         // Spatial version (updated on grid rebuild)
    
    // Cold fields (rarely updated)
    public ushort ResourceTypeIndex;    // Type (set at spawn)
    public ushort FamilyIndex;          // Family (derived from catalog)
    public ResourceTier Tier;           // Tier (derived from catalog)
}
```

### StorehouseRegistryEntry
```csharp
public struct StorehouseRegistryEntry : IBufferElementData
{
    // Hot fields
    public Entity StorehouseEntity;      // Entity reference
    public float3 Position;             // Position
    public float TotalStored;            // Current inventory (updated frequently)
    public byte ActiveTickets;         // Reservations
    public uint LastMutationTick;       // Timestamp
    public int CellId;                  // Spatial cell
    public uint SpatialVersion;         // Spatial version
    
    // Cold fields
    public float TotalCapacity;         // Capacity (set from config)
    public ushort MaxResourceTypes;      // Config (set from authoring)
}
```

### VillagerRegistryEntry
```csharp
public struct VillagerRegistryEntry : IBufferElementData
{
    // Hot fields
    public Entity VillagerEntity;        // Entity reference
    public float3 Position;             // Position (updated every frame)
    public byte AvailabilityFlags;       // Idle/Reserved/InCombat (updated frequently)
    public ushort CurrentJobType;        // Job type (updated on assignment)
    public byte JobPhase;                // Job phase (updated during execution)
    public uint LastMutationTick;        // Timestamp
    public int CellId;                   // Spatial cell
    public uint SpatialVersion;         // Spatial version
    
    // Cold fields
    public ushort VillagerId;            // ID (set at spawn)
    public ushort FactionId;             // Faction (set at spawn)
    public byte DisciplineArchetype;     // Discipline (set at spawn, rarely changes)
    public byte MoraleTier;              // Morale (updated less frequently than position)
}
```

## Performance Considerations

- **Hot fields**: Aim for < 64 bytes per entry to fit in cache lines
- **Cold fields**: Can be larger, accessed less frequently
- **Separate buffers**: Consider splitting hot/cold into separate buffers if hot fields become too large
- **Query patterns**: Design queries to access hot fields primarily, minimize cold field access

## Future Optimizations

- Split hot/cold into separate components when entry size exceeds 64 bytes
- Use `SharedComponentData` for truly cold config data shared across many entities
- Consider `BlobAssetReference` for large lookup tables (type catalogs, recipe sets)


