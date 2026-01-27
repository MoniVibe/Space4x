# Spatial Services Concepts

## 1. Spatial Query Broker
- **Behaviour**: Consumer systems request neighbourhood slices (radius, AABB, cone) through a common broker instead of scanning registries. The broker caches query descriptors per tick and multiplexes results to multiple subscribers (villager AI, miracles, logistics).
- **Implementation sketch**:
  ```csharp
  public struct SpatialQueryRequest : IBufferElementData
  {
      public SpatialQueryType Type; // Radius, AABB, Cone, kNN
      public float3 Origin;
      public float Radius;
      public float3 Extents;
      public Entity Requester;
      public uint ResultHandle; // points into pooled result buffers
  }
  ```
  - Add `SpatialQueryRequestBuffer` singleton populated by consumer systems.
  - New `SpatialQueryBrokerSystem` (update after `SpatialGridBuildSystem`) iterates requests, runs provider-specific queries (e.g., `UniformSpatialGridProvider.QueryRadius`) and stores results in pooled buffers.
  - Consumer jobs read back results via handle lookups, keeping deterministic ordering.

## 2. Region Tagging & Occupancy Layers
- **Behaviour**: Maintain lightweight region descriptors (biomes, influence zones, hazard levels) keyed by spatial cell so AI and miracles can react to area traits without searching authoring data.
- **Implementation sketch**:
  - Extend spatial state with `NativeArray<SpatialCellMetadata>` storing `BiomeId`, `ThreatLevel`, `Ownership`, `Population`.
  - Authoring: `SpatialRegionAuthoring` baker writes blob maps into `SpatialRegionConfig`.
  - Runtime system (`SpatialRegionBakeSystem`) projects authored polygons into cell metadata during scene load; updates happen via streamed events (e.g., capture points changing ownership).
  - Consumers (raid AI, miracle targeting) fetch metadata using cell lookup utilities added to `SpatialQueryHelper`.

## 3. Multi-Resolution Grids
- **Behaviour**: Support coarse + fine grids simultaneously so large scale features (climate zones, trade routes) query coarse cells while local AI uses fine cells.
- **Implementation sketch**:
  - Expand `SpatialGridConfig` to support `Levels` array (cell size per level).
  - `SpatialGridBuildSystem` updates per-level buffers (`SpatialGridEntryLevel`, `SpatialGridCellRangeLevel`).
  - Add `SpatialQueryLevel` parameter to broker requests.
  - Authoring profile (`SpatialPartitionProfile`) exposes level definitions; baking creates blob asset consumed by runtime config system.

## 4. Pathing & Flow Fields Integration
- **Behaviour**: Generate flow fields or cached path costs per region cell to accelerate villager routing and fleet navigation.
- **Implementation sketch**:
  - Introduce `SpatialFlowField` component holding `NativeArray<float>` costs per cell.
  - Path generation systems enqueue recompute requests when terrain/buildings change.
  - Flow field jobs reuse grid indices from `SpatialGridState` to avoid duplicate hashing.
  - Villager movement systems sample flow data before falling back to A* on demand.

## 5. Spatial Event Streams
- **Behaviour**: Publish events when density thresholds or state transitions occur (e.g., overcrowded house cell, predator enters village cell) so gameplay systems react without polling.
- **Implementation sketch**:
  - New buffer `SpatialEventStream` appended by `SpatialGridInstrumentationSystem` or dedicated detectors.
  - Each event includes `CellId`, `EventType`, `Entity`, `Delta`.
  - Consumers subscribe by event type; e.g., villager morale system listens for overcrowded cells, combat AI listens for threat spikes.
  - Ensure events carry `SpatialVersion` to maintain rewind compatibility.

## 6. Visualization & Tooling Hooks
- **Behaviour**: Provide shared debug overlays showing occupancy heatmaps, region tags, flow fields.
- **Implementation sketch**:
  - Extend `SpatialInstrumentationSystem` to emit `SpatialDebugSnapshot` blob each frame (or on demand).
  - Editor tooling reads snapshot via `WorldDebugOverlayWindow`, renders gizmos per cell.
  - Include toggleable layers (entities, regions, flow, events) to aid designers.

## Integration Checklist
- Ensure all new services update after `SpatialGridBuildSystem` and before registry consumers in the `SpatialSystemGroup`.
- Register data in `RegistrySpatialSyncState` so downstream registries detect spatial version changes.
- Add authoring docs under `Docs/Guides/Authoring` describing region and flow-field setup once implemented.
- Parallelisation: design query brokers and region updates as Burst-friendly jobs (e.g., `IJobChunk`, `IJobParallelFor`) to leverage Unity’s job scheduler and keep spatial services scalable.
