# Registry Lifecycle & Deterministic Rebuild

## Overview

PureDOTS registries use a **deterministic rebuild** strategy: they rebuild every frame from authoritative component queries rather than tracking individual spawn/despawn events. This ensures deterministic behavior and rewind safety.

## Current Implementation

### Rebuild Pattern

All registry systems follow this pattern:

1. **Query authoritative entities** each frame (e.g., all entities with `ResourceSourceConfig`)
2. **Build registry entries** using `DeterministicRegistryBuilder<TEntry>`
3. **Apply to buffer** deterministically (sorted by `Entity.Index` for stable ordering)
4. **Skip during playback** (registries are rebuilt from queries, so playback state is deterministic)

Example from `ResourceRegistrySystem`:

```csharp
// Query all resource sources
foreach (var (sourceState, resourceTypeId, transform, entity) in 
    SystemAPI.Query<RefRO<ResourceSourceState>, RefRO<ResourceTypeId>, RefRO<LocalTransform>>()
    .WithAll<ResourceSourceConfig>()
    .WithEntityAccess())
{
    builder.Add(new ResourceRegistryEntry { /* ... */ });
}

// Apply deterministically
builder.ApplyTo(ref entries, ref metadata, timeState.Tick, continuity);
```

### Deterministic Ordering

`DeterministicRegistryBuilder` ensures entries are sorted by `Entity.Index` before applying to the buffer. This guarantees:
- Identical results across runs with the same entity set
- Stable iteration order for consumers
- Rewind safety (rebuilding from queries produces identical state)

### Rewind Integration

Registries currently skip updates during playback:

```csharp
if (rewindState.Mode != RewindMode.Record)
{
    return; // Skip rebuild during playback
}
```

**Rationale**: Since registries rebuild from queries, the playback state is already deterministic. Rebuilding during playback would produce identical results but adds unnecessary computation.

**Future consideration**: If playback performance becomes an issue, registries could rebuild during playback with minimal overhead since they're query-based.

## Spawn/Despawn Handling

Entities spawn/despawn automatically:
- **Spawn**: New entities matching the query are included in the next rebuild
- **Despawn**: Entities no longer matching the query are excluded from the next rebuild

No ECB-based tracking needed - the query naturally handles lifecycle changes.

## Performance Characteristics

- **Cost**: O(n) per frame where n = number of entities matching the query
- **Allocation**: `DeterministicRegistryBuilder` uses `Allocator.Temp` (disposed after apply)
- **Scalability**: Linear with entity count; acceptable for typical use cases (<100k entities per registry)

## Optimization Opportunities

If rebuild cost becomes prohibitive:

1. **Dirty tracking**: Only rebuild when entities matching the query change
2. **Incremental updates**: Track spawn/despawn events via ECB and apply incrementally
3. **Caching**: Cache registry state and invalidate on entity changes

**Current recommendation**: Rebuild-every-frame is simple, deterministic, and performant enough for current scale. Optimize only if profiling shows registry rebuilds are a bottleneck.

## Comparison with Event-Based Approach

| Approach | Pros | Cons |
|----------|------|------|
| **Rebuild Every Frame** (Current) | Simple, deterministic, no ECB tracking | O(n) cost per frame |
| **ECB Tracking** | Incremental updates, lower cost | Complex spawn/despawn tracking, requires deterministic sorting |

**Decision**: Rebuild-every-frame chosen for simplicity and determinism guarantees.

## See Also

- `Docs/DesignNotes/RewindPatterns.md` - Rewind strategy documentation
- `Runtime/Runtime/Registry/RegistryUtilities.cs` - `DeterministicRegistryBuilder` implementation
- `Docs/TODO/RegistryRewrite_TODO.md` - Registry rewrite task tracking


