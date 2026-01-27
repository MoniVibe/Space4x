# Presentation Bridge Contracts

## Overview

PureDOTS uses a presentation bridge pattern to separate simulation logic (hot archetypes) from visual representation (cold archetypes or companion entities). This ensures determinism, scalability, and rewind safety.

## Core Contracts

### 1. Presentation Registry

**Purpose**: Maps simulation entity types to visual prefabs/assets.

**Components**:
- `PresentationRegistryReference` - Blob asset containing descriptor mappings
- `PresentationRegistryBlob` - Blob array of `PresentationDescriptor` entries
- `PresentationDescriptor` - Maps key hash to prefab, default offset/scale/tint

**Authoring**: `PresentationRegistryAsset` ScriptableObject bakes into blob asset

**Usage**: Systems request visuals via `PresentationSpawnRequest` using descriptor hash keys

### 2. Presentation Commands

**Request Buffers**:
- `PresentationSpawnRequest` - Requests visual instantiation for a simulation entity
- `PresentationRecycleRequest` - Requests visual cleanup when entity despawns

**Flags**:
- `PresentationSpawnFlags` - Controls pooling, animation, overrides (tint, scale, transform)

**Systems**:
- `PresentationSpawnSystem` - Processes spawn requests, instantiates visuals
- `PresentationRecycleSystem` - Processes recycle requests, returns to pool or destroys

### 3. Presentation Handles

**Component**: `PresentationHandle`
- Links simulation entity to visual entity
- Stores descriptor hash and variant seed for deterministic recreation
- Visual entity reference for updates/cleanup

**Pattern**: One simulation entity → One presentation handle → One visual entity

## Companion Archetypes

### Hot Archetypes (Simulation)

**Purpose**: Store simulation-critical data updated every frame.

**Components**:
- `LocalTransform` - Position, rotation, scale
- `VillagerState`, `ResourceSourceState`, etc. - Gameplay state
- `SpatialIndexedTag` - Spatial indexing
- `RewindableTag` - Rewind participation

**Characteristics**:
- Slim (< 64 bytes per entity)
- Burst-compatible
- Deterministic
- Updated in fixed-step groups

### Cold Archetypes (Presentation)

**Purpose**: Store visual representation data updated less frequently.

**Components**:
- `PresentationHandle` - Link to visual entity
- `PresentationVisualState` (optional) - Animation state, particle effects
- `PresentationMeshInstance` (optional) - Entities Graphics mesh reference

**Characteristics**:
- Can be larger (presentation-specific data)
- May require main-thread access (Unity rendering APIs)
- Tolerates non-deterministic updates (visual-only)
- Updated in presentation groups

### Companion Entity Pattern

**Pattern**: Simulation entity spawns companion visual entity

```
Simulation Entity (hot)
├─ LocalTransform (simulation position)
├─ VillagerState (gameplay data)
└─ PresentationHandle → Visual Entity (cold)
    ├─ LocalTransform (visual position, may lag)
    ├─ PresentationMeshInstance
    └─ PresentationVisualState
```

**Benefits**:
- Separation of concerns
- Independent chunking (hot/cold don't mix)
- Visual updates don't affect simulation determinism
- Easy to disable visuals without touching simulation

## Rewind Safety

### Playback Guards

**Component**: `PlaybackGuardTag`
- Added to simulation entities during rewind playback
- Presentation systems check for guard before updating visuals
- Visuals can either:
  1. Regenerate every frame from simulation state (safe but expensive)
  2. Honor playback guard and skip updates (efficient but requires state restoration)

### Presentation Update Strategy

**Option 1: Full Regeneration** (Simple, safe)
- Presentation systems read simulation state every frame
- Visuals regenerate from authoritative data
- No playback guard needed (always correct)
- Higher cost during playback

**Option 2: Event-Driven** (Efficient, requires care)
- Presentation systems subscribe to events (`DivineHandEvent`, `VillagerEvent`, etc.)
- Only update visuals when events fire
- During playback, replay events in order
- Requires event history for rewind

**Option 3: Hybrid** (Balanced)
- Critical visuals (hand cursor, UI) regenerate every frame
- Stable visuals (buildings, terrain) update only on events
- Use playback guard to skip non-critical updates during playback

## System Execution Order

```
SimulationSystemGroup (hot)
├─ VillagerSystemGroup
│   └─ Updates VillagerState, LocalTransform
├─ ResourceSystemGroup
│   └─ Updates ResourceSourceState
└─ HandSystemGroup
    └─ Updates DivineHandState

PresentationSystemGroup (cold)
├─ PresentationSpawnSystem
│   └─ Processes PresentationSpawnRequest → Creates visual entities
├─ PresentationUpdateSystem (game-specific)
│   └─ Syncs visual LocalTransform from simulation LocalTransform
└─ PresentationRecycleSystem
    └─ Processes PresentationRecycleRequest → Cleans up visuals
```

## Authoring Patterns

### Prefab Setup

1. **Simulation Prefab** (SubScene entity):
   - Components: `VillagerState`, `LocalTransform`, `SpatialIndexedTag`
   - No visual components (presentation bridge handles visuals)

2. **Visual Prefab** (referenced by PresentationRegistry):
   - Components: `LocalTransform`, `RenderMesh`, `MaterialMeshInfo`
   - Used by `PresentationSpawnSystem` to instantiate visuals

3. **Baker Integration**:
   - `VillagerAuthoring` bakes simulation components
   - `PresentationRegistryAsset` maps "Villager" key → visual prefab
   - Runtime: systems request visuals via descriptor hash

### Scene Setup

1. **Simulation SubScene**:
   - Contains simulation entities (villagers, resources, etc.)
   - No visual GameObjects

2. **Presentation Bootstrap**:
   - `PresentationBootstrapSystem` initializes registry
   - Systems request visuals as entities spawn

3. **Visual World** (optional):
   - Separate world for presentation-only systems
   - Synchronizes with main simulation world via events/buffers

## Best Practices

1. **Keep Hot Archetypes Lean**: Minimize component count and size
2. **Use Companion Entities**: Don't mix simulation and presentation data
3. **Respect Rewind**: Check `RewindState.Mode` and `PlaybackGuardTag`
4. **Pool Visuals**: Use `PresentationSpawnFlags.AllowPooling` for frequently spawned visuals
5. **Deterministic Keys**: Use stable descriptor keys (not random GUIDs)
6. **Event-Driven Updates**: Prefer events over per-frame polling for visuals

## Future Enhancements

- Entities Graphics integration (hybrid renderer)
- GPU-driven animation (compute shaders)
- Batch visual updates (jobs for transform sync)
- Visual LOD system (distance-based detail reduction)
- Presentation world separation (optional companion world)


