# SoA (Structure of Arrays) Expectations

## Overview

PureDOTS systems must use SoA-friendly component layouts to maximize cache performance and enable efficient Burst-compiled jobs. This document defines layout rules, alignment requirements, and chunk sizing guidelines for all high-volume systems.

## Core Principles

### 1. Structure of Arrays Layout

DOTS ECS inherently uses SoA layout: components are stored in separate arrays per chunk. Design component structs to take advantage of this:

- **Small, tightly-packed structs**: Keep component structs small (< 128 bytes preferred, < 64 bytes ideal for hot components)
- **Cache-line alignment**: Align frequently accessed fields to 16-byte boundaries where practical
- **Minimize padding**: Use `byte`, `ushort` where values fit to reduce wasted space
- **Hot fields first**: Place frequently accessed fields at the start of structs (see `RegistryHotColdSplits.md`)

### 2. Chunk Size Targets

DOTS chunks hold up to 16KB of component data. Target archetype sizes to maximize entities per chunk:

- **Hot simulation archetypes**: ≤ 96-128 bytes per entity → ~125-170 entities per chunk
- **Cold/presentation archetypes**: ≤ 60-80 bytes per entity → ~200-250 entities per chunk
- **Registry buffers**: Entries should be ≤ 64 bytes for hot fields, larger cold fields acceptable

### 3. Alignment Guidelines

- **16-byte alignment**: Prefer `float3`, `quaternion`, `float4` types (16 bytes) for hot fields
- **8-byte alignment**: Use `float`, `int`, `uint`, `double` for 8-byte boundaries
- **4-byte alignment**: `short`, `ushort` for 4-byte boundaries
- **1-byte packing**: Use `byte`, `sbyte`, `bool` to minimize padding (but avoid fragmentation)

## High-Volume System Requirements

### Villagers

**Target**: ≤ 128 bytes per entity (hot archetype), ~125 entities per chunk

**Hot Components** (updated every frame):
- `LocalTransform` (24 bytes) - Position, rotation, scale
- `VillagerMovement` (~32 bytes) - Velocity, speed, rotation
- `VillagerAIState` (32 bytes) - Current state, target, flags
- `VillagerNeeds` (20 bytes) - Hunger, energy, morale (can clamp to shorts if needed)
- `VillagerJob` (16 bytes) - Job assignment, phase
- `VillagerAvailability` (16 bytes) - Flags, timestamps

**Cold Components** (move to companion entity):
- `VillagerStats` - Analytics data
- `VillagerAnimationState` - Presentation state
- `VillagerMemoryEvent` buffer - Event history
- `VillagerInventoryItem` buffer - Inventory items

**SoA Refactor Checklist**:
- [ ] Replace `VillagerNeeds` floats with `short`/`ushort` if precision allows
- [ ] Move `VillagerMood` to cold archetype if not consumed every tick
- [ ] Split inventory buffer to companion entity (use `VillagerInventoryRef` index on hot archetype)
- [ ] Consolidate tags into packed `VillagerFlags` component

**Reference**: `Docs/DesignNotes/VegetationLifecycleAndChunks.md` (Villagers section)

### Resources

**Target**: ≤ 64 bytes per registry entry (hot fields)

**Hot Fields** (frequently updated):
- `Entity SourceEntity` (8 bytes)
- `float3 Position` (12 bytes + 4 padding = 16 bytes)
- `float UnitsRemaining` (4 bytes)
- `byte ActiveTickets` (1 byte)
- `byte ClaimFlags` (1 byte)
- `uint LastMutationTick` (4 bytes)
- `int CellId` (4 bytes)
- `uint SpatialVersion` (4 bytes)

**Cold Fields** (rarely updated):
- `ushort ResourceTypeIndex` (2 bytes)
- `ushort FamilyIndex` (2 bytes)
- `ResourceTier Tier` (enum, typically 1 byte)

**Total**: ~48 bytes hot + ~5 bytes cold = ~53 bytes per entry

**Reference**: `Docs/DesignNotes/RegistryHotColdSplits.md`

### Vegetation

**Target**: ≤ 96 bytes per entity (hot archetype), ~170 entities per chunk

**Hot Components**:
- `LocalTransform` (24 bytes)
- `VegetationId` (8 bytes)
- `VegetationLifecycle` (24 bytes)
- `VegetationHealth` (24 bytes) - Move to cold if updates infrequent
- `VegetationProduction` (~32 bytes after refactor - replace `FixedString64Bytes` with `ushort ResourceTypeIndex`)
- `VegetationReproduction` (24 bytes) - Only for spreading species
- `VegetationSeasonal` (16 bytes)
- Packed `VegetationFlags` (8 bytes) - Consolidate stage tags

**Cold Components** (companion entity):
- `VegetationHistoryEvent` buffer
- `VegetationSeedDrop` buffer
- LOD and FX tags
- `LinkedEntityGroup`

**SoA Refactor Checklist**:
- [ ] Replace `VegetationProduction.ResourceId` string with `ushort ResourceTypeIndex` (halves footprint)
- [ ] Consolidate stage tags into `VegetationFlags` component
- [ ] Move `VegetationHealth` to cold archetype if updates are infrequent

**Reference**: `Docs/DesignNotes/VegetationLifecycleAndChunks.md` (Vegetation section)

### Environment Grids

**Target**: Blob assets with contiguous arrays for cache-friendly access

**Layout**:
- Store grid data in blob assets (`MoistureGridBlob`, `TemperatureGridBlob`, etc.)
- Use `BlobArray<float>` for scalar grids
- Use `BlobArray<Struct>` for vector/packed data
- Access via `ref var grid = ref blob.Value;` to avoid copies

**Runtime Buffers**:
- `MoistureGridRuntimeCell` buffer - Hot runtime state
- `SunlightGridRuntimeSample` buffer - Hot runtime state
- Keep runtime buffers ≤ 16 bytes per cell for cache efficiency

**Reference**: `Runtime/Runtime/Environment/EnvironmentGrids.cs`

### Spatial Grid

**Target**: SoA arrays for efficient queries

**Layout**:
- `NativeArray<SpatialGridCellRange>` - Cell metadata
- `NativeArray<SpatialGridEntry>` - Entity entries (position + entity reference)
- `NativeArray<float3>` - Position cache (optional, for dirty detection)

**Entry Size**: `SpatialGridEntry` should be ≤ 32 bytes (Entity + float3 + metadata)

**Reference**: `Runtime/Runtime/Spatial/SpatialGridComponents.cs`

## Component Design Patterns

### Pattern 1: Hot/Cold Split

Split frequently updated fields from rarely updated fields:

```csharp
// Hot component (updated every frame)
public struct VillagerMovement : IComponentData
{
    public float3 Position;      // 12 bytes + 4 padding
    public float3 Velocity;      // 12 bytes + 4 padding
    public float Speed;          // 4 bytes
    public quaternion Rotation;  // 16 bytes
    // Total: 52 bytes
}

// Cold component (updated infrequently)
public struct VillagerConfig : IComponentData
{
    public float MaxSpeed;       // 4 bytes
    public float Acceleration;  // 4 bytes
    public ushort ArchetypeId;   // 2 bytes
    // Total: 10 bytes
}
```

### Pattern 2: Packed Flags

Consolidate multiple boolean flags into bitfields:

```csharp
public struct VillagerFlags : IComponentData
{
    private byte _flags;
    
    public bool IsIdle { get => (_flags & 0x01) != 0; set => _flags = (byte)(value ? _flags | 0x01 : _flags & ~0x01); }
    public bool IsWorking { get => (_flags & 0x02) != 0; set => _flags = (byte)(value ? _flags | 0x02 : _flags & ~0x02); }
    public bool IsFleeing { get => (_flags & 0x04) != 0; set => _flags = (byte)(value ? _flags | 0x04 : _flags & ~0x04); }
    // ... more flags
    
    // Total: 1 byte instead of multiple 16-byte tag components
}
```

### Pattern 3: Index References

Use indices instead of direct references for cold data:

```csharp
// Hot component
public struct VillagerInventoryRef : IComponentData
{
    public int InventoryBufferIndex;  // 4 bytes - index into companion entity's buffer
}

// Cold component (on companion entity)
public struct VillagerInventory : IBufferElementData
{
    public ushort ResourceTypeIndex;
    public float Amount;
}
```

### Pattern 4: Blob Asset Lookups

Use blob assets for large, read-only lookup tables:

```csharp
// Component holds reference (8 bytes)
public struct ResourceTypeIndex : IComponentData
{
    public BlobAssetReference<ResourceTypeIndexBlob> Catalog;  // 8 bytes
}

// Blob asset holds actual data (shared across all entities)
public struct ResourceTypeIndexBlob
{
    public BlobArray<FixedString64Bytes> Ids;
    public BlobArray<FixedString128Bytes> DisplayNames;
    public BlobArray<float4> Colors;
}
```

## Alignment Examples

### Good Alignment (16-byte boundaries)

```csharp
public struct WellAlignedComponent : IComponentData
{
    public float3 Position;      // 0-11 bytes (aligned to 16)
    public float4 Color;         // 16-31 bytes (aligned to 16)
    public quaternion Rotation;  // 32-47 bytes (aligned to 16)
    // Total: 48 bytes, all fields 16-byte aligned
}
```

### Poor Alignment (padding waste)

```csharp
public struct PoorlyAlignedComponent : IComponentData
{
    public byte Flag1;           // 0 bytes
    public float Value1;          // 4-7 bytes (4 bytes padding before)
    public byte Flag2;            // 8 bytes
    public float Value2;          // 12-15 bytes (3 bytes padding before)
    // Total: 16 bytes, but 7 bytes wasted on padding
}
```

### Better Alignment (packed)

```csharp
public struct BetterAlignedComponent : IComponentData
{
    public byte Flag1;            // 0 bytes
    public byte Flag2;             // 1 byte
    public ushort Padding;        // 2-3 bytes (explicit padding)
    public float Value1;          // 4-7 bytes (no padding needed)
    public float Value2;           // 8-11 bytes
    // Total: 12 bytes, minimal padding
}
```

## Memory Budgets

### Target Memory Footprints (100k entities)

- **Villagers (40k)**:
  - Hot archetype: ~4.5 MB (after SoA refactor)
  - Cold archetype: ~8-10 MB (buffers, relationships)
  
- **Vegetation (60k)**:
  - Hot archetype: ~5.8 MB (after SoA refactor)
  - Cold archetype: ~3 MB (history, seeds)
  
- **Resources (variable)**:
  - Registry entries: ~53 bytes × count
  - Target: < 1 MB for 10k resources
  
- **Environment Grids**:
  - Blob assets: ~1-2 MB per grid (shared)
  - Runtime buffers: ~16 bytes × cell count
  
- **Spatial Grid**:
  - Cell ranges: ~16 bytes × cell count
  - Entries: ~32 bytes × entity count
  - Target: < 5 MB for 100k entities

**Total Target**: ~25-30 MB for 100k entities (hot + cold archetypes)

## Burst Compatibility

All SoA layouts must be Burst-compatible:

- **No managed types**: Avoid `string`, `object`, delegates, generics with managed constraints
- **Blittable structs**: All fields must be primitive types or other blittable structs
- **No pointers to managed memory**: Use `NativeArray`, `BlobAssetReference` instead
- **Fixed-size arrays**: Use `FixedString` types or blob arrays, not `List<T>`

## Validation Checklist

When designing new components for high-volume systems:

- [ ] Component size ≤ 128 bytes (hot) or ≤ 64 bytes (ideal)
- [ ] Hot fields placed first, aligned to 16-byte boundaries where practical
- [ ] Cold fields moved to separate component or companion entity
- [ ] Flags consolidated into packed bitfields (≤ 8 bytes)
- [ ] Strings replaced with indices into blob catalogs
- [ ] Buffers moved to companion entities when > 32 bytes per element
- [ ] Blob assets used for large read-only lookup tables
- [ ] Alignment verified (no excessive padding)
- [ ] Burst compatibility confirmed (no managed types)

## Cross-References

- `Docs/DesignNotes/RegistryHotColdSplits.md` - Registry-specific hot/cold guidelines
- `Docs/DesignNotes/VegetationLifecycleAndChunks.md` - Memory budgets and chunk layouts
- `Docs/DesignNotes/PresentationBridgeContracts.md` - Hot/cold archetype separation
- `Docs/TruthSources/PlatformPerformance_TruthSource.md` - Burst and IL2CPP guidelines
- `Docs/TODO/SystemIntegration_TODO.md` - SoA adoption tasks

## Future Optimizations

- **AOSOA (Array of Structure of Arrays)**: For extremely high-volume systems (>100k entities), consider manual SoA layout with separate `NativeArray<float3>`, `NativeArray<float>` for positions/values
- **SIMD-friendly layouts**: Group related float values (e.g., `float4` for RGBA) to enable SIMD operations
- **Chunk-level metadata**: Store chunk-level aggregates (e.g., total capacity) to avoid per-entity scans


