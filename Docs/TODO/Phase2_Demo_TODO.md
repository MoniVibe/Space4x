# Phase 2 – Space4x Demo (spine-driven)

Goal: minimal mine → haul loop validated against PureDOTS spines; visuals remain swappable placeholders.

## Slice 1 – Mining
- [x] Miner ship reads scripted order; FixedStep state machine ticks “Mining” (MiningOrder/MiningState) and emits `PlayEffectRequest("FX.Mining.Sparks")` via effect stream.
- [ ] Uses Registry/Continuity spine for mining entity definitions; no hybrid/service locator usage.

## Slice 2 – Carrier
- [x] On threshold, emit `SpawnResource` entities; Carrier auto-picks via nearest-query system.
- [x] HUD/debug counter increments “ore in hold” via telemetry buffer (no UI hard refs).
- Notes: telemetry key `space4x.mining.oreInHold` is written from `Space4XMiningTelemetrySystem` (reads `Space4XMiningTelemetry` singleton fed by carrier pickups). Presentation binds to the TelemetryStream metric, not to gameplay entities.

## Slice 3 – Time Demo
- [ ] Rewind during transfer; resource counts/state replay identically.
- [ ] Time spine snapshot/command log drives resim; fixed-tick gate respects pause/rewind rules.
- In progress: miner FixedStep now skips when `RewindState.Mode` is not `Record`, and mining telemetry snapshot restore now reuses a single `Space4XMiningTelemetry` singleton (prevents double-entity/dirty snapshots during rewind/catch-up).

## Acceptance
- [ ] Presentation driven solely by Presentation spine bindings; removing bindings leaves the sim intact.
- [ ] PlayMode tests cover mining tick, carrier pickup, telemetry counter, and rewind determinism. (Mining tick covered by `Space4XMinerMiningSystemTests`)
- [ ] Registry continuity validation passes for mining/haul entities before PlayMode.

### Shared mining schema notes
- Miner authoring now seeds `MiningOrder`, `MiningState`, and `MiningYield` (resource id, tick interval, spawn threshold); mining ticks live in `Space4XMinerMiningSystem` (FixedStep).
- Effect requests queue in `Space4XEffectRequestStream` as `PlayEffectRequest("FX.Mining.Sparks")` attached to the miner entity.

### 2025-02-04 progress (Agent 2)
- MiningResourceSpawnSystem now maps `MiningYield.ResourceId` → `ResourceType` via `Space4XMiningResourceUtility`, keeps vessel cargo type in sync, and uses the yield spawn threshold (fallback 25% capacity) when emitting pickups.
- MiningVessel cargo type is synced from the active mining order’s resource id so spawned pickups match registry identifiers.
- Added coverage in `Space4XMinerMiningSystemTests` verifying yield-driven spawn thresholds/resource typing and pending/ready updates.
- `Space4XMinerMiningSystem` writes `MiningCommandLogEntry` with resolved resource type on each gather tick to keep the time spine aligned.
- `Space4XMiningYieldSpawnBridgeSystem` turns `MiningYield.PendingAmount/SpawnReady` into `SpawnResourceRequest` entries (using spine resource ids mapped to `ResourceType`) so `MiningResourceSpawnSystem` can emit pickups for carriers.

### 2025-02-05 progress (Agent 3)
- Mining FixedStep now gates on `RewindState.Mode == Record` to avoid ticking miners during playback/catch-up runs.
- Telemetry rewind replay now restores into a single `Space4XMiningTelemetry` singleton to prevent duplicate metrics/entities.
- Added miner tests for the rewind guard and reused helpers to reduce duplicate setup in `Space4XMinerMiningSystemTests`.
