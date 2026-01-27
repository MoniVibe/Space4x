# Resource Registry Plan - DOTS-Native Implementation

## Overview

Replace legacy resource/storehouse registries with DOTS-native singletons and buffers to provide clean, queryable data for the simulation. This eliminates EntityManager-based lookups and provides efficient indexed access to resources and storehouses. Keep behaviour aligned with `Docs/TruthSources/RuntimeLifecycle_TruthSource.md`, `Docs/TODO/SystemIntegration_TODO.md`, and honour rewind guidance in `Docs/DesignNotes/RewindPatterns.md`.

## Current State Analysis

### Existing Patterns
- **Resource IDs**: Currently use `FixedString64Bytes` via `ResourceTypeId.Value` component
- **Storehouse Access**: Static `StorehouseAPI` class with manual `EntityManager` queries *(legacy; removed in favour of registry buffers)*
- **Resource Types**: `ResourceTypeCatalog` ScriptableObject exists but lacks a baker to DOTS data
- **Query Pattern**: Systems query entities directly via `EntityQuery` and component lookups

### Problems Addressed
1. No centralized resource type catalog available at runtime
2. StorehouseAPI uses EntityManager (not Burst-compatible, requires main thread) *(resolved by removing the helper and reading reservation components directly)*
3. No efficient index for looking up resources by type or storehouses by capacity
4. Resource type validation happens only at authoring time

## DOTS Data Layout

### 1. ResourceTypeIndex (Blob Asset)

Maps human-readable resource type IDs (`FixedString64Bytes`) to compact `ushort` indices for efficient storage and comparison.

```csharp
FixedString64Bytes id;              // Human-readable ID (e.g., "Wood", "Stone")
BlobString displayName;     // UI display name
BlobArray<byte> color;      // RGBA color for UI/debug viz
```

**Baker Input**: `ResourceTypeCatalog` ScriptableObject entries
**Baker Output**: `BlobAssetReference<ResourceTypeIndex>` singleton component
**Index Type**: `ushort` (supports up to 65,535 resource types)

### 2. ResourceRegistry (Singleton Component + Buffer)

Provides indexed access to all resource source entities by type.

**Singleton Component**:
```csharp
public struct ResourceRegistry : IComponentData
{
    public int TotalResources;          // Total count of all resource sources
    public int TotalActiveResources;   // Resources with UnitsRemaining > 0
    public uint LastUpdateTick;        // Frame synchronization
    public uint LastSpatialVersion;    // Spatial grid version used for cached cell ids
}
```

**Buffer Element**:
```csharp
public struct ResourceRegistryEntry : IBufferElementData
{
    public ushort ResourceTypeIndex;   // Index into ResourceTypeIndex
    public Entity SourceEntity;         // The resource source entity
    public float3 Position;             // Cached position for spatial queries
    public float UnitsRemaining;        // Cached state for quick filtering
    public byte ActiveTickets;          // Reservation tracking
    public byte ClaimFlags;             // Custom flag mask (player claim, villager claim, etc.)
    public uint LastMutationTick;       // Deterministic ordering/versioning hook
    public int CellId;                  // Cached spatial cell id (-1 when grid unavailable)
    public uint SpatialVersion;         // Spatial grid version used to compute CellId
}
```

**Query Patterns**:
- All resources: Query buffer elements
- By type: Filter by `ResourceTypeIndex`
- Active only: Filter by `UnitsRemaining > 0`
- Spatial: Use `CellId`/`SpatialVersion` when available, otherwise fall back to `Position` for distance calculations

**Registry Metadata & Handles**  
- Each registry singleton carries a `RegistryMetadata` component describing its semantic kind, archetype id, and latest version.  
- `CoreSingletonBootstrapSystem` seeds this metadata and the runtime refreshes it alongside buffer rebuilds.  
- Systems can request typed discovery via `SpatialRegistryMetadata` handles, allowing shared AI modules and pathfinding to resolve villager/resource/storehouse registries without hard-coded entity references.

### 3. StorehouseRegistry (Singleton Component + Buffer)

Provides indexed access to all storehouse entities with capacity information.

**Singleton Component**:
```csharp
public struct StorehouseRegistry : IComponentData
{
    public int TotalStorehouses;       // Total count
    public float TotalCapacity;        // Sum of all MaxCapacity values
    public float TotalStored;           // Sum of all TotalStored values
    public uint LastUpdateTick;
    public uint LastSpatialVersion;     // Spatial grid version used for cached cell ids
}
```

**Buffer Element**:
```csharp
public struct StorehouseRegistryEntry : IBufferElementData
{
    public Entity StorehouseEntity;     // The storehouse entity
    public float3 Position;             // Cached position
    public float TotalCapacity;         // Sum of all capacity elements
    public float TotalStored;           // Current inventory total
    public FixedList32Bytes<StorehouseRegistryCapacitySummary> TypeSummaries; // Inlined per-type capacity/usage
    public uint LastMutationTick;       // Deterministic ordering/versioning hook
    public int CellId;                  // Cached spatial cell id (-1 when grid unavailable)
    public uint SpatialVersion;         // Spatial grid version used to compute CellId
}
```

**Query Patterns**:
- Find storehouse by type: Iterate the `TypeSummaries` fixed list for a matching `ResourceTypeIndex`
- Find available capacity: Filter by `TotalStored < TotalCapacity` or compare per-type `Reserved` values inside `TypeSummaries`
- Nearest storehouse: Prefer `CellId`/`SpatialVersion` when spatial alignment is valid, fall back to `Position` otherwise

### 4. Logistics Registries (Transport Domain)

Provides shared state for miner vessels, haulers, freighters, and wagons so logistics systems can query availability without EntityManager scans.

**Common Structure**:
```csharp
public struct MinerVesselRegistry : IComponentData
{
    public int TotalVessels;
    public int AvailableVessels;
    public float TotalCapacity;
    public uint LastUpdateTick;
}

public struct MinerVesselRegistryEntry : IBufferElementData
{
    public Entity VesselEntity;
    public float3 Position;
    public ushort ResourceTypeIndex;
    public float Capacity;
    public float Load;
    public TransportUnitFlags Flags;
    public uint LastCommandTick;
}
```

Hauler, freighter, and wagon registries follow the same pattern with domain-specific payload (route ids, manifests, assigned villagers). All implement `IRegistryEntry`/`IRegistryFlaggedEntry` so shared helpers can reason about availability from bitmasks without bespoke code.

**Spatial Integration**: Once the spatial service publishes registry handles via `SpatialRegistryMetadata`, transport registries should store cached cell indices or last-known positions so queries can be resolved without re-reading `LocalTransform`. Plan to extend registry entries with optional spatial metadata (e.g., `int CellId`, `uint LastSpatialVersion`) after the new spatial provider lands (see `Docs/DesignNotes/SpatialPartitioning.md`).

**Status**: Components and bootstrap seeding exist today; future logistics systems will mirror authoritative transport components into these buffers. The registry directory already advertises the handles so spatial queries and AI subsystems can opt-in as soon as entries are populated.

### 5. Resource Type Mapping

**Authoring**: ResourceTypeCatalog ScriptableObject → BlobAssetReference via baker
**Runtime**: Lookup string→index via blob asset traversal
**Stored Indices**: `ushort` for compact component data

```csharp
// Helper for converting string to index
public static ushort LookupResourceTypeIndex(
    BlobAssetReference<ResourceTypeIndex> catalog,
    FixedString64Bytes resourceId)
```

## Systems Architecture

### ResourceTypeIndexBaker

**Location**: `PureDOTS.Authoring`
**Input**: `ResourceTypeCatalog` ScriptableObject (from `PureDotsConfigAuthoring`)
**Output**: Creates singleton entity with `ResourceTypeIndex` blob reference
**Update**: Runs during SubScene baking, creates deterministic blob asset

**Implementation Notes**:
- Build blob from `ResourceTypeCatalog.entries`
- Store mappings as `NativeHashMap<FixedString64Bytes, ushort>`
- Create `BlobAssetReference<ResourceTypeIndex>` singleton

### ResourceRegistrySystem

**Update Group**: `ResourceSystemGroup` (before other resource systems)
**Schedule**: Early in group, updates registry before consumers query it
**Responsibilities**:
1. Query all entities with `ResourceSourceConfig` + `ResourceTypeId`
2. Resolve `ResourceTypeId.Value` → `ushort` index via blob lookup
3. Update `ResourceRegistry` buffer with current state
4. Cache `UnitsRemaining` and `Position` for filtering

**System Ordering**:
```
[FixedStepSimulation] ResourceSystemGroup
  ├─ ResourceRegistrySystem (updates catalog)
  ├─ ResourceGatheringSystem (queries catalog)
  ├─ ResourceDepositSystem
  └─ StorehouseInventorySystem
```

### StorehouseRegistrySystem

**Update Group**: `ResourceSystemGroup` (before StorehouseInventorySystem)
**Responsibilities**:
1. Query all entities with `StorehouseConfig` + `StorehouseInventory`
2. Copy state into `StorehouseRegistry` buffer
3. Cache `Position`, `TotalCapacity`, `TotalStored`

**System Ordering**:
```
ResourceSystemGroup
  ├─ StorehouseRegistrySystem (updates catalog)
  ├─ ResourceDepositSystem (queries catalog)
  └─ StorehouseInventorySystem (updates storehouse state)
```

### RegistryConsoleInstrumentationSystem *(new)*

- **Update Group**: `LateSimulationSystemGroup` (after `RegistryDirectorySystem`).
- **Purpose**: Optional console logging triggered by attaching `RegistryConsoleInstrumentation` to a singleton. Produces compact `[Registry] Tick ...` summaries for automated tests and headless runs.
- **Behaviour**: Respects minimum tick deltas and a "log only on directory version change" flag to avoid noisy output. Consumes existing registry metadata so no additional bookkeeping is required.
- **Spatial alignment**: Future spatial jobs should emit similar instrumentation (cell counts, rebuild timings) so registry metrics and spatial metrics can be correlated during headless runs.

## Migration Strategy

### Phase 1: Add New Components (Non-Breaking)
- Create `ResourceTypeIndex` blob asset structures
- Create `ResourceRegistry` and `StorehouseRegistry` components
- Create baker for `ResourceTypeCatalog`
- Add registry systems (update buffers, don't break existing code)

### Phase 2: Update StorehouseAPI *(COMPLETED)*
- Static helpers removed. Consumers should query `StorehouseRegistryEntry.TypeSummaries` and mutate `StorehouseJobReservation` / `StorehouseReservationItem` for capacity claims.
- History: initial plan targeted method replacement; Beta finalized the migration by deleting the helper and documenting buffer-based usage in `VillagerJobs_DOTS.md`.

### Phase 3: Migrate Consumer Systems
- Update `ResourceGatheringSystem` to use `ResourceRegistry` buffer
- Update `ResourceDepositSystem` to use `StorehouseRegistry` buffer
- Update `VillagerJobAssignmentSystem` to query registries instead of entity queries

### Phase 4: Deprecate Legacy APIs
- Mark `StorehouseAPI` as `[Obsolete]`
- Update tests to use new registry patterns
- Remove legacy code after migration window

## API Contracts for Consumers

### Villager Systems

**Requirement**: Find nearest resource source by type
```csharp
var registry = SystemAPI.GetSingleton<ResourceRegistry>();
var entries = SystemAPI.GetBuffer<ResourceRegistryEntry>(registryEntity);

foreach (var entry in entries)
{
    if (entry.ResourceTypeIndex == targetType && entry.UnitsRemaining > 0)
    {
        var distSq = math.distancesq(entry.Position, villagerPos);
        // Track nearest
    }
}
```

### UI Systems

**Requirement**: Display total resources/storage
```csharp
var storehouseReg = SystemAPI.GetSingleton<StorehouseRegistry>();
float totalWood = 0f;
foreach (var entry in storehouseEntries)
{
    // Query entry's buffer for "Wood" type
    // Sum amounts
}
```

### Job Board Systems

**Requirement**: Validate storehouse capacity before assignment
```csharp
var storehouseReg = SystemAPI.GetSingleton<StorehouseRegistry>();
foreach (var entry in storehouseEntries)
{
    if (entry.TotalStored < entry.TotalCapacity)
    {
        // Available capacity found
    }
}
```

## Indexes & Performance

### Expected Indexes

1. **ResourceRegistry**:
   - ResourceTypeIndex → List of entities (via buffer filtering)
   - Spatial: Position → Distance queries (Linear scan acceptable for small sets)

2. **StorehouseRegistry**:
   - TotalCapacity → Sort descending for largest-first queries
   - Availability: TotalStored < TotalCapacity → Filter for capacity

3. **ResourceTypeIndex**:
   - String → ushort index: HashMap lookup O(1) authoring time, blob traversal O(n) runtime
   - ushort → String: Blob array access O(1)

### Performance Assumptions

- **Resource count**: < 100 per type (linear scans acceptable)
- **Storehouse count**: < 50 total (no spatial partitioning needed)
- **Resource types**: < 100 total (blob traversal acceptable)

If scales exceed assumptions, consider:
- NativeMultiHashMap for type→entities
- Spatial hashing for resource/storehouse positioning
- Cached binary search for sorted registries

## Invariants

1. **ResourceRegistry**:
   - Buffer entries match entities with `ResourceSourceConfig`
   - All entries have valid `ResourceTypeIndex` (exist in catalog)
   - `LastUpdateTick` updated every frame
   - `LastSpatialVersion` mirrors the spatial grid version when available

2. **StorehouseRegistry**:
   - Buffer entries match entities with `StorehouseConfig`
   - Cached `TotalStored` and `TotalCapacity` match sum of buffer elements
   - `LastUpdateTick` updated every frame
   - `LastSpatialVersion` mirrors the spatial grid version when available

3. **ResourceTypeIndex**:
   - Singleton exists throughout simulation
   - All `ushort` indices valid and unique
   - Strings null-terminated, max 64 bytes

## Beta Feedback – Villager Job Loop Integration

- **ResourceRegistryEntry**
  - Add `byte ActiveTickets` to track villager job reservations per source (clamped to `MaxSimultaneousWorkers`).
  - Add `byte ClaimFlags` (bit 0 = PlayerClaim, bit 1 = VillagerReserved) so villagers can yield to player interactions without additional queries.
  - Add `uint LastMutationTick` to capture deterministic ordering when multiple systems contest the same source.

```csharp
public struct ResourceRegistryEntry : IBufferElementData
{
    public ushort ResourceTypeIndex;
    public Entity SourceEntity;
    public float3 Position;
    public float UnitsRemaining;
    public byte ActiveTickets;
    public byte ClaimFlags;
    public uint LastMutationTick;
    public int CellId;
    public uint SpatialVersion;
}
```

- **StorehouseRegistryEntry**
  - Replace the (invalid) `DynamicBuffer<StorehouseCapacityElement>` field with a fixed list summary so consumers can reason about per-type capacity without extra entity lookups.
  - Track `Reserved` amounts to prevent double-counting between villager tickets and player deposits.
  - Cache spatial metadata (CellId + SpatialVersion) to keep lookups aligned with the active grid.

```csharp
public struct StorehouseRegistryCapacitySummary
{
    public ushort ResourceTypeIndex;
    public float Capacity;
    public float Stored;
    public float Reserved;
}

public struct StorehouseRegistryEntry : IBufferElementData
{
    public Entity StorehouseEntity;
    public float3 Position;
    public float TotalCapacity;
    public float TotalStored;
    public FixedList32Bytes<StorehouseRegistryCapacitySummary> TypeSummaries;
    public uint LastMutationTick;
    public int CellId;
    public uint SpatialVersion;
}
```

These additions let the villager job loop create deterministic job tickets, honour player priority, and choose viable drop-off targets using only the registry buffers. Villager job assignment/delivery systems now rely on the cached `CellId`/`SpatialVersion` to shortlist nearby entries before falling back to broader scans when spatial data is unavailable. The neighbour heuristic is intentionally simple: expand a Manhattan-radius search (1 → 2 → 4 cells) until candidates appear, only falling back to distance checks when spatial metadata is stale.

### Consumer Integration Guide

When integrating new systems with the registries:

1. **Pull the buffer through the directory** using `RegistryDirectoryLookup.TryGetRegistryBuffer<TEntry>`; this keeps consumers decoupled from concrete singleton entities.
2. **Shortlist entries using the cached `CellId`** – compute the agent cell via `SpatialHash.Quantize`, then iterate the registry entries looking for matching/nearby cells (expanding the search radius as needed). The villager job systems contain a reference implementation (`VillagerJobAssignmentSystem`, `VillagerJobDeliverySystem`).
3. **Evaluate candidates deterministically** using existing scoring helpers (`TryScoreResourceCandidate`, `TryScoreStorehouseCandidate`) or custom logic. All helpers should treat `CellId = -1` or mismatched `RegistrySpatialVersion` as “stale” and defer to broader searches.
4. Optionally **fall back to linear scans** when the grid is unavailable (rewind, bootstrap) to guarantee work still proceeds.

New consumers should follow the same pattern: shortlist with the cached metadata first, fall back gracefully if spatial data is invalid, and update documentation/tests to cover their integration path.

#### Upcoming Consumers
- **Logistics/Transport modules** – when miner vessels, haulers, or wagons arrive, mirror the villager flow: cache `CellId`/`SpatialVersion`, shortlist nearby loads/drops, and record pending reservations via registry flags.
- **AI sensor suites** – proximity scans for miracles or environmental hazards should call the shared shortlist helper before sampling positions directly, protecting against missing transforms during streaming.
- **Tooling/UI inspectors** – registry overlays can read `RegistrySpatialVersion` to warn when cached data lags behind the grid (e.g., after partial rebuilds).

## Update Order

```
[FixedStepSimulation] ResourceSystemGroup
  1. ResourceTypeIndexSystem (ensures singleton exists)
  2. ResourceRegistrySystem (scan resources, update buffer)
  3. StorehouseRegistrySystem (scan storehouses, update buffer)
  4. ResourceGatheringSystem (reads registries)
  5. ResourceDepositSystem (reads registries)
  6. StorehouseInventorySystem (updates storehouse state)
```

## Testing Strategy

### Unit Tests (EditMode)
- ResourceTypeIndexBaker converts catalog to blob correctly
- Index lookups resolve strings ↔ ushort correctly
- Registry systems create singleton + buffer on bootstrap

### Playmode Tests
- **Resource Loop**: Gather → Deposit → Verify state in registries
- **Capacity Clamping**: Deposit beyond capacity → Verify rejection
- **Pile Merge**: Multiple deposits → Verify aggregation
- **Storehouse Events**: Deposit/withdraw → Verify LastUpdateTick increments
- **Registry Sync**: Entity spawns/despawns → Registry updates
- **Registry Instrumentation**: Enable `RegistryConsoleInstrumentation` and assert console snapshot format (headless validation)

### Test Coverage
```csharp
[Test] ResourceRegistry_UpdatesOnEntitySpawn()
[Test] ResourceRegistry_FiltersByType()
[Test] StorehouseRegistry_ClampsCapacity()
[Test] StorehouseRegistry_EmitsEvents()
[Test] ResourceTypeIndex_LookupRoundTrip()
```

## Open Questions for Beta

1. **Resource Type Migration**: Should existing `FixedString64Bytes` components be migrated to `ushort` indices at authoring time, or keep both?
   - **Decision**: Keep both initially. String-based authoring, index-based runtime lookups.

2. **Storehouse Buffer Synchronization**: Should `StorehouseRegistryEntry` contain a reference to the actual buffer or copy data?
   - **Decision**: Copy data for safety (Burst-compatible). Entity reference for accessing buffer if needed.

3. **Dynamic Type Registration**: Can resource types be added at runtime?
   - **Decision**: No. Types fixed at SubScene bake time for determinism.

4. **Registry Cleanup**: Should destroyed entities be removed from registry buffers immediately or lazily?
   - **Decision**: Immediately in same frame. Registry systems query entities directly.

5. **Index Limits**: Is 65,535 resource types sufficient?
   - **Decision**: Yes. Current catalog has < 10 types.

## TruthSource Contract Mapping

This implementation replaces legacy registries per PureDOTS_TODO.md:40-43:

- **WorldServices/RegistrySystems**: Replaced with singleton + buffer patterns
- **Domain-specific registries**: Resources and storehouses now use DynamicBuffer
- **Bridge shims**: Eliminated by direct DOTS-native access

The registries provide deterministic, queryable data for villager AI, UI systems, and job assignment logic.
