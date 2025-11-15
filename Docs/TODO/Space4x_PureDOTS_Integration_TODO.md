# Space4X ↔ PureDOTS Integration TODO

Checklist for bringing the Space4X gameplay layer online with the shared `com.moni.puredots` package. Update as progress is made.

## Next Agent Prompt

- Focus: broaden registry metrics (colony supply, logistics strain), improve ability UX hooks, and validate production scenes against the new spatial/miracle requirements.
- Starting point: use the spatial wiring guide under `Docs/Guides/Space4X` to audit active scenes; extend registries with supply telemetry and polish miracle presentation flows.
- Deliverables: colony supply metrics surfaced to telemetry, UX plan for ability HUD/tooltips, validation report for production scenes referencing the new bootstrap/tagging checklist.
- Constraints: keep gameplay-specific types under `Space4x/Assets/Scripts/Space4x`; surface engine-level gaps back to PureDOTS TODOs.

## Registry Alignment

- [ ] Catalogue Space4X domain actors (colonists, fleets, stations, trade hubs, miracles/abilities, shipyards, anomalies) and map them to the PureDOTS registry contracts.
- [x] Introduce DOTS data for any missing Space4X concepts needed by the registries (fleet posture, logistics routes, anomaly states) while reusing shared schemas wherever possible.
- [x] Implement `Space4XRegistryBridgeSystem` to register Space4X buffers/singletons with the shared registries and react to registry callbacks.
- [x] Build authoring/baker adapters that translate Space4X SubScenes/prefabs into registry entries at conversion time.

## Spatial & Continuity Services

- [x] Configure the spatial grid provider/profile for orbital/sector scales and link Space4X systems to the PureDOTS spatial services. *(All production scenes/subscenes now reference `Assets/Space4X/Config/DefaultSpatialPartitionProfile.asset`; see audit notes.)*
- [x] Ensure every gameplay baker authoring Space4X entities adds `SpatialIndexedTag` (or equivalent) so residency lookups stay deterministic.
- [x] Author a scene bootstrap config that seeds the correct `SpatialGridConfig`/`SpatialGridState` for sector-scale play and document how designers override it per scene. *(Scenes now point at `Assets/Space4X/Config/PureDotsRuntimeConfig.asset`, and the guide calls out the asset.)*
- [ ] Align time/continuity systems (time state, rewind, continuity validation) with PureDOTS expectations, ensuring deterministic fleet/colony updates.
- [ ] Confirm Burst compatibility for bridge systems once spatial and continuity hookups are live.

## Telemetry & Metrics

- [x] Emit telemetry events for core loops (fleet engagements, colony growth, resource logistics, miracle activations) into PureDOTS instrumentation buffers for the shared debug HUD. *(Colonies, fleets, logistics, anomaly, and miracle coverage complete.)*
- [x] Surface metrics counters (population levels, logistics throughput, threat alerts, miracle cooldowns) so both projects benefit from the neutral dashboards. *(Miracle energy/cooldown telemetry shipped; continue broadening counters as registries expand.)*
- [ ] Add colony supply/bottleneck metrics to the registry snapshot and telemetry stream.
- [ ] Capture ability UX telemetry hooks (casting latency, cancellation) once the HUD layer is ready.

## Scenes, Prefabs & Assets

- [ ] Audit scenes/prefabs and add bakers/MonoBehaviours that supply Space4X-specific data to PureDOTS registries during conversion. *(Use the Spatial & Miracle Integration guide for the checklist.)*
- [ ] Remove any lingering service locator usage in gameplay scripts in favour of registry lookups.
- [ ] Update ScriptableObject catalogs (fleet archetypes, resource definitions, anomaly types) to align with the shared registry IDs.
- [ ] Validate production scenes against the bootstrap/tagging checklist and document any gaps.

## Testing & Validation

- [x] Build PlayMode/DOTS integration tests verifying registry registration, spatial sync, and telemetry emission for Space4X flows.
- [ ] Add focused tests for key scenarios (fleet spawning, trade route updates, miracle execution, colony supply) to prove shared registry consumption works. *(Logistics/anomaly validation landed; miracle/colony supply still pending.)*
- [ ] Provide mock registry utilities for isolated Space4X tests.

## Documentation & Follow-Up

- [x] Document Space4X adapters and required authoring assets in `Docs/Guides/Space4X` and cross-reference PureDOTS truth sources. *(See `SpatialAndMiracleIntegration.md` for the latest checklist.)*
- [ ] Feed engine-level gaps back into `PureDOTS_TODO.md` or TruthSources when Space4X work uncovers them.
- [ ] Track open questions/risks in this file to guide future agent prompts.

## Validation Commands

- Edit-mode telemetry suite: `Unity -batchmode -projectPath . -runTests -testResults Logs/EditModeResults.xml -testPlatform editmode`
- Inspect `Logs/EditModeResults.xml` after the run; confirm `Space4XRegistryBridgeSystemTests` passes with spatial continuity counters populated.
