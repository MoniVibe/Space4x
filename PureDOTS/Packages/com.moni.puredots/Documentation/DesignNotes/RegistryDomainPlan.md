# Domain Registry Expansion Plan

Updated: 2025-11-02

## Purpose
Phase 2 of the roadmap calls for registries beyond resources/storehouses to deliver consistent, deterministic discovery APIs. This note captures the target state for villager, miracle, and broader logistics registries, outlining the data contracts, rebuild flow, continuity integration, and validation coverage required to finish the slice.

The plan assumes the shared scaffolding landed during the previous pass:
- `DeterministicRegistryBuilder<T>` for deterministic ordering
- `RegistryContinuitySnapshot` + `RegistrySpatialSyncState`
- `RegistryHealthSystem` continuity enforcement
- Registry directory helpers for cross-domain discovery

## Villager Registry

### Current Snapshot
- `VillagerRegistry`/`VillagerRegistryEntry` exist but the system only records position/job/availability and skips spatial metadata + continuity snapshots.
- No counters for idle vs. reserved vs. incapacitated villagers.
- Consumers (job assignment, AI) still perform ad-hoc queries for discipline/adventure flags.

### Target Contract
- Entry fields:
  - Entity, villager ID, faction ID
  - Position, `CellId`, `SpatialVersion`
  - Availability flags (Idle, Reserved, InCombat, Carrying)
  - Job info (type, phase, ticket)
  - Discipline archetype, morale tier, health summary (byte/short summary to avoid per-system lookups)
  - Optional logistics link (`AssignedWagon`, `EscortGroupId`)
- Registry component aggregates: total, idle, combat capable, incapacitated, average morale/health (cached to avoid repeated scans).
- Continuity: mark `RequiresSpatialSync` so divergence is treated as failure.

### Systems & Integration
1. Extend `VillagerRegistrySystem`:
   - Pull `SpatialGridResidency` and/or fallback hash to fill `CellId`/`SpatialVersion`.
   - Gather health/morale/discipline snapshots via component lookups.
   - Publish continuity snapshot using `RegistrySpatialSyncState`.
2. Update consumers (`VillagerJobSystems`, targeting) to prefer registry data over duplicate component lookups.
3. Instrumentation: expose counts to debug HUD + console instrumentation.
4. Rewind: confirm registry rebuild is deterministic in record mode and skipped in playback.

### Validation
- EditMode: entry struct ordering, availability flag translation.
- Playmode:
  - Registry rebuild with/without spatial grid (continuity warning path).
  - Job assignment uses entry metadata (no fallback queries).
  - Rewind round-trip (record -> mutate -> rewind -> catch-up -> record).

## Miracle Registry

### Scope
Track all miracle-capable entities (rain clouds, active miracle zones, cooldown anchors) so:
- The divine hand / command buffers can query available miracles efficiently.
- AI/presentation modules can subscribe to miracle lifecycle events without scanning the world.

### Data Layout
- Registry singleton: `MiracleRegistry` (total miracles, active miracles, total energy cost).
- Entry struct:
  - Entity, miracle ID/index, `MiracleType`
  - Position, `CellId`, `SpatialVersion`, effective radius/height
  - Energy/cooldown remaining, state flags (Ready, Charging, Active, Disabled)
  - Owning faction / player index
  - Optional payload: linked rain cloud, bound hand command, effect intensity

### Systems
1. ? Author miracle components (definition/runtime/target/caster) (`MiracleSource`, `MiracleCooldown`, etc.) with bakers supplying unique IDs.
2. Implement `MiracleRegistrySystem`:
   - Rebuild entries each record-frame; integrate with spatial continuity (require sync).
   - Keep aggregate metrics for quick HUD access.
3. Add instrumentation:
   - Registry console log & HUD overlay (miracle counts, cooldowns).
   - Health flags when miracles fail to report spatial data.

### Validation
- EditMode: baker produces deterministic IDs, registry ordering stable.
- Playmode:
  - Miracles spawning/despawning update registry.
  - Divine hand queries use registry data (integration smoke test).
  - Continuity failure triggers health warnings when spatial data missing.
  - Rewind test verifying miracle state restores (pair with `MiracleRewindTests` backlog).

## Logistics & Transport Registries

### Current Snapshot
- `TransportRegistrySystem` covers miner vessels, haulers, freighters, wagons with spatial data, but continuity snapshots default to requiring spatial sync even when grid absent.
- No registries yet for logistics requests, delivery queues, or route templates.

### Enhancements
1. **Transport registries**:
   - Consume `RegistrySpatialSyncState` (mirror resource/storehouse pattern) to honour continuity enforcement. ✅ wired in `TransportRegistrySystem`.
   - Expand entries with per-domain metadata (e.g., wagon assigned villager state, route priority). ✅ entries now surface assigned villager + job flags.
   - Add aggregate metrics (idle vs. busy counts, capacity utilisation). ✅ new registry fields report idle/assigned counts and capacity/load totals.
2. **Logistics request registry** (new):
   - Singleton summarising outstanding transport requests (source, destination, resource type, urgency). ✅ `LogisticsRequestRegistry` implemented with aggregate metrics (pending/in-progress/critical, capacity totals).
   - ? Entry fields: request entity, position(s), cell ids, required units, flags (urgent, blocking, player-pinned). ✅ `LogisticsRequestRegistryEntry` captures source/destination cell ids, priority, flags, units remaining.
   - System reads from request buffers (`LogisticsRequest` components) each record frame. ✅ `LogisticsRequestRegistrySystem` rebuilds registry with continuity snapshots.
3. **Route profile registry** (optional but planned):
   - Index deterministic route templates for reuse (source/destination cell sets, cost metrics).
   - Supports AI path planning without repeated graph builds.

### Validation
- Transport registries: playmode coverage ensuring continuity enforcement + aggregate metrics.
- Request/route registries: unit tests for deterministic ordering; playmode for spawn/despawn + rewind scenarios.

## Implementation Order
1. **Villager registry upgrade** (1–2 days)
   - Data enrichment + spatial continuity + tests.
2. **Transport registry continuity polish** (0.5 day)
   - Hook to spatial sync, add aggregates, tests.
3. **Miracle registry introduction** (2–3 days)
   - Components, rebuild system, instrumentation, tests.
4. **Logistics request registry** (2 days)
   - Data layout, system, consumers, tests.
5. **Route/profile registry (optional)** (2 days, can land later).

## Cross-Cutting Tasks
- Update `RegistryHealthSystem` thresholds so new registries inherit sensible defaults (expose per-domain config via authoring assets).
- Extend HUD + console output to include new registries.
- Document consumer patterns in `Docs/Guides/SceneSetup.md` and domain-specific guides.
- Add playmode bundle `RegistryContinuityPlaymodeTests` covering: villager, miracle, transport, request registries.

## References
- `Docs/TODO/RegistryRewrite_TODO.md` (update tasks per domain).
- `Docs/DesignNotes/RegistryContinuityContracts.md` (continuity requirements).
- `Docs/TODO/MiraclesFramework_TODO.md`, `Docs/TODO/VillagerSystems_TODO.md`, `Docs/TODO/SpatialServices_TODO.md` for integration expectations.
