# Vegetation Lifecycle, Chunk Layout, and Rewind Notes

## Purpose
- Define the vegetation growth -> harvest -> respawn loop before systems are written.
- Show how vegetation timelines plug into the existing `TimeTickSystem` and rewind spine.
- Capture recommended hot vs cold archetype splits (villagers plus vegetation) with rough memory budgets so the 100k-entity target remains realistic.
- Provide acceptance criteria that gate performance and rewind validation for this domain.

## Vegetation State Machine
- **Lifecycle stages** map onto `VegetationLifecycle.LifecycleStage` and drive tags used by other systems.
  - `Seedling` -> created via baker or reproduction; initializes timers and subscribes to soil and water checks.
  - `Growing` -> accumulates `GrowthProgress` toward 1.0 using `growthRate * dt * seasonMultiplier`.
  - `Mature` -> yields structural resources (logs, fibers) and waits for fruit counters to fill.
  - `Fruiting` -> `VegetationReadyToHarvestTag` present; `CurrentProduction` accumulates until a villager harvests or capacity is reached.
  - `HarvestCooldown` (tracked with a timer component instead of reusing stage values) -> plant is partially reset, no resource output, prevents repeated harvesting in back to back ticks.
  - `RegrowthSeed` (seedling stage plus a regrowth tag) -> handles respawn delay after full harvest or death.
- **Transition rules per tick**
  - Stage timers increment via the deterministic delta from `TimeState.FixedDeltaTime`.
  - `Seedling -> Growing` when soil and water thresholds pass and a deterministic random check succeeds. Use `Unity.Mathematics.Random` seeded by species id and cell index, and record the seed in snapshot data for rewind.
  - `Growing -> Mature` when `GrowthProgress >= 1.0`; append a `VegetationHistoryEvent` with `StageTransition`.
  - `Mature -> Fruiting` when seasonal gate passes and production buffers have capacity.
  - `Fruiting -> HarvestCooldown` when villagers issue a harvest command; transfer `CurrentProduction` to storehouse and record `LastHarvestTick`.
  - `HarvestCooldown -> Growing` when the cooldown timer expires and health remains above zero.
  - Any stage -> `Dying` or `Dead` when health reaches zero because of drought, fire, or over-harvest. Dead entities either despawn or queue a `RegrowthSeed` flow after a configurable respawn delay.
- **Planned systems**
  - `VegetationGrowthSystem` (ISystem, Burst) updates timers, health modifiers, and stage transitions. Schedule in `VegetationSystemGroup` immediately after environmental modifier systems.
  - `VegetationHarvestSystem` consumes villager requests, updates production counters, and raises history samples.
  - `VegetationRespawnSystem` counts down regrowth timers and issues structural changes through an `EntityCommandBuffer`.
  - History logging remains lightweight: append to `VegetationHistoryEvent` buffers with `(tick, value)` payloads and cap entries to four per lifecycle to avoid buffer bloat.
  - All structural changes (adding/removing tags, spawning offspring) must go through an `EntityCommandBuffer` obtained from `BeginSimulationEntityCommandBufferSystem` to keep Burst-compatible jobs deterministic. Where feasible, prefer enableable components (`IEnableableComponent`) for tags like `VegetationStressedTag` or `VegetationReadyToHarvestTag` so systems can toggle them without adding/removing components. If enableables are unavailable, conditional add/remove must happen inside ECB playback and remain guarded by `WithNone<PlaybackGuardTag>`.

## Authoring and Config Data
- **VegetationSpeciesCatalog (ScriptableObject)**
  - `List<VegetationSpeciesDefinition> species` stored in ascending order by `speciesId` (string). Baker converts to a blob asset (`BlobAssetReference<VegetationSpeciesCatalogBlob>`) for fast runtime lookup via `VegetationId.SpeciesType`.
  - `VegetationSpeciesDefinition` fields:
    - `string speciesId` (trimmed, case-insensitive) -> becomes `ushort` index baked into `VegetationId`.
    - `StageDurations stageDurations` (floats in seconds for seedling, growing, mature, flowering, fruiting, dying, respawnDelay).
    - `GrowthSettings growth` (base growth rate, per-stage multipliers, seasonal multipliers keyed by `VegetationSeasonal.SeasonType`).
    - `HarvestSettings harvest` (max yield per cycle, cooldown seconds, replenish curve id, resource type index, partial harvest penalty).
    - `HealthSettings health` (max health, baseline regen, damage per deficit, drought tolerance seconds, frost tolerance seconds).
    - `EnvironmentThresholds environment` (desired min/max water, light, soil quality, pollution tolerance, wind tolerance).
    - `ReproductionSettings reproduction` (cooldown seconds, seeds per event, spread radius, offspring cap per parent, maturity requirement, grid cell padding).
    - `RandomSeeds seeds` (`uint growthSeed`, `uint reproductionSeed`, `uint lootSeed`). Bakers store these in the blob so deterministic `Unity.Mathematics.Random` can use `Random.CreateFromIndex(seed ^ tick)`.
  - Additional catalog-level fields:
    - `float globalHealthRecoveryScale`, `float globalDamageScale` to tune across all species.
    - `float defaultSpawnDensity` for procedural placement.
    - `AnimationCurve ageScaleCurve` (sampled offline into baked lookup table to avoid runtime allocations).
- **VegetationEnvironmentProfile (ScriptableObject)**
  - Defines baseline water, light, soil, and seasonal oscillations per biome.
  - Baker writes a `VegetationEnvironmentConfig` singleton with:
    - `float dailyLightCurve[24]`, `float seasonalWaterCurve[4]`.
    - `float soilRegenerationPerSecond`, `float droughtPenaltyScale`, `float frostPenaltyScale`.
    - `uint environmentSeed` used for deterministic noise sampling.
- **Runtime components produced by bakers**
  - `VegetationSpeciesLookup` singleton contains blob reference and exposes helper `Lookup(speciesIndex)`.
  - Each vegetation entity receives:
    - `VegetationSpeciesIndex` (ushort) to access blob data.
    - `VegetationRandomState` storing the last random sample indices (uint counters) so rewind can restore deterministic sequences without reinitializing RNG.
    - `VegetationEnvironmentState` (float water, light, soil, pollution, wind, lastSampleTick).
  - Config bakers run before conversion of vegetation prefabs so every instance has a valid species index and random seed at creation.

## Environmental System Blueprint
- **Inputs**
  - `VegetationEnvironmentState` updated by a sampling pass that reads from a spatial grid (`EnvironmentGridCell` chunk or singleton) populated by weather, terrain, and irrigation systems.
  - Species thresholds obtained from `VegetationSpeciesLookup`.
  - Optional `WeatherEventBuffer` (global) signals extreme events (storm, drought) that modulate the thresholds.
- **Processing flow (VegetationHealthSystem)**
  1. Execute in `VegetationSystemGroup` **before** `VegetationGrowthSystem` and after any environment sampling system.
  2. For each vegetation entity:
     - Compare `EnvironmentState` values to species thresholds. Compute normalized deficits (0-1) for water, light, soil, pollution, temperature.
     - Apply health deltas:
       - If all values within ideal band -> increase `VegetationHealth.Health` toward `MaxHealth` using `health.RegenerationRate * deltaTime * globalHealthRecoveryScale`.
       - If deficits present -> subtract `health.DamagePerDeficit * deficitMagnitude * deltaTime * globalDamageScale`. Accumulate separate timers for drought/frost to respect tolerance seconds.
     - Update derived flags:
       - Add `VegetationStressedTag` when deficit > 0.4 for longer than configured buffer.
       - Add `VegetationDyingTag` when health < `DyingThreshold` (species defined) and remove when recovery occurs.
       - Add `VegetationDeadTag` if health reaches zero; zero out `CurrentProduction`.
     - Update `VegetationLifecycle.CurrentStage` to `Dying` when threshold hit so growth system responds next frame.
  3. Store results in history buffers (`VegetationHistoryEvent` type `Damage` with value = deficit) when thresholds crossed to allow analytics.
- **Rewind handling**
  - `VegetationHealth` and `VegetationEnvironmentState` components are part of the state root so snapshots capture the latest health and environment values.
  - `VegetationRandomState` counters ensure reapplying environment noise yields identical sequences on re-sim.
  - During playback (`PlaybackGuardTag` present) the system is skipped because of the `RewindState` check, mirroring growth system behavior.
- **Ordering requirements**
  1. `EnvironmentGridSamplingSystem` (produces per-cell environment data).
  2. `VegetationEnvironmentSamplingSystem` (writes to `VegetationEnvironmentState`).
  3. `VegetationHealthSystem`.
  4. `VegetationGrowthSystem`.
  5. `VegetationHarvestSystem`.
  6. `VegetationRespawnSystem`.
  - Register ordering in `Docs/SystemOrdering/SystemSchedule.md` once implemented.

## Harvest and Reproduction Flow
- **Harvest command staging**
  - Introduce a `VegetationHarvestCommand` buffer on a singleton (`VegetationHarvestCommandQueue`). Element fields: `Entity villager`, `Entity vegetation`, `ushort speciesIndex`, `float requestedAmount`, `uint issuedTick`, `uint commandId`.
  - `VillagerHarvestPlannerSystem` (runs after job assignment) writes commands into the queue using an `EntityCommandBuffer`. It also reserves the target plant by setting a `VegetationReservedTag` to prevent duplicate assignments.
  - `VegetationHarvestSystem` reads the queue in record mode:
    - Validate the vegetation entity still has `VegetationReadyToHarvestTag` and sufficient `CurrentProduction`.
    - Consume the requested amount, update `CurrentProduction`, and push a `HarvestReceipt` (buffer) containing `villager`, `vegetation`, `resourceType`, `yieldAmount`, `issuedTick`, `commandId`.
    - Clear `VegetationReadyToHarvestTag` when yield hits zero and start cooldown timer from species catalog.
  - Store receipts in `HarvestHistoryBuffer` (attached to vegetation entity) so rewinds can remove any receipts with `EventTick > targetTick` and restore production totals.
  - `StorehouseDeliverySystem` processes receipts, adding resources to storehouses and stamping an opposite history event for rollback symmetry.
- **Rollback plan**
  - `VegetationHarvestSystem` maintains a `ProcessedCommandCounter`. When rewinding, commands with `issuedTick > rewindTarget` are ignored; history trimming restores `CurrentProduction` using stored yield amounts.
  - `VegetationReservedTag` is removed during rewind by the `RewindCoordinatorSystem` cleanup so villagers can replan once the timeline diverges.
- **Reproduction and spread**
  - Use `VegetationReproductionSystem` scheduled after harvest but before respawn.
    - Reads `VegetationReproduction` component and species blob entry.
    - Checks `ReproductionTimer` against `ReproductionSettings.cooldownSeconds`.
    - Queries `VegetationGridData` (spatial bin) to ensure `MaxOffspringRadius` capacity is not exceeded.
    - For each offspring:
      - Use deterministic random seeded by `species.RandomSeeds.reproductionSeed ^ parentId ^ loopIndex ^ currentTick`.
      - Select a target cell from the grid respecting pad distance; ensure biome compatibility by reading `VegetationEnvironmentProfile`.
      - Enqueue spawn via `EntityCommandBuffer` referencing a baked prefab from `VegetationSpawnConfig`.
    - Update parent timers and record a `VegetationHistoryEvent` of type `Reproduced` with value equal to spawned count.
  - `VegetationSpawnSystem` consumes the queued spawns at the end of the frame, assigning species index, initial environment state, and random seeds.
  - Ensure reproduction respects rewind by storing offspring command ids and removing any whose `issuedTick` exceeds the rewind target.

## Time and Rewind Integration
- Vegetation entities must carry `RewindableTag` so `RewindCoordinatorSystem` can add or remove `PlaybackGuardTag` during rewind playback.
- Update systems should read `RewindState` and early out unless `RewindState.Mode == RewindMode.Record`. This matches the pattern in `StorehouseHistoryRecordingSystem` (see `Assets/Scripts/PureDOTS/Systems/StorehouseSystems.cs`) and avoids writes during rewind playback.
- Snapshot capture follows the existing pattern:
  - Keep `VegetationHistorySample` buffers on the cold companion entity or singleton so only the summary data is stored.
  - Record `LifecycleStage`, `GrowthProgress`, `CurrentProduction`, `LastHarvestTick`, and any active cooldown or respawn timers.
  - When trimming history, reuse the `PruneOldSamples` helper shipped in `StorehouseSystems` to honor `HistorySettings` horizons.
- Deterministic guarantees:
  - Any randomness uses `Unity.Mathematics.Random` or pre-generated blob data. Persist the seed or sample index in the entity to ensure rewind produces identical outcomes.
  - Species catalog entries with zero tolerance thresholds (e.g., pollution tolerance 0) are treated as strict cutoffs: health systems should not divide by zero; they clamp deficits and mark the entity stressed immediately when the environment exceeds the bound.
  - Villager harvest commands must include an issuing tick so storehouse systems can roll back partial harvests if the timeline rewinds past the command.
  - `VegetationSpeciesIndex` is authoritative: catalog-based plants set it during baking; runtime-spawned plants must copy the index from the parent or spawn command. Manual overrides should provide a valid ushort index that maps into the catalog blob; otherwise, systems treat the entity as catalog species `0`.
- On resume after rewind (`RewindMode` transitions to `CatchUp` or `Record`), vegetation systems recompute derived state such as `SeasonMultiplier` from authoritative config data instead of relying on cached mutable values.
- Classification aligns with the "pure simulation" category from the legacy truthsource: state roots are stored, gameplay systems suppress side effects during playback, and presentation layers regenerate visuals after rewind completes.

## Chunk Archetype Layout and Memory Budgets

### Vegetation
- **Hot simulation archetype** (target <= 96 bytes per entity, about 170 entities per chunk)
  - `LocalTransform` (24 bytes) for simulation position, rotation, and scale.
  - `VegetationId` (8 bytes).
  - `VegetationLifecycle` (24 bytes).
  - `VegetationHealth` (24 bytes). Move to a cold archetype if updates are infrequent.
  - `VegetationProduction` (currently about 96 bytes because of `FixedString64Bytes`). Replace the string with a `ushort ResourceTypeIndex` pointing into a blob catalog to halve the footprint.
  - `VegetationReproduction` (24 bytes) only for species that spread.
  - `VegetationSeasonal` (16 bytes).
  - Consolidate stage tags into a packed `VegetationFlags` component to avoid multiple 16-byte tag components on the hot chunk.
  - Estimated per entity memory: roughly 152 bytes today, about 96 bytes after resource id and flag refactors.
- **Cold or presentation archetype**
  - Companion entity holds `VegetationHistoryEvent`, `VegetationSeedDrop`, LOD and FX tags, and `LinkedEntityGroup`.
  - Presentation chunk should remain below 60 bytes per entity (200 to 220 entities per chunk).
- **Budgets**
  - 60k vegetation entities in hot archetype -> about 5.8 MB after the refactor (roughly 9.1 MB with current structs).
  - Cold archetype adds roughly 3 MB and grows with history buffer length. Keep per entity history entries capped at four; otherwise worst case adds roughly 5.7 MB.

### Villagers
- **Hot simulation archetype** (target <= 128 bytes per entity, about 125 entities per chunk)
  - `LocalTransform` (24 bytes).
  - `PhysicsVelocity` (24 bytes) or `VillagerMovement` (~32 bytes) depending on the movement provider.
  - `VillagerId` (8 bytes).
  - `VillagerNeeds` (20 bytes). Investigate clamping to shorts if we need extra savings.
  - `VillagerAIState` (32 bytes).
  - `VillagerJob` (16 bytes).
  - `VillagerAvailability` (16 bytes).
  - Optionally keep `VillagerMood` (16 bytes) here only if state machines consume it every tick. Otherwise move to analytics companion.
  - Move analytics and presentation data (`VillagerStats`, `VillagerAnimationState`, inventory buffers) to a companion entity. For inventory, store a lightweight `VillagerInventoryRef` index on the hot archetype instead of the buffer header.
  - Estimated per entity memory: about 144 bytes today, about 112 bytes after the suggested split.
- **Cold or presentation archetype**
  - Holds `VillagerAnimationState`, `VillagerSensors`, `VillagerRelationship`, UI material properties, and buffers.
  - Expected occupancy 100 to 120 entities per chunk due to sensor data size.
- **Budgets at 40k villagers**
  - Hot archetype: roughly 4.5 MB after refactor (about 5.8 MB currently).
  - Cold archetype plus buffers: between 8 and 10 MB depending on relationship counts.

### Aggregate Memory Targets (100k entities example)
- Scenario uses 40k villagers and 60k vegetation.
- Hot simulation chunks total about 10.3 MB after refactors (about 13.6 MB today).
- Cold and presentation chunks add roughly 11 to 13 MB.
- Command buffers, history buffers (trimmed), and global singletons should stay within an additional 12 MB.
- Rewind snapshots must respect a 256 MB rolling budget for the template baseline. With optimized component sizes, a single snapshot covering 100k entities costs roughly 10 to 12 MB uncompressed. LZ4 compression at around 2x keeps thirty tick cadence under 80 MB, leaving headroom for branch history.

## Acceptance Criteria (100k Entities plus Rewind)
- **Simulation tick time**
  - Headless playmode scenario with 40k villagers and 60k vegetation at 60 Hz remains under 8.0 ms per tick on target hardware (Core i9-12900K class). Combined cost of `VegetationGrowthSystem` and `VillagerJobAssignmentSystem` stays below 2.5 ms per tick.
  - Structural change pass (spawn, despawn, respawn) stays below 0.5 ms per frame by batching `EntityCommandBuffer` work.
- **Memory footprint**
  - Live world memory (simulation plus cold archetypes and buffers) stays below 64 MB excluding graphics assets.
  - Rolling rewind buffer remains below 256 MB after a ten minute session at 60 Hz with thirty tick snapshot cadence. If exceeded, adjust cadence or enable delta compression for vegetation history buffers.
- **Determinism and rewind validation**
  - Deterministic soak: run a five minute script twice with identical commands and confirm snapshot checksums match within float tolerance.
  - Rewind branch test: record 120 seconds, rewind 30 seconds, issue revised harvest commands, and replay 120 seconds. Storehouse totals must diverge only by the expected delta (no double harvests).
  - Respawn correctness: rewinding across the harvest cooldown boundary must restore the proper stage and prevent duplicate `VegetationReadyToHarvestTag`.
  - History integrity: `VegetationHistoryEvent` buffers drop future entries when rewinding and regenerate replacements without duplication.
- **Health and reproduction validation**
  - Health regulation scenario:
    - Given a profile with ideal water 40-80, when irrigation toggles between drought (water 10) for 90 seconds and flood (water 90) for 90 seconds, then `VegetationHealth.Health` clamps between 0 and `MaxHealth`, and `VegetationStressedTag` (enableable or tag) is active only during deficit ticks.
    - Rewind the test to the midpoint (tick where flood starts) and re-run; health curves and stress tag enable/disable transitions must match bit-for-bit. Tests should assert both `IsComponentEnabled<VegetationStressedTag>` and stored history events.
    - Zero tolerance fallback: create a species with pollution tolerance 0.0, run an environment pulse that introduces pollution 0.05, and confirm the plant immediately enters stressed state and health decays according to zero-tolerance rules.
  - Harvest/command integrity:
    - Given three villager harvest commands in a single tick, when `VegetationHarvestSystem` processes them, then exactly three `HarvestReceipt` entries exist with matching `commandId`, and rewinding before the tick removes both receipts and storehouse deltas.
    - Verify cooldown: subsequent commands inside `cooldownSeconds` return zero yield and record a cooldown rejection history entry.
  - Reproduction stability:
    - Given a species with `seedsPerEvent = 4` and `offspringCap = 12`, when reproduction runs for ten minutes at 60 Hz, total offspring per parent never exceeds cap and `VegetationGridData` occupancy stays below configured density.
    - Rewind halfway through and branch with different grid availability; offspring positions must remain deterministic within each branch (same seeds/tick produce identical placements).
  - Decay lifecycle:
    - Spawn vegetation, force health to zero, run decay system; entities despawn only after configured grace period and release grid reservations. Rewind before grace expiry and confirm reservations persist.
- **Observability**
  - Add a DOTS debugger overlay or Entities Hierarchy preset that surfaces chunk occupancy and memory for vegetation and villager archetypes, and confirm it survives rewinds.
  - Profiling automation: CI job runs the headless scenario, captures Unity Profiler data for `VegetationGrowthSystem`, `VegetationHealthSystem`, `VegetationHarvestSystem`, `VillagerJobAssignmentSystem`, `RewindCoordinatorSystem`, and `TimeTickSystem`, and fails the build when any metric regresses more than 10 percent week over week.

## Suggested Test Harness Scenarios
- `VegetationHealthCycleTest` – Table-driven test applying alternating environment events and asserting health/stress state snapshots every 30 ticks.
- `VegetationHarvestCommandTest` – Playmode test injecting harvest commands, verifying receipt buffers, cooldown enforcement, and rewind rollback.
- `VegetationReproductionDeterminismTest` – Headless playmode soak spawning dozens of plants, capturing offspring positions, rewinding, and comparing world hashes between branches.
- `VegetationDecayCleanupTest` – Ensures dead entities transition through decay, release grid slots, and history buffers purge future entries on rewind.
- All tests that create blob assets must dispose them after assertions (`blob.Dispose()`) to avoid leaking persistent allocations in the editor test runner.

## Next Steps
- Implement flag packing (`VegetationFlags`, `VillagerFlags`) and replace `FixedString64Bytes` resource identifiers with blob lookups before performance testing.
- Author and bake the `VegetationSpeciesCatalog` and `VegetationEnvironmentProfile` assets so runtime systems can swap hard-coded thresholds for data-driven values.
- Extend `Docs/SystemOrdering/SystemSchedule.md` with entries for the new vegetation systems once their order is locked.
- Coordinate with the testing agent to script the 100k entity headless scenario and expose it through `PureDotsTestMenu`, including drought/flood and reproduction stress variants.
