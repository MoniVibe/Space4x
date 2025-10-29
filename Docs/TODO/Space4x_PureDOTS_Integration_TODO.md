# Space4X ↔ PureDOTS Integration TODO

Checklist for bringing the Space4X gameplay layer online with the shared `com.moni.puredots` package. Update as progress is made.

## Next Agent Prompt

- Focus: Expand the bridge beyond prototype entities and wire real gameplay data.
- Starting point: extend the registry bridge to cover logistics routes and anomaly/threat data, leveraging the new colony/fleet components as a template.
- Deliverables: additional DOTS components for logistics/anomalies, bakers for their authoring assets, metrics derived from those registries, and updated PlayMode/DOTS tests covering multi-domain sync.
- Constraints: keep gameplay-specific types under `Space4x/Assets/Scripts/Space4x`; surface any engine-level needs back to PureDOTS docs/TODOs.

## Registry Alignment

- [ ] Catalogue Space4X domain actors (colonists, fleets, stations, trade hubs, miracles/abilities, shipyards, anomalies) and map them to the PureDOTS registry contracts.
- [ ] Introduce DOTS data for any missing Space4X concepts needed by the registries (fleet posture, logistics routes, anomaly states) while reusing shared schemas wherever possible.
- [x] Implement `Space4XRegistryBridgeSystem` to register Space4X buffers/singletons with the shared registries and react to registry callbacks.
- [x] Build authoring/baker adapters that translate Space4X SubScenes/prefabs into registry entries at conversion time.

## Spatial & Continuity Services

- [ ] Configure the spatial grid provider/profile for orbital/sector scales and link Space4X systems to the PureDOTS spatial services.
- [ ] Align time/continuity systems (time state, rewind, continuity validation) with PureDOTS expectations, ensuring deterministic fleet/colony updates.
- [ ] Confirm Burst compatibility for bridge systems once spatial and continuity hookups are live.

## Telemetry & Metrics

- [x] Emit telemetry events for core loops (fleet engagements, colony growth, resource logistics, miracle activations) into PureDOTS instrumentation buffers for the shared debug HUD. *(Colonies & fleets initial coverage complete; extend to logistics/miracles next.)*
- [x] Surface metrics counters (population levels, logistics throughput, threat alerts, miracle cooldowns) so both projects benefit from the neutral dashboards. *(Initial colony/fleet metrics live; broaden coverage in follow-up tasks.)*

## Scenes, Prefabs & Assets

- [ ] Audit scenes/prefabs and add bakers/MonoBehaviours that supply Space4X-specific data to PureDOTS registries during conversion.
- [ ] Remove any lingering service locator usage in gameplay scripts in favour of registry lookups.
- [ ] Update ScriptableObject catalogs (fleet archetypes, resource definitions, anomaly types) to align with the shared registry IDs.

## Testing & Validation

- [x] Build PlayMode/DOTS integration tests verifying registry registration, spatial sync, and telemetry emission for Space4X flows.
- [ ] Add focused tests for key scenarios (fleet spawning, trade route updates, miracle execution, colony supply) to prove shared registry consumption works.
- [ ] Provide mock registry utilities for isolated Space4X tests.

## Documentation & Follow-Up

- [ ] Document Space4X adapters and required authoring assets in `Docs/Guides/Space4X` and cross-reference PureDOTS truth sources.
- [ ] Feed engine-level gaps back into `PureDOTS_TODO.md` or TruthSources when Space4X work uncovers them.
- [ ] Track open questions/risks in this file to guide future agent prompts.

