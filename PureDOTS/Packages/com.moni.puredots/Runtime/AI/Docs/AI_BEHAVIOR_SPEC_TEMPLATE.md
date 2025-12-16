# AI Behavior Specification Template

> This specification format captures a single AI behavior across the Body / Mind / Aggregate pillars that live on the same Entities runtime (see `TRI_PROJECT_BRIEFING.md`). Copy this file when defining a new behavior and replace every placeholder.

## 1. Identity & Scope
- **Behavior ID**: `ai.<domain>.<behavior>`
- **Domain / Feature Group**: _e.g., mining, logistics, diplomacy_
- **Owning Teams**: _systems, design, QA leads_
- **Summary**: _one paragraph – why this behavior exists and player impact_
- **Primary Entities**: _carriers, villagers, anomalies, etc._
- **Supported Modes**: _headless / presentation / both_
- **Dependencies**: _catalog blobs, registries, scenario hooks, AgentSyncBus channels, etc._

## 2. Pillar Cadence Contract
Use integer tick cadences (no Hz). The shared helper `CadenceGate.ShouldRun(currentTick, cadenceTicks)` enforces the gate.

| Pillar    | System Group(s)                             | Cadence (ticks) | Gate Condition                                   |
|-----------|---------------------------------------------|-----------------|--------------------------------------------------|
| Body      | _e.g., `GameBodySystemGroup`_               | `BodyCadence`   | `CadenceGate.ShouldRun(timeState.Tick, BodyCadence)`
| Mind      | _e.g., `GameMindSystemGroup`_               | `MindCadence`   | `CadenceGate.ShouldRun(timeState.Tick, MindCadence)`
| Aggregate | _e.g., `GameAggregateSystemGroup`_          | `AggregateCadence` | `CadenceGate.ShouldRun(timeState.Tick, AggregateCadence)`

_Notes_: Body defaults to `1`. Document if Mind / Aggregate skip ticks intentionally.

## 3. Body Pillar (Execution)
- **Systems**: _list each Burst `ISystem` + order (UpdateInGroup/After/Before)_
- **Queries**: _components/buffers per system; include spatial/index constraints_
- **States**: _task phases, limb channels, focus usage, locomotion requirements_
- **Inputs**: _components/buffers consumed from other pillars (e.g., `VesselAIState`, `FocusBudget`)_
- **Outputs**: _components/buffers/tags written (e.g., `MovementCommand`, `ResourceStorage`)_
- **Invariants**: _capacity never goes negative, focus clamped, etc._
- **Failure Handling**: _how body pillar reacts to missing targets, rewinds, depletion_

## 4. Mind Pillar (Goals / Intent)
- **Cadence**: `MindCadence` ticks via cadence gate.
- **Systems**: _goal evaluators, planners, AgentSyncBus writers_
- **Goal Graph**: _states + transitions (Idle → Seek → Execute → Recover)_
- **Focus & Cognitive Resources**: _how focus, morale, and stats feed planning_
- **Interfaces**: _components published to Body (e.g., `VesselAIState`), signals consumed from Aggregate_
- **Edge Cases**: _dead targets, conflicting intents, multi-entity coordination_

## 5. Aggregate Pillar (Rollups / Coordination)
- **Cadence**: `AggregateCadence` ticks via cadence gate.
- **Systems**: _registry sync, morale / economics aggregation, cross-entity coordination_
- **Aggregated Scopes**: _bands, fleets, villages, empires_
- **Telemetry**: _buffers / components (e.g., `MiningTelemetry`, `AggregateMoraleState`)_
- **Policies & Thresholds**: _when aggregates retarget, spawn tasks, or broadcast alerts_

## 6. Data & Component Contracts
List every component/buffer touched across pillars.

| Component / Buffer | Pillar | Access | Description |
|--------------------|--------|--------|-------------|
| _e.g., `VesselAIState`_ | Mind → Body | RW | _tracks goal, target entity, timers_ |

Additional details:
- **Catalog / Registry IDs**: _resource types, storehouse IDs, module catalogs_
- **Blob Assets**: _schema + owning singleton_
- **Tags / Filters**: _`GameWorldTag`, `RuntimeMode`, `SpatialQuery` requirements_
- **Authoring Hooks**: _bakers, authoring components, scenario JSON sections_

## 7. Cadence Gate Usage Examples
```
var timeState = SystemAPI.GetSingleton<TimeState>();
if (!CadenceGate.ShouldRun(timeState.Tick, MindCadenceTicks))
{
    return; // skip this pillar tick deterministically
}
```
_Optional_: use `CadenceGate.ShouldRun(ref SystemState state, int cadenceTicks)` which fetches `TimeState` internally when available.

## 8. Headless & Testing Checklist (Non-Optional)
- **Golden Scenario ID(s)**: `_Assets/Scenarios/..._` + CLI command snippet.
- **Stress Scenario ID(s)**: `_scale_* variants or soak cases._
- **Expected Report Fields**: _list counters/metrics to assert in JSON reports (e.g., `oreDelivered`, `storehouseFillRate`)._
- **Telemetry Hooks**: _buffers / counters that must update when scenario runs._
- **Unit / Integration Tests**: _NUnit fixtures + key asserts._
- **Invariants**: _ordered list of invariants to validate (no negative cargo, tasks complete within X ticks, etc.)._

## 9. Presentation / Hand-off Notes
- **Data Surface**: _components/buffers to visualize later (positions, statuses, debug counters)._ 
- **TODO Tags**: _any `PresentationTodoTag`, `DebugTelemetryTag`, etc._
- **Dependencies**: _which presentation agents must mirror this behavior._

## 10. Implementation Checklist
1. _Systems created / updated_
2. _Components/buffers/bakers implemented_
3. _Cadence gate integrated for each pillar_
4. _Scenarios + reports authored_
5. _Docs updated (`HEADLESS_PROGRESS.md`, behavior spec clones)_
6. _Tests passing headless_

---

When cloning this template:
- Keep terminology consistent with PureDOTS + TRI docs.
- Embed links to scenario JSONs, catalog entries, and tests for fast traceability.
- Update whenever behavior code changes to keep docs and determinism in sync.
