# Spatial Services Plan – Registry-Aligned Spatial Grid

> Active implementation tasks are tracked in `Docs/TODO/SpatialServices_TODO.md` and `Docs/TODO/RegistryRewrite_TODO.md`. Treat this note as context/background and update it when those TODOs land.

## Current State Snapshot

- **Components in place**: `SpatialGridConfig`, `SpatialGridState`, `SpatialGridCellRange`, `SpatialGridEntry`, staging buffers, and `SpatialIndexedTag` already exist (`Assets/Scripts/PureDOTS/Runtime/Spatial/SpatialComponents.cs`).
- **Systems**: `SpatialGridInitialBuildSystem` (Initialization) and `SpatialGridBuildSystem` (SpatialSystemGroup) rebuild the grid and populate `SpatialRegistryMetadata` handles from `RegistryDirectorySystem` each time the grid updates.
- **Utilities**: `SpatialUtilities.cs` / `SpatialQueryHelper` provide deterministic radius queries, k-NN, and AABB overlaps; `SpatialRegistryPerformanceTests` validates large rebuilds and k-NN behaviour.
- **Consumers**:
  - `VillagerJobSystems` use `SpatialQueryHelper.CollectEntitiesInRadius` to shortlist resource/storehouse candidates before ranking via registry data.
  - `AISystems` runs batched k-nearest jobs per sensor (`SpatialKNearestBatchJob`).
  - Registry systems currently do not cache cell identifiers; they rely on positions for follow-up queries.
- **Gaps**:
  - No partial rebuild or dirty-position tracking beyond transform change filters.
  - Logistics/miracle/domain systems still query registries or EntityManager directly instead of spatial queries.
  - Rewind mode skips spatial updates entirely; no metadata snapshot is maintained for playback/catch-up verification.
  - No instrumentation for cell counts, rebuild latency, or grid occupancy.

## Target Contract

1. **Provider Interface**
   - Define a `ISpatialGridProvider` abstraction to allow hashed grid (current) and future hierarchical/BVH providers.
   - Provider responsible for: config validation, rebuild scheduling (dirty flags), query dispatch, and instrumentation hooks.
   - Document provider contract here and expose via `SpatialGridConfig.ProviderId` (0 = hashed grid default).

2. **Config & State**
   - `SpatialGridConfig` remains the authoritative singleton; clarify required fields (cell size, bounds, hash seed, provider).
   - `SpatialGridState` tracks double-buffer index, total entries, version, last update tick. Add room for provider-specific state (e.g., `uint SpatialBuildVersion`).
   - Config changes trigger full rebuild; record last applied hash/extent for change detection.

3. **Registry Handles**
   - `SpatialRegistryMetadata` continues to mirror `RegistryDirectory` handles each rebuild.
   - Registries now cache the latest spatial rebuild version to detect stale entries (`SpatialGridState.Version`).
   - Resource and storehouse registries populate `CellId` + `SpatialVersion` per entry when the grid is available; additional domains can follow the same pattern.

4. **Rewind Contract**
   - Spatial grid rebuilds only during `RewindMode.Record`; playback/catch-up reuse cached buffers.
   - Document expectation: consumers must guard against stale cell data when rewinding (e.g., re-query positions if `SpatialGridState.Version` hasn’t advanced).
   - Investigate lightweight snapshot (e.g., storing `SpatialGridState` + diff metadata) if deterministic rewind validation requires it.

5. **Dirty Detection / Partial Rebuild**
   - Introduce optional dirty-list tracking: reuse `SpatialIndexedDirtyTag` or maintain last-known positions hashed per entity to skip full rebuild unless thresholds exceeded.
   - Outline a provider-agnostic approach: changed transforms go through a staging buffer; provider decides whether to rebuild affected cells or fallback to full rebuild.

6. **Query Extensions**
   - Expand `SpatialQueryHelper` with data-driven descriptors: support multiple radii in batch, filter callbacks that leverage registry data (e.g., only resources of type X).
- Provide Unity Jobs wrappers (IJobParallelFor) for radius, k-NN, and AABB batches so AI/logistics systems can schedule queries Burst-safe.
- Document deterministic sorting and tie-breaking (entity index first, optional version second).
- Add helpers to return both entity and cached registry handle/cell metadata once registries store spatial tokens.

7. **Instrumentation**
   - Add optional `SpatialConsoleInstrumentation` companion to the registry logger: logs cell count, total entries, rebuild duration/tick.
   - Hook into `DebugDisplaySystem` to surface spatial stats (total indexed entities, rebuild cadence, search hot spots).
   - Status: `SpatialConsoleInstrumentation` and HUD telemetry shipped (grid cells/entries/version exposed via DebugDisplay telemetry, console logging gated by grid singleton component).
   - Rain clouds now opt into `SpatialIndexedTag`, letting miracle entities appear in grid queries/HUD spatial diagnostics without manual tagging.
   - Villager job assignment/delivery consume the cached `CellId`/`SpatialVersion` on registry entries to filter nearby targets before falling back to distance checks; ensure partial-rebuild integrations continue updating `LastSpatialVersion` so these heuristics stay valid.

## Planned Changes

- **Systems**
  - `SpatialGridBuildSystem`: introduce provider interface; add double-buffer swap semantics for partial rebuild; record rebuild duration and dirty counts.
  - `SpatialGridInitialBuildSystem`: honour provider initialization, seed registry handles, optionally run early instrumentation.
  - Future: add `SpatialGridDirtyTrackingSystem` before rebuild to collate transform changes, enabling incremental updates.

- **Utilities**
  - Extend `SpatialUtilities.cs` with provider-agnostic hashing helpers and deterministic sort utilities shared by registries.
  - Add job structs for batched radius queries with filtering (resource type, faction) to reduce custom loops in consumer systems.

- **Registry Integration**
  - Update `ResourceRegistryEntry` / `StorehouseRegistryEntry` to optionally store `CellId` once spatial service exposes it; maintain fallback to position when spatial data absent.
  - Ensure registry update systems compare spatial rebuild version to decide whether to refresh cached cell data.
  - ✅ Villager registry now writes `CellId`/`SpatialVersion` (residency when synced, deterministic fallback otherwise) and reports resolved/fallback/unmapped counts through continuity metadata.
  - ✅ Transport registries (miner vessels, haulers, freighters, wagons) mirror the villager flow via `TransportRegistrySystem`, tagging entries with spatial metadata + availability summaries.
- Logistics registries should adopt the same pattern (cell id + spatial version) once transport entities are indexed.
- Document consumer responsibilities: query routines must fall back to registry/transform data when spatial metadata missing or stale (e.g., before first rebuild or during rewind).

## Testing & Validation Plan

- **Unit Tests**
  - Hash determinism (`SpatialHash.MortonKey`), config change detection, dirty-list behaviour.
  - Provider selection fallback when invalid provider ID.
  - Registry entry spatial token update logic (once implemented).

- **Playmode Tests**
  - Rebuild with large entity counts (existing performance test, extend with partial-rebuild scenario).
  - Rewind playback scenario ensuring spatial grid state remains stable when mode != `Record`.
  - Registry + spatial alignment: spawn resources/storehouses/logistics units, rebuild grid, verify registry entries (when extended) match spatial cell IDs / provider tokens.

- **Instrumentation Smoke**
  - Add playmode check verifying `SpatialConsoleInstrumentation` emits a `[Spatial]` log with expected counts.

## Documentation Updates

- This file (`Docs/DesignNotes/SpatialPartitioning.md`) becomes the canonical spatial service design spec; keep in sync with `ResourceRegistryPlan.md` when registry contracts change.
- Update `Docs/TODO/SpatialServices_TODO.md` as milestones complete; note registry alignment tasks.
- Reflect major decisions in `Docs/Progress.md` once implementation slices land.

## Outstanding Questions

- Do we snapshot spatial buffers for rewind playback or rely on registry/time adapters?
- How aggressively should partial rebuild attempt to avoid full rebuild (threshold heuristics)?
- What instrumentation (cells/sec, rebuild ms) do designers need surfaced in UI vs. console?
- How do registries and spatial providers coordinate cell metadata updates (e.g., extend registry entries automatically vs. consumer opt-in)?
