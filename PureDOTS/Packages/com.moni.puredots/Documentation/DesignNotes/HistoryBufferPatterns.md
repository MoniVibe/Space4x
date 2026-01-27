# History Buffer Patterns

## Overview

PureDOTS systems use double-buffering, ring buffers, and structured history capture to enable deterministic rewind while keeping hot paths cache-friendly. This document defines patterns for history storage, snapshot cadence, and memory-efficient history management.

## Core Patterns

### 1. Double-Buffering

**Purpose**: Separate read and write buffers to avoid stalls during history capture.

**Pattern**:
- **Write Buffer**: Current frame writes to buffer A
- **Read Buffer**: Previous frame's buffer B is available for reading
- **Swap**: After capture, swap buffers (A ↔ B)

**Use Cases**:
- Spatial grid double-buffering (`SpatialGridStagingEntry` + `SpatialGridEntry`)
- Environment grid updates (write to staging, swap after validation)
- Large buffer updates where readers need stable data

**Example**:
```csharp
// Spatial grid uses double-buffering
public struct SpatialGridState : IComponentData
{
    public byte ActiveBufferIndex;  // 0 or 1
    // ...
}

// Write to staging buffer
var stagingBuffer = entityManager.GetBuffer<SpatialGridStagingEntry>(gridEntity);
stagingBuffer.Add(new SpatialGridStagingEntry { /* ... */ });

// After rebuild, swap buffers
var activeBuffer = entityManager.GetBuffer<SpatialGridEntry>(gridEntity);
SwapBuffers(stagingBuffer, activeBuffer);
```

**Benefits**:
- Readers see stable data (no mid-frame changes)
- Writers don't block readers
- Cache-friendly (sequential writes, sequential reads)

### 2. Ring Buffers

**Purpose**: Fixed-size circular buffers for recent history with automatic overflow handling.

**Pattern**:
- Fixed capacity (e.g., 60 samples for 60 seconds at 1 sample/second)
- Circular index wraps around
- Oldest entries automatically overwritten

**Use Cases**:
- Per-entity history samples (`VillagerHistorySample`, `ResourceHistorySample`)
- Event queues with finite retention
- Recent state snapshots for replay

**Example**:
```csharp
public struct VillagerHistorySample : IBufferElementData
{
    public uint Tick;
    public float3 Position;
    public float Health;
    // ...
}

// Ring buffer implementation (capacity = 60 samples)
var buffer = entityManager.GetBuffer<VillagerHistorySample>(entity);
if (buffer.Length >= 60)
{
    // Find oldest sample (by tick)
    int oldestIndex = 0;
    for (int i = 1; i < buffer.Length; i++)
    {
        if (buffer[i].Tick < buffer[oldestIndex].Tick)
            oldestIndex = i;
    }
    buffer[oldestIndex] = newSample;  // Overwrite oldest
}
else
{
    buffer.Add(newSample);  // Append until capacity
}
```

**Benefits**:
- Fixed memory footprint (no unbounded growth)
- Fast access (O(1) append, O(n) search)
- Automatic pruning (oldest entries discarded)

### 3. Structured History Capture

**Purpose**: Capture minimal, structured data at fixed cadence for deterministic replay.

**Pattern**:
- **Snapshot Cadence**: Fixed interval (e.g., every 1-5 seconds)
- **Compact Samples**: Store only essential state (position, health, flags)
- **Tick-Based Indexing**: Samples indexed by tick for O(log n) lookup

**Use Cases**:
- Villager state snapshots (`VillagerHistorySample`)
- Resource state (`ResourceHistorySample`)
- Storehouse inventory (`StorehouseHistorySample`)
- Job progress (`VillagerJobHistorySample`)

**Example**:
```csharp
// Capture sample at fixed cadence
if (timeState.Tick % snapshotCadence == 0)
{
    var sample = new VillagerHistorySample
    {
        Tick = timeState.Tick,
        Position = transform.Position,
        Health = needs.Health,
        Hunger = needs.Hunger,
        Energy = needs.Energy,
        // ... minimal essential state
    };
    historyBuffer.Add(sample);
    
    // Prune old samples (beyond horizon)
    PruneHistory(ref historyBuffer, timeState.Tick, horizonSeconds, timeState.FixedDeltaTime);
}
```

**Benefits**:
- Deterministic replay (same cadence, same data)
- Memory efficient (only essential fields)
- Fast lookup (binary search by tick)

## History Capture Strategies

### Strategy 1: Snapshot Cadence (Recommended)

**When to Use**: Systems with continuous state that can be sampled at intervals.

**Implementation**:
1. Capture samples at fixed tick intervals (e.g., every 60 ticks = 1 second)
2. Store compact samples with tick + essential state
3. Prune samples beyond horizon (e.g., keep last 60 seconds)

**Examples**:
- `VillagerHistorySample` - Position, needs, job state
- `ResourceHistorySample` - Units remaining, flags
- `StorehouseHistorySample` - Inventory totals, capacity

**Memory Budget**: ~16-32 bytes per sample × entities × samples per entity

### Strategy 2: Command Replay

**When to Use**: Systems driven by explicit commands/events.

**Implementation**:
1. Record commands with tick + command data
2. Replay commands in order during playback
3. Commands should be deterministic (include seed if random)

**Examples**:
- `HandInteractionHistorySample` - Hand interactions, miracles
- `VegetationHarvestCommand` - Harvest events
- `ResourceSiphonCommand` - Resource gathering events

**Memory Budget**: ~8-16 bytes per command × command frequency

### Strategy 3: Deterministic Rebuild

**When to Use**: Systems where state can be rebuilt from deterministic transforms.

**Implementation**:
1. Record seed + version data only
2. Rebuild state deterministically during playback
3. Sort entities by `Entity.Index` for deterministic order

**Examples**:
- Registry rebuilds (sort by Entity.Index, rebuild from components)
- Spatial grid rebuilds (version + seed, rebuild from positions)
- Flow field rebuilds (goal positions + seed, rebuild Dijkstra)

**Memory Budget**: Minimal (seed + version per rebuild)

## Memory Management

### Pruning Strategy

**Horizon-Based Pruning**:
- Keep samples within time horizon (e.g., 60 seconds)
- Remove samples older than `currentTick - horizonTicks`
- Prune during capture (check before adding)

**Example**:
```csharp
private static void PruneHistory<T>(
    ref DynamicBuffer<T> buffer,
    uint currentTick,
    float horizonSeconds,
    float fixedDeltaTime)
    where T : unmanaged, IBufferElementData, IHistorySample
{
    uint horizonTicks = (uint)(horizonSeconds / fixedDeltaTime);
    uint minTick = currentTick > horizonTicks ? currentTick - horizonTicks : 0;
    
    // Remove samples older than minTick
    for (int i = buffer.Length - 1; i >= 0; i--)
    {
        if (buffer[i].Tick < minTick)
        {
            buffer.RemoveAt(i);
        }
    }
}
```

### Capacity Limits

**Per-Entity Buffers**:
- **Villagers**: Max 60 samples (~1.9 KB per villager at 32 bytes/sample)
- **Resources**: Max 30 samples (~960 bytes per resource at 32 bytes/sample)
- **Storehouses**: Max 30 samples (~480 bytes per storehouse at 16 bytes/sample)

**Global Buffers**:
- **Spatial Grid**: Double-buffered, capacity = max entities
- **Environment Grids**: Snapshot cadence (every 5 seconds), keep last 20 snapshots

**Total Budget** (100k entities):
- Villager history: 40k × 1.9 KB = ~76 MB
- Resource history: 10k × 960 B = ~9.6 MB
- Storehouse history: 1k × 480 B = ~480 KB
- **Total**: ~86 MB (within 256 MB rewind budget)

## Hot Path Considerations

### Cache-Friendly Access

**Sequential Writes**:
- Append samples sequentially (better cache performance)
- Avoid random inserts (sort during playback if needed)

**Read Patterns**:
- Binary search by tick (O(log n))
- Sequential scan for recent samples (O(n) but cache-friendly)

**Example**:
```csharp
// Hot path: append sample (sequential write)
historyBuffer.Add(newSample);

// Cold path: binary search for playback (sorted by tick)
int index = BinarySearchByTick(historyBuffer, targetTick);
var sample = historyBuffer[index];
```

### Avoid Allocations

**Pre-Allocate Buffers**:
- Set capacity upfront (avoid reallocations)
- Use `ResizeUninitialized()` if capacity changes

**Pool History Samples**:
- Reuse sample structs (avoid GC allocations)
- Use `NativeArray` pools for temporary lookups

## Rewind Integration

### Playback Flow

1. **Determine Target Tick**: From `RewindState.TargetTick`
2. **Find Sample**: Binary search history buffer for sample at or before target tick
3. **Restore State**: Apply sample data to components
4. **Rebuild Derived State**: Rebuild registries, spatial grid if needed

**Example**:
```csharp
public void Playback(ref SystemState state, uint targetTick)
{
    foreach (var (transform, needs, historyBuffer) in
             SystemAPI.Query<RefRW<LocalTransform>, RefRW<VillagerNeeds>, DynamicBuffer<VillagerHistorySample>>())
    {
        if (historyBuffer.Length == 0)
            continue;
            
        int sampleIndex = FindSampleIndex(historyBuffer, targetTick);
        var sample = historyBuffer[sampleIndex];
        
        // Restore state
        transform.ValueRW.Position = sample.Position;
        needs.ValueRW.Health = sample.Health;
        needs.ValueRW.Hunger = sample.Hunger;
        // ...
    }
}
```

### Catch-Up Strategy

**Incremental Replay**:
- Replay samples from current tick to target tick
- Apply each sample in order
- Rebuild derived state after catch-up completes

**Example**:
```csharp
public void CatchUp(ref SystemState state, uint currentTick, uint targetTick)
{
    // Replay samples between currentTick and targetTick
    for (uint tick = currentTick + 1; tick <= targetTick; tick++)
    {
        ApplySampleAtTick(tick);
    }
    
    // Rebuild derived state (registries, spatial grid)
    RebuildDerivedState();
}
```

## Implementation Checklist

When adding history capture to a system:

- [ ] Choose appropriate strategy (snapshot cadence, command replay, or deterministic rebuild)
- [ ] Define compact sample struct (essential fields only, ≤ 64 bytes)
- [ ] Implement capture at fixed cadence (or on events for command replay)
- [ ] Add pruning logic (horizon-based, capacity limits)
- [ ] Implement playback logic (binary search, state restoration)
- [ ] Add catch-up logic (incremental replay if needed)
- [ ] Verify memory budget (per-entity and total)
- [ ] Test rewind determinism (same tick → same state)

## Cross-References

- `Docs/DesignNotes/RewindPatterns.md` - High-level rewind strategies
- `Docs/DesignNotes/SoA_Expectations.md` - Memory layout guidelines
- `Docs/TruthSources/RuntimeLifecycle_TruthSource.md` - Rewind lifecycle
- `Runtime/Runtime/HistoryComponents.cs` - History sample structs
- `Runtime/Systems/VillagerJobSystems.cs` - Example history capture implementation


