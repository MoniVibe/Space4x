# AI Behavior Spec – `ai.mining.gather_to_storehouse`

## 1. Identity & Scope
- **Domain / Feature**: Space4X mining + logistics loop.
- **Summary**: Mining vessels extract resources from asteroids, haul them to carriers, and carriers off-load to the designated storehouse entity so registries stay authoritative.
- **Primary Entities**: `MiningVessel`, `Carrier`, `Asteroid`, `StorehouseConfig` entities, `SpawnResource` pickups.
- **Supported Modes**: Headless + presentation. Headless is the reference lane for deterministic validation via `Space4XScenarioEntryPoint`.
- **Dependencies**:
  - PureDOTS time/rewind (`TimeState`, `RewindState`).
  - Resource registry (`ResourceRegistry`, `ResourceRegistryEntry`).
  - Storehouse registry (`StorehouseConfig`, `StorehouseInventory`, `StorehouseCapacityElement`, `StorehouseInventoryItem`).
  - `Space4XMiningTelemetry` singleton for aggregate counters.

## 2. Pillar Cadence Contract
| Pillar    | System Groups                                           | Cadence (ticks) | Gate Condition |
|-----------|---------------------------------------------------------|-----------------|----------------|
| Body      | `PureDOTS.Systems.ResourceSystemGroup` (movement, gather, deposit) | `1`             | Always run     |
| Mind      | `Space4XTransportAISystemGroup` (targeting, intent updates)       | `6`             | `CadenceGate.ShouldRun(time.Tick, 6)` |
| Aggregate | `Space4XRegistrySystemGroup` (telemetry, registry sync)          | `30`            | `CadenceGate.ShouldRun(time.Tick, 30)` |

Body stays at cadence 1 to keep physics/timing deterministic. Mind cadences at every 6 ticks (~planning every 6 fixed steps) to amortize cost. Aggregate surfaces telemetry every 30 ticks to smooth noise.

## 3. Body Pillar (Execution)
- **Systems**:
  - `VesselMovementSystem` – steers vessels to mining targets / carriers.
  - `VesselGatheringSystem` – drains `ResourceSourceState` or `ResourceDeposit` into vessel cargo when within 3 units.
  - `VesselDepositSystem` – unloads cargo into carrier `ResourceStorage` buffers when returning.
  - `CarrierPickupSystem` – sweeps spawned pickup entities and loads carrier storage.
  - _New_: `CarrierStorehouseDepositSystem` (Space4X) – drains carrier storage into assigned storehouse inventory near drop-off point.
- **Queries**: All Body systems filter on `MiningVessel`, `Carrier`, `LocalTransform`, `ResourceStorage`, and `GameWorldTag` to ensure only active sim entities participate.
- **States**: `VesselAIState.State` (Idle, MovingToTarget, Mining, Returning). `MiningVessel.CurrentCargo` + `CargoResourceType` encode body progress. Carrier-level `MovementCommand` optionally guides drop-off running.
- **Inputs**: `VesselAIState` (Mind), `MiningOrder`, `MinerTargetStrategy`, `StorehouseDropoffAssignment` (new component from Mind pillar tagging carriers with a storehouse).
- **Outputs**: `SpawnResource` pickups, `ResourceStorage` buffer mutations, `StorehouseInventoryItem` buffer deltas, `MiningCommandLogEntry` history writes.
- **Invariants**:
  1. `CurrentCargo` ∈ [0, CargoCapacity].
  2. Storehouse `TotalStored` never exceeds `TotalCapacity`; overflow spawns pickup (TODO once storehouse full).
  3. When resource depleted, vessel sets `VesselAIState.TargetEntity = Entity.Null` in <= 1 tick.
- **Failure Handling**: If target destroyed or missing transforms, Body pillar resets to Idle and Mind re-queues next target in next Mind cadence.

## 4. Mind Pillar (Goals / Intent)
- **Cadence**: `MindCadence = 6` using `CadenceGate.ShouldRun(time.Tick, 6)` at the top of `VesselAISystem` + `VesselTargetingSystem`.
- **Systems**:
  - `VesselAISystem` – scans `ResourceRegistryEntry` buffer, assigns `MiningOrder.TargetEntity`, selects nearest carrier/storehouse pair.
  - `VesselTargetingSystem` – resolves `TargetPosition` from `TargetEntity` (asteroid or carrier) and clears when invalid.
  - `Space4XResourceRegistryPopulationSystem` – Mind gate ensures registry snapshots update before AISystem runs (cadence multiples aligned to avoid stale data).
  - _New_: `StorehouseAssignmentSystem` – maps faction/carrier to best `StorehouseConfig` (closest open capacity) and writes `StorehouseDropoffAssignment` + `MindStorehouseIntent` components.
- **Goal Graph**: Idle → AcquireTarget (Mind) → Execute Mining (Body) → Return (Body) → Deposit to Carrier → Carrier Deposit to Storehouse → Idle.
- **Focus**: Each vessel consumes `FocusBudget` units when acquiring a target; gating prevents over-subscription.
- **Edge Cases**: Mind pillar must resolve collisions when multiple vessels choose same resource. `MiningOrder.Reservation` buffer slots guard against > `MaxSimultaneousWorkers`.

## 5. Aggregate Pillar (Rollups)
- **Cadence**: `AggregateCadence = 30` using `CadenceGate.ShouldRun(time.Tick, 30)`.
- **Systems**:
  - `Space4XMiningTelemetrySystem` – sums `Space4XMiningTelemetry.OreInHold` + storehouse fill % for reports.
  - `Space4XRegistryBridgeSystem` – records deliveries into `RegistryDirectory` snapshots.
  - _New_: `StorehouseLogisticsTelemetrySystem` – aggregates `StorehouseInventoryItem` deltas per resource type to feed compliance/needs systems.
- **Telemetry**: `Space4XMiningTelemetry`, `StorehouseHistorySample`, `RegistryDeliveryLog`.
- **Policies**: If aggregate sees throughput < quota for 3 aggregates runs, emits `LogisticsShortfallSignal` consumed by Mind pillar for retargeting.

## 6. Data & Component Contracts
| Component / Buffer | Pillar | Access | Description |
|--------------------|--------|--------|-------------|
| `VesselAIState` | Mind → Body | RW | Current behavior state, targets, timers.
| `MiningOrder` | Mind | RW | Reservation against registry nodes.
| `MiningVessel` | Body | RO | Mining stats per vessel.
| `ResourceStorage` | Body → Aggregate | RW | Carrier hold inventory.
| `StorehouseCapacityElement` | Body/Aggregate | RO | Max storage per resource.
| `StorehouseInventoryItem` | Body/Aggregate | RW | Current stored and reserved units.
| `StorehouseDropoffAssignment` (new) | Mind/Body | RO | Carrier → storehouse mapping for delivery leg.
| `StorehouseDeliveryLog` buffer (new) | Aggregate | Append | Telemetry record per deposit (tick, resourceType, amount).

Authoring hooks:
- `Space4XCarrierAuthoring` adds `ResourceStorage` + default cargo capacities.
- Sample storehouse seeded via `Space4XSampleRegistryBootstrapSystem`.
- Scenario JSON `carrier` spawns may declare `storehouseId` to pre-bind assignments.

## 7. Cadence Gate Usage
```
[BurstCompile]
public void OnUpdate(ref SystemState state)
{
    if (!CadenceGate.ShouldRun(ref state, MindCadenceTicks))
    {
        return;
    }
    // Mind logic here
}
```
Body systems keep cadence = 1 and skip the call.

## 8. Headless & Testing Checklist
- **Golden Scenario**: `Assets/Scenarios/space4x_demo_mining.json`
  - CLI: `Space4X.x86_64 --batchmode --scenario Assets/Scenarios/space4x_demo_mining.json --report reports/space4x_mining_golden.json`
  - Expected report fields: `oreMined`, `oreDelivered`, `storehouseFillPercent`, `vesselIdleTicks`.
- **Stress Scenario**: `Assets/Scenarios/scenario_space_demo_01_scale_100k.json`
  - Validates cadence gates hold under 100k vessels + multi storehouses.
- **Unit Tests**: extend `Space4XMinerMiningSystemTests`, `Space4XMiningMovementIntegrationTests` to assert `StorehouseInventoryItem` increments and `ResourceStorage` drains.
- **Invariants**:
  1. Golden scenario deposit count matches number of full cargo cycles recorded in `MiningCommandLog`.
  2. Stress scenario storehouse never exceeds `MaxCapacity` and never drops below 0.
  3. Telemetry `StorehouseHistorySample` records at least one entry per Aggregate cadence while throughput > 0.

## 9. Presentation / Hand-off Notes
- Expose `StorehouseDropoffAssignment` + `StorehouseDeliveryLog` for later visualization (UI cues showing carriers docking / delivering).
- Add `StorehouseDeliveryTodoTag` near deposit zone so presentation lane can spawn FX.

## 10. Implementation Checklist
1. Add `StorehouseDropoffAssignment` + `StorehouseDeliveryLog` components and bakers.
2. Implement shared cadence gating via `CadenceGate` in Mind/Aggregate systems.
3. Author `CarrierStorehouseDepositSystem` + telemetry aggregator.
4. Update mining scenarios with storehouse drop-off goals + new report metrics.
5. Extend NUnit + scenario-based tests, update `HEADLESS_PROGRESS.md` with behavior status.
