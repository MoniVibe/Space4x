# System Integration Baseline Audit

_Updated: 2025-10-25_

## Environment & Climate Data Components
- No runtime definitions detected for `MoistureGrid`, `TemperatureGrid`, `SunlightGrid`, or `WindField` in `Assets/Scripts` (repo-wide search via `rg "struct\\s+MoistureGrid"`).
- `ClimateState` and `FlowFieldData` likewise absent from compiled sources; only referenced inside TODO documents.
- Implication: shared data contracts remain doc-only and can be centralised in the forthcoming `EnvironmentGrids.cs` without migration risk.

## ScriptableObject Profile Inventory
| Asset | Path | Namespace | Owner (proposed) | Notes |
| --- | --- | --- | --- | --- |
| `SpatialPartitionProfile` | `Assets/Scripts/PureDOTS/Authoring/SpatialPartitionProfile.cs` | `PureDOTS.Authoring` | Spatial/Simulation | Produces `SpatialGridConfig` plus runtime buffers; already wired into bakers. |
| `PureDotsRuntimeConfig` | `Assets/Scripts/PureDOTS/Authoring/PureDotsConfigAssets.cs` | `PureDOTS.Authoring` | Core/Runtime | Hosts `TimeSettingsData`, `HistorySettingsData`, and references `ResourceTypeCatalog`. |
| `ResourceTypeCatalog` | `Assets/Scripts/PureDOTS/Authoring/PureDotsConfigAssets.cs` | `PureDOTS.Authoring` | Resources | Catalog of resource ids and colors (current stand-in for `ResourceProfile`). |
| `VegetationSpeciesCatalog` | `Assets/Scripts/PureDOTS/Config/VegetationSpeciesCatalog.cs` | `PureDOTS.Config` | Vegetation | Detailed per-species growth/health thresholds; converts to blob at bake. |
| `HandCameraInputProfile` | `Assets/Scripts/PureDOTS/Input/HandCameraInputProfile.cs` | `PureDOTS.Input` | Hand/Interaction | Configures Divine Hand input map plus router timing values. |
| *(Missing)* `ClimateProfile` | — | — | Environment | Not yet implemented; tracked in `ClimateSystems_TODO`. |
| *(Missing)* `MiracleProfile` | — | — | Miracles | Not yet implemented; tracked in `MiraclesFramework_TODO`. |
| *(Suggested)* `EnvironmentGridConfig` | — | — | Environment | To be introduced alongside shared grid definitions. |

## System Group Topology (Current)
Source: `Assets/Scripts/PureDOTS/Systems/SystemGroups.cs`, `Assets/Scripts/PureDOTS/Systems/RewindRoutingSystems.cs`.

- `InitializationSystemGroup`
  - `TimeSystemGroup` (`OrderFirst`)
- `SimulationSystemGroup`
  - `FixedStepSimulationSystemGroup`
    - `VillagerJobFixedStepGroup`
  - `VillagerSystemGroup` (`UpdateAfter FixedStepSimulationSystemGroup`)
  - `ResourceSystemGroup` (`UpdateAfter VillagerSystemGroup`)
  - `CombatSystemGroup` (`UpdateAfter BuildPhysicsWorld`, `UpdateBefore ExportPhysicsWorld`)
  - `HandSystemGroup` (`UpdateAfter BuildPhysicsWorld`, `UpdateBefore ExportPhysicsWorld`)
  - `VegetationSystemGroup` (`UpdateAfter FixedStepSimulationSystemGroup`)
  - `ConstructionSystemGroup` (`UpdateAfter ResourceSystemGroup`)
  - `RewindModeRoutingSystem`
    - `RecordSimulationSystemGroup`
    - `CatchUpSimulationSystemGroup`
    - `PlaybackSimulationSystemGroup`
  - `LateSimulationSystemGroup` (`OrderLast`)
- `LateSimulationSystemGroup`
  - `HistorySystemGroup`

No dedicated environment or miracle sub-groups exist yet; planned workstreams will extend Simulation accordingly.

## Time & Rewind Implementation Status
- `TimeState`, `TimeSettingsConfig`, and defaults live in `Assets/Scripts/PureDOTS/Runtime/TimeComponents.cs`; functionality present and ready for integration.
- `RewindState` (with `RewindMode`) defined in the same file and already drives `RewindModeRoutingSystem` enablement switching between record/catch-up/playback groups.
- `HistorySettingsData`/`HistorySettings` flows are available via `PureDotsRuntimeConfig` authoring asset.
- No `GameplayFixedStep` (component, system, or group) currently exists in the codebase (`rg "GameplayFixedStep"` returns no results). Requirements should be clarified before referencing it in docs.

## Next Actions
1. Implement shared environment grid data (`EnvironmentGrids.cs`) without refactor risk.
2. Formalise ownership + namespaces for missing profiles (`ClimateProfile`, `MiracleProfile`, `EnvironmentGridConfig`).
3. Extend system topology with the proposed `EnvironmentSystemGroup` and cascade updates through dependent TODOs.
