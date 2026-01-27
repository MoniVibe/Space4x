# Resource Authoring, Consumption, & Registry Integration

## Goals
- Provide a unified workflow for defining resources, consumption rules, and registry wiring across Godgame and Space4x.
- Ensure resources flow deterministically from authoring assets through baking, registries, consumption systems, and analytics.

## Authoring
- **ResourceTypeCatalog**: ScriptableObject listing all resource ids, categories, stack rules, spoilage parameters, icon references. Baker emits `BlobAssetReference<ResourceTypeIndex>` (see `ResourceRegistryPlan.md`).
- **ResourceSourceAuthoring**: MonoBehaviour for resource nodes (mines, forests, farms). Fields:
  - `ResourceTypeId`, `InitialQuantity`, `RegrowthRate`, `QualityTier`.
  - Optional `SpawnProfile` reference for pooling integration.
  - Baker attaches `ResourceSource` component and registers entity for spatial indexing.
- **StorehouseAuthoring**: defines storage capacity by resource category, access policies, security flags. Baker populates `Storehouse` component and entry in `StorehouseRegistry`.
- **ConsumptionProfile`**: ScriptableObject mapping resource types to consumption rates per agent/job. Baker produces blob consumed by needs systems (villagers, animals, fleets).
- **RecipeAuthoring**: see `ProductionChains.md`—inputs, outputs, facility requirements.

## Baking Pipeline
- Bakers run in deterministic order, pushing blob references into singleton components (`ResourceRuntimeConfig`, `ConsumptionProfileSet`).
- Validation:
  - Ensure every `ResourceTypeId` in authoring assets exists in `ResourceTypeCatalog`.
  - Check `StorehouseAuthoring` capacity arrays match catalog entries.
  - Warn when consumption profiles reference missing resource types or negative rates.
- Conversion attaches `SpatialIndexedTag` for gatherable resources if spatial services active.

## Registries
- `ResourceRegistrySystem`:
  - Rebuilds buffers with `ResourceRegistryEntry` (type, entity, units, cell, version).
  - Integrates with `RegistryContinuityContracts` (spatial version tracking, metadata).
- `StorehouseRegistrySystem`:
  - Aggregates per-type capacity/stock, exposes quick lookup for logistics.
- `ConsumptionRegistry` (new concept):
  - Tracks active consumption contexts (villager needs, upkeep timers) with `ConsumptionRegistryEntry` (consumer entity, resource type, rate, next tick).
  - Enables analytics and shortage detection.
- Registries emit events via `RegistryInstrumentationSystem` and log to telemetry service.

## Consumption Logic
- **VillagerNeedsSystem**: reads `ConsumptionProfile` blobs, decrements storehouse resources through logistics reservations, updates villager need meters, logs deficits.
- **UpkeepSystems** (buildings, fleets, elites): maintain `UpkeepState` components with per-tick resource costs; failure triggers narrative/penalties (tie into event system).
- **SpoilageSystem**: processes resources with decay timers (food, organics) using SoA layout to avoid per-entity overhead.
- **FIFO Consumption**: align with Space4x needs (see TODO `4xdotsrequest.md`); maintain per storehouse queue of batches sorted by `ArrivalTick`.

## Services Integration
- Economy/trade adjusts consumption rates via modifiers (famine, surplus).
- Tech/culture unlock alternative consumption (magic energy vs. food).
- Military logistics reserves resources for campaigns; integrates with scheduler.
- Narrative situations inject or drain resources based on event outcomes.

## Testing
- Edit-mode: `ResourceRegistryTests`, `StorehouseRegistryTests`, add `ConsumptionProfileTests` verifying baking consistency, rate application.
- Playmode: simulate villager population, verify resource draw matches profiles, registries update, shortages trigger events.
- Performance: stress tests with 100k consumption contexts ensure Burst jobs maintain budgets.
