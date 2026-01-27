# Registry Continuity Contracts

Updated: 2025-11-02

> Active work is tracked in `Docs/TODO/RegistryRewrite_TODO.md` (registry implementation) and `Docs/TODO/SpatialServices_TODO.md` (spatial sync). Use this note for contract/context and refresh it when TODO items complete.

## Purpose
Phase 2 of the roadmap calls for "registry–spatial continuity contracts": interface guarantees and tests that prove every registry stays in sync with the spatial grid and deterministic ordering regardless of producer order. This note defines those guarantees, outlines the implementation work, and lists the validation we need before the roadmap slice can be marked complete.

## Current Snapshot
- Resource, storehouse, and villager registries already rebuild through `DeterministicRegistryBuilder<T>` with deterministic ordering by `Entity.Index`.
- Registries cache spatial hints (`CellId`, `SpatialVersion`) when the spatial grid service is available, but consumers must call helper utilities to populate the fields and still fall back to positions frequently.
- `RegistryHealthSystem` reports stale entry counts, but the metrics do not yet distinguish spatial drift vs. rebuild churn.
- No automated tests exercise registry order stability when entity queries return results in different orders, nor do we validate behaviour when partial spatial rebuilds deliver cell updates out of phase.
- Miracles, construction, logistics, and transport registries are still pending; their schemas are defined in TODOs but not wired into the continuity pipeline.
- A shared `RegistrySpatialSyncState` singleton now publishes the latest spatial grid version each frame; registries consume it when producing continuity snapshots.
- Metadata now retains a `RegistryContinuitySnapshot` (spatial version + resolved/fallback/unmapped counters) when registries rebuild, and `DeterministicRegistryBuilder<T>.ApplyTo(...)` accepts the snapshot so systems can report continuity state without bespoke plumbing.
- `RegistryHealthSystem` consumes the continuity snapshot so registries marked `RequiresSpatialSync` degrade to failure when spatial data is missing.
- Transport and logistics request registries now publish continuity snapshots and aggregate counters (`TransportRegistrySystem`, `LogisticsRequestRegistrySystem`).

## Continuity Invariants To Enforce
1. Deterministic order: registry buffers must sort entries consistently (entity index asc) regardless of how producers discover entities or how many rebuild passes run per tick.
2. Monotonic metadata: `RegistryMetadata.Version` increments exactly once per logical rebuild, and `LastUpdateTick` reflects the tick that produced the buffer written to consumers.
3. Spatial coherence: when `SpatialGridState.Version` advances, registries that claim `SupportsSpatialQueries` must refresh `CellId` and `SpatialVersion` for every entry within the same frame. Entries must never mix grid versions.
4. Dirty tolerance: partial rebuilds (dirty subsets) cannot leave entries with stale spatial metadata. If dirty data cannot be resolved, registries must mark fallback counts and emit a health warning.
5. Rewind safety: when `RewindState.Mode != Record`, registries skip mutations but keep prior buffers accessible. On returning to record mode, the first rebuild must produce identical buffers for unchanged simulation state.
6. Cross-domain parity: all registries (villager, resource, storehouse, miracle, construction, logistics) adhere to the same metadata contract so shared helpers (`RegistryDirectoryLookup`, spatial filters, AI consumers) can treat them uniformly.

## Implementation Plan
1. Codify metadata contract
   - Extend `RegistryMetadata.MarkUpdated` to accept the spatial version that populated the entries.
   - Add assertions in debug builds to ensure `SupportsSpatialQueries` registries always pass a spatial version when updating.
2. Registry builder enhancements
   - Update `DeterministicRegistryBuilder<T>` to accept an optional `uint spatialVersion` and record how many entries updated via spatial hints vs. fallbacks.
   - Record diagnostic counters (resolved, fallback, unmapped) for all registries, not only resource/storehouse, and expose them to instrumentation.
3. Spatial handshake
   - Introduce a lightweight `RegistrySpatialSyncSystem` that runs after spatial rebuild and before consumer groups. It publishes the current grid version and enforces that all registries observing `SupportsSpatialQueries` rebuild when the version changes. _Status: system now publishes `RegistrySpatialSyncState`, and `RegistryHealthSystem` flags registries that skip required spatial data; version-delta regression tests still pending._
   - Provide helpers for registries to request lazy updates (e.g., copy cached `CellId` when transform was unchanged during partial rebuild).
4. Domain rollout
   - Resource and storehouse registries: integrate with the handshake, populate spatial counters, add debug assertions.
   - Villager registry: cache `CellId` once transport logistics grid slice lands; enable continuity metrics.
   - Construction, miracle, logistics registries: implement buffer schemas with spatial metadata from day one and register them with the directory.
5. Observability
   - Extend `RegistryHealthSystem` to flag mismatched spatial versions, stale cell ids, and registries that skipped mandatory rebuilds.
   - Emit `[RegistryContinuity]` console logs when mismatches occur (toggle via instrumentation component) to aid CI triage.

## Validation Matrix
1. EditMode unit tests (new `RegistryContinuityTests.cs`)
   - `RegistryBuilder_StableOrder_WhenInputOrderChanges`: feed entries in differing orders, ensure buffer order matches expected canonical sort.
   - `RegistryBuilder_MarksMetadataOnce_PerRebuild`: verify metadata version increments exactly once per rebuild invocation.
   - `RegistryBuilder_MismatchedSpatialVersion_Throws`: for registries flagged with spatial support, ensure failing to pass a spatial version triggers an assertion in development builds.
2. Playmode tests (new suite `RegistrySpatialContinuityPlaymodeTests`)
   - `PartialRebuild_PreservesSpatialVersion`: spawn entities, run spatial rebuild, mutate a subset, trigger partial rebuild, ensure all registry entries share the new grid version.
   - `ProducerOrder_Shuffle_DoesNotChangeEntries`: reorder villager/resource producers (spawn/despawn in shuffled order) and ensure registry buffers remain identical after rebuild.
   - `Rewind_RoundTrip_RestoresRegistry`: record -> mutate -> rewind -> catch-up -> record; confirm registry buffers and metadata return to pre-mutation state.
   - `SpatialFallback_Warns`: simulate missing spatial providers and assert registry health counters report fallback cases without crashing consumers.
3. Snapshot diff test
   - Integrate with replay harness: capture registry snapshots before and after deterministic sample runs, diff buffers to guarantee stable ordering and spatial coherence.

## Tooling & Docs
- Add a section to `Docs/Guides/SceneSetup.md` referencing the continuity contract and expected component wiring for registries that depend on spatial metadata.
- Update `Docs/TODO/RegistryRewrite_TODO.md` to reference this plan and link tasks to the relevant milestones below.
- Extend `Registry health` HUD panel with columns for `SpatialVersion`, `Resolved`, `Fallback`, `Unmapped`, and highlight mismatches.

## Dependencies
- Spatial grid must expose its version per frame (`SpatialGridState.Version`) and notify when partial rebuilds occur.
- Replay/rewind helpers from `TimeAware` need to remain stable; registry tests depend on accurate mode transitions.
- Miracles/logistics registries must land their base schemas to participate in continuity validation.

## Milestones
1. Contract scaffolding (metadata extension, builder updates, spatial handshake) – target 2 days.
2. Resource/storehouse continuity tests and instrumentation – target 2 days.
3. Villager registry spatial integration – target 1 day.
4. Remaining registries adopt contract (miracle, construction, logistics) – per domain slices, estimate 3 days total once schemas exist.
5. Playmode suite + replay diff harness integration – target 2 days.

The slice is "done" when all registries implement the contract, the new tests pass in CI, and the health telemetry reports no continuity violations across smoke scenarios.
