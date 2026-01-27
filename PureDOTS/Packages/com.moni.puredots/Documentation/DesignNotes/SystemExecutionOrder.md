# System Execution Order

_Updated: 2025-10-25_

This document captures the canonical execution ordering for the Pure DOTS simulation. All systems must target the groups and dependencies described here so climate, spatial, gameplay, and presentation layers consume consistent data. Refer to Docs/TruthSources/RuntimeLifecycle_TruthSource.md for the authoritative overview and update both files together.

## High-Level Ordering
- **InitializationSystemGroup** – boots configuration and performs one-off grid/index builds (via TimeSystemGroup).
- **SimulationSystemGroup** (per frame)
  1. BuildPhysicsWorld
  2. **EnvironmentSystemGroup** (climate, environment grids)
  3. **SpatialSystemGroup** (grid rebuilds & derived spatial caches)
  4. **GameplaySystemGroup** (villagers, resources, miracles, vegetation, construction, hand IO)
  5. **LateSimulationSystemGroup** (History + rewind prep)
  6. ExportPhysicsWorld
- **PresentationSystemGroup** consumes the resulting state; toggled by rewind guards so playback can run without resimulating gameplay.

Assets/Scripts/PureDOTS/Systems/SystemGroups.cs contains the authoritative attribute configuration. Any change to ordering must update that file, this document, and the runtime truth source together.

## Group Details

### TimeSystemGroup
Runs at the start of InitializationSystemGroup to seed deterministic singletons and align Unity's player loop with PureDOTS timing.
- CoreSingletonBootstrapSystem
- TimeSettingsConfigSystem
- HistorySettingsConfigSystem
- GameplayFixedStepSyncSystem

### EnvironmentSystemGroup
Runs immediately after physics and before any spatial or gameplay work. Systems in this group **must** preserve the following order (top -> bottom):
1. EnvironmentEffectUpdateSystem
2. Climate derivations (biome, rainfall, etc.)

Guidelines:
- Always write additive contributions to buffers; consumers use EnvironmentSampling.
- Update LastUpdateTick/LastTerrainVersion after writing.

### SpatialSystemGroup
Produces spatial indices used by gameplay systems.
- SpatialGridBuildSystem (OrderFirst)
- Flow-field/nav helpers
- Debug/observability systems (optional)

The spatial rebuild updates SpatialRegistryMetadata so registries know which handles are spatialised. Consumers must [UpdateAfter(typeof(SpatialSystemGroup))] if they depend on these handles.

### GameplaySystemGroup
Contains high-level domain subgroups, executed after spatial rebuilds.
1. AISystemGroup
2. VillagerJobFixedStepGroup (inside FixedStepSimulationSystemGroup, but called out here for clarity)
3. VillagerSystemGroup
4. ResourceSystemGroup
5. VegetationSystemGroup
6. MiracleEffectSystemGroup
7. HandSystemGroup (HandInputRouterSystem OrderFirst -> DivineHandSystem)
8. ConstructionSystemGroup

Rules of thumb:
- If a system consumes spatial data, ensure it runs after the spatial group.
- Climate-dependent systems either live in environment or add [UpdateAfter(typeof(EnvironmentSystemGroup))].
- Domain registries should be refreshed before dependent systems execute.
- The hand routing pipeline is: hybrid HandCameraInputRouter -> HandInputRouterSystem -> DivineHandSystem.

### LateSimulationSystemGroup & HistorySystemGroup
LateSimulationSystemGroup is marked OrderLast inside simulation and hosts cleanup/state capture. HistorySystemGroup sits within it to record state every tick. Systems writing history should either live in HistorySystemGroup or [UpdateBefore(typeof(HistorySystemGroup))] to guarantee ordering.
- MoistureGridTimeAdapterSystem (HistorySystemGroup) serialises environment moisture buffers for rewind playback via the shared `TimeAwareController`.
- StorehouseInventoryTimeAdapterSystem (HistorySystemGroup) snapshots storehouse totals so resource deliveries rewind correctly.
- VillagerJobTimeAdapterSystem now uses `TimeAwareController` to gate record/playback/catch-up, replacing bespoke `RewindState` checks.

### PresentationSystemGroup
Consumes simulation data for rendering/UI. Guarded by PresentationRewindGuardSystem to pause updates during catch-up rewinds.

## Rewind Guards
- EnvironmentRewindGuardSystem
- SpatialRewindGuardSystem
- GameplayRewindGuardSystem
- PresentationRewindGuardSystem

These systems toggle group execution based on RewindState.Mode. Any new top-level group must either reuse one of these guards or introduce its own guard system to remain rewind-safe.

## Implementation Checklist
- When adding a new environment system, confirm it resides in EnvironmentSystemGroup and declares ordering that honours the pipeline.
- When adding new gameplay systems that consume climate or spatial data, ensure they either live in the appropriate subgroup or add explicit UpdateAfter attributes.
- If a domain introduces a new group, add it to SystemGroups.cs, this document, and RuntimeLifecycle_TruthSource.md.
- For spatial/registry changes, keep Assets/Tests/Playmode/SpatialRegistryPerformanceTests.cs updated when expectations change.




