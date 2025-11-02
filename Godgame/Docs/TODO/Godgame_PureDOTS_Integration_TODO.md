# Godgame ↔ PureDOTS Integration TODO

Tracking the work required for Godgame gameplay to consume the shared `com.moni.puredots` package. Keep this file updated as bridges land.

## Next Agent Prompt

- Focus: Deliver the initial Godgame bridge into the shared registries.
- Starting point: expand `GodgameRegistryBridgeSystem` to register villager/storehouse buffers with the neutral registries and emit at least one telemetry sample to the PureDOTS HUD.
- Deliverables: minimal DOTS components representing villagers or storehouses, baker/authoring glue for a sample prefab/SubScene, and a PlayMode/DOTS test verifying the registry tells.
- Constraints: keep gameplay-specific code under `Godgame/Assets/Scripts/Godgame`; escalate only genuine engine-level gaps to PureDOTS and document them here.

## Registry Alignment

- [ ] Inventory existing Godgame entities (villagers, bands, miracles, storehouses, logistics assets, spawners) and map each to the corresponding PureDOTS registry contract.
  - Villager/storehouse data now flow through `GodgameVillagerSyncSystem` and `GodgameStorehouseSyncSystem`, feeding live state into the bridge; remaining domains (bands, miracles, logistics, spawners) still pending.
- [ ] Author DOTS components/buffers for Godgame domain data that are missing but required by the registries (e.g., villager intent, miracle cooldowns) without duplicating the shared schemas.
- [ ] Flesh out `GodgameRegistryBridgeSystem` so it registers domain singletons/buffers with the shared registries and subscribes to registry events.
- [ ] Provide authoring/baker adapters that populate registry entries from Godgame prefabs/SubScenes.
  - Added standalone `VillagerAuthoring` and `StorehouseAuthoring` MonoBehaviours that bake registry components for individual scene objects; still need to wire them into production prefabs/SubScenes.

## Spatial & Continuity Services

- [ ] Connect Godgame spatial grid usage to the PureDOTS spatial service (cell config, provider selection, rebuild cadence).
- [ ] Ensure continuity/rewind components from PureDOTS are hooked into Godgame determinism flows (time state, rewind state, continuity buffers).
- [ ] Validate Burst compilation for the bridge systems after spatial bindings are in place (fix any hybrid fallbacks).

## Telemetry & Metrics

- [ ] Emit telemetry events (villager lifecycle, storehouse inventory, band morale, miracle usage) via the PureDOTS instrumentation buffers so the shared debug HUD reflects Godgame data.
- [ ] Wire metrics counters into the bridge so per-domain stats (population, resource throughput, pending miracles) flow into the neutral dashboards.

## Scenes, Prefabs & Assets

- [ ] Review existing scenes/prefabs and add the necessary MonoBehaviour or baker adapters that translate Godgame authoring assets into PureDOTS-friendly data.
- [ ] Replace legacy service locators in gameplay scripts with registry lookups via the PureDOTS APIs.
- [ ] Update any ScriptableObjects catalogues so they now reference the shared registries instead of local enums or IDs.

## Testing & Validation

- [ ] Stand up PlayMode/DOTS integration tests under `Godgame.Gameplay` that exercise registry registration, data sync, and telemetry emission.
  - `GodgameRegistryBridgeSystemTests` now drives the villager/storehouse sync systems, verifies continuity metadata (including miracle registry baseline), and asserts telemetry keys remain Burst-friendly.
- [ ] Add validation tests for common flows (villager spawning, band assignment, storehouse transactions, miracle dispatch) proving they interact with the shared registries.
- [ ] Create test utilities/mocks to simulate PureDOTS registries when running focused Godgame tests.

## Foundational Gameplay Systems

- [ ] Stand up the Divine Hand right-click pipeline per TruthSource (`Hand_StateMachine.md`, `RMBtruthsource.md`, `Slingshot_Contract.md`). Deliverables: DOTS-friendly `RightClickRouterSystem` + `DivineHandStateSystem`, handler components for pile siphon/storehouse dump/slingshot aim under `Assets/Scripts/Godgame/Interaction`, HUD events (`OnHandTypeChanged`, `OnHandAmountChanged`, `OnStateChanged`), and PlayMode tests covering priority resolution and frame-rate independence (30 vs 120 FPS).
  - [ ] Implement the `PlayerInput` bridge that writes `InputState` and finish `CameraOrbitSystem` grounding.
  - [ ] Build right-click affordance detectors (storehouse intake, pile surface, valid ground) reusable by router/hand systems.
  - [ ] Create `HandCarrySystem` PD follow + villager interrupt flow and associated jitter/GC tests.
  - [ ] Author slingshot impulse calculation, cooldown handling, and a throw test fixture.
  - [ ] Wire HUD + cursor hint listeners for `HandStateChanged`/`HandCarryingChanged`.
  - [ ] Add `HandTelemetrySystem` metrics for siphon/dump/throw.
- [ ] Implement aggregate resource piles and storehouse inventory loop (`Aggregate_Resources.md`, `Storehouse_API.md`). Deliverables: ECS components + baker authoring for `AggregatePile` and `GodgameStorehouse`, pooled prefab with size curve visual updates, Storehouse API surface (`Add/Remove/Space`), telemetry hooks for registry sync, and regression tests for overflow/merge/capacity scenarios.
  - [ ] Runtime `AggregatePileSystem` (merge/split, pooling, hit metadata) + authoring for pile prefabs.
  - [ ] Storehouse intake authoring (intake collider, capacities) and DOTS totals/events system.
  - [ ] Conservation PlayMode tests (pile→hand→storehouse, spillover when full).
  - [ ] Telemetry + registry sync wiring for storehouse totals.
- [ ] Establish villager job/state graph aligned with `VillagerTruth.md`, `Villagers_Jobs.md`, `VillagerState.md`. Deliverables: job assignment buffers, state machine system (`Idle/Navigate/Gather/Carry/Deliver/Interrupted`), storehouse handoff via API events, interrupt/resume rules, and integration tests proving Gather→Deliver→Idle with storehouse totals reconciled.
  - [ ] Job scheduler / assignment buffer with GOAP hooks described in TruthSources.
  - [ ] State machine system with guards (`HasPath`, `HasCapacity`, `HasResource`) and events.
  - [ ] Interrupt handling tests (hand pickup, path blocked) and storehouse reconciliation.
- [ ] Bring up the rewindable time engine stack (`TimeTruth.md`, `TimeEngine_Contract.md`, `Timeline_DataModel.md`, `Input_TimeControls.md`). Deliverables: `TimeEngine` singleton with snapshot/command log, rewind GC policy, input bindings routed through Interaction, TimeHUD feedback (tick, speed, branch id), and EditMode tests for pause/rewind/step-back determinism and memory budget guards.
  - [ ] Command stream + snapshot storage implementation with GC tests.
  - [ ] Input routing for time controls, including UI priority overrides.
  - [ ] TimeHUD binding (tick, speed, branch).
  - [ ] Determinism tests for pause, rewind hold, step back, speed multipliers.

## Documentation & Follow-Up

- [ ] Document adapter surfaces and required authoring assets in `Docs/Guides/Godgame` (create folder as needed) and cross-link to PureDOTS truth sources.
- [ ] Update `PureDOTS_TODO.md` and relevant TruthSources when Godgame-specific needs reveal engine-level gaps.
- [ ] Capture open questions or blockers in this file to steer future agent prompts.

### Session Notes – current agent sweep

- PureDOTS gameplay code lives under `Packages/com.moni.puredots/Runtime/` with domain folders (`Runtime`, `Systems`, `Authoring`). Registries, telemetry, and time/rewind singletons are all defined there; consumer projects should reference those assemblies rather than duplicating structs.
- Core bootstrap (`PureDOTS.Systems.CoreSingletonBootstrapSystem`) seeds `TimeState`, `RewindState`, registry entities, telemetry, and registry health instrumentation. Godgame bridge systems must assume these singletons already exist (or call `EnsureSingletons` in tests) before attempting registry writes.
- Registry contracts of interest:
  - `VillagerRegistryEntry` expects availability flags, discipline, AI state, health/morale/energy bytes, and spatial continuity data (`CellId`, `SpatialVersion`).
  - `StorehouseRegistryEntry` supports per-resource capacity summaries via `FixedList32Bytes<StorehouseRegistryCapacitySummary>` and requires continuity metadata when spatial queries are enabled.
  - `RegistryMetadata.MarkUpdated` records entry counts, tick, and optional `RegistryContinuitySnapshot`; pass `RegistryContinuitySnapshot.WithoutSpatialData()` if Godgame lacks a spatial grid in early slices.
- Reuse `DeterministicRegistryBuilder<T>` when mirroring Godgame entities into shared buffers so ordering, metadata updates, and accumulator hooks stay deterministic with PureDOTS expectations.
- Telemetry is published via the `TelemetryStream` singleton buffer (`TelemetryMetric` elements). Godgame telemetry systems should batch metrics per frame and rely on PureDOTS HUD to visualise them.
- Open questions: when Godgame introduces spatial cells, align bridge with `RegistrySpatialSyncState` so registries can advertise real `CellId`/version data; confirm whether Godgame needs additional registry kinds (miracle/band/logistics) surfaced through PureDOTS packages.
- Bridge now watches `SpatialGridResidency`/`RegistrySpatialSyncState` and will mark continuity with real cell ids once Godgame entities receive spatial data; until then it falls back to non-spatial snapshots.
- Sample baker now applies `SpatialIndexedTag` to baked villagers/storehouses; new `GodgameSpatialIndexingSystem` covers runtime-spawned entities so they participate in PureDOTS spatial rebuilds without manual tagging.
- `GodgameRegistryBridgeSystemTests` seeds PureDOTS bootstrap singletons via `CoreSingletonBootstrapSystem.EnsureSingletons`, drives the spatial dirty/build systems, and asserts registry entries expose non-negative `CellId`/`SpatialVersion`.
- Follow-up: `SpatialGridResidency.Version` still lags the grid `SpatialGridState.Version` after initial rebuild, so bridge entries currently report via the fallback path. Investigate whether PureDOTS spatial systems should bump residency versions post-build or if Godgame needs a lightweight sync system.
- New runtime sync systems (`GodgameVillagerSyncSystem`, `GodgameStorehouseSyncSystem`) keep mirror components Burst-safe and free of allocations; they sanitize resource summaries and leverage the persistent resource catalog blob for index lookups.
- Registry bridge telemetry now caches metric keys and runs entirely via `TelemetryMetric` value writes, preserving Miracle registry consumers from managed allocations; tests lock this in by checking the metadata continuity snapshot.
- Godgame hand components now live directly under the `Godgame.Interaction` namespace (rather than `Godgame.Interaction.Hand`) to match Entities codegen expectations, and the gameplay asmdef references `Unity.Burst` so Burst attributes resolve during compile.
