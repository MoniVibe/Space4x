# Demo Scenarios

## Overview

Scenarios are JSON files that define deterministic demo setups. They are loaded by the ScenarioRunner and drive all demo behavior without scene-hardcoded logic.

## Scenario JSON Schema

### Basic Structure

```json
{
  "name": "scenario_name",
  "game": "Space4X",
  "seed": 12345,
  "duration_seconds": 120,
  "entities": [
    {
      "type": "Carrier",
      "id": "CARRIER-1",
      "position": [0, 0, 0],
      "components": { ... }
    }
  ],
  "actions": [
    {
      "tick": 100,
      "type": "spawn",
      "entity": "MINER-1"
    }
  ],
  "expectations": {
    "expectMiningYield": true,
    "expectCarrierPickup": true,
    "expectInterceptAttempts": true
  }
}
```

### Schema Fields

- `name` - Scenario identifier
- `game` - "Space4X" or "Godgame"
- `seed` - RNG seed for determinism
- `duration_seconds` - Scenario runtime
- `entities` - Initial entity definitions
- `actions` - Time-based actions (spawn, move, trigger)
- `expectations` - Validation assertions

## Known-Good Scenarios

### Space4X Scenarios

#### combat_duel_weapons.json

**Purpose**: Two carriers with mixed modules engage in combat

**Setup**:
- Carrier 1: Position (0, 0, 0), weapons: laser-s-1, missile-s-1
- Carrier 2: Position (100, 0, 0), weapons: laser-s-1, engine damage

**Flow**:
- T=0s: Carriers spawn
- T=10s: Carriers engage
- T=30s: First module destroyed
- T=60s: Combat resolution

**Expectations**:
- `expectDamageTotal > 0`
- `expectModulesDestroyed >= 1`
- `expectHits > 0`

**Location**: `Assets/Scenarios/space4x_demo_mining_combat.json` (similar)

#### mining_loop.json

**Purpose**: 4 miners, 2 haulers, 1 station demonstrate mining loop

**Setup**:
- Station: Position (0, 0, 0)
- Miners: 4 vessels, start docked
- Haulers: 2 vessels, start docked
- Asteroids: 10 scattered deposits

**Flow**:
- T=0s: Vessels dispatch
- T=20s: Mining begins
- T=40s: First pickup spawned
- T=60s: Carrier pickup occurs
- T=120s: Loop completes

**Expectations**:
- `expectMiningYield: true`
- `expectCarrierPickup: true`
- `expectThroughput > 0`

**Location**: `Assets/Scenarios/space4x_demo_mining.json`

#### compliance_demo.json

**Purpose**: Safe zone infraction → sanction path

**Setup**:
- Carrier: Position (0, 0, 0)
- Safe zone: Center (50, 0, 0), radius 30
- Hostile target: Position (60, 0, 0)

**Flow**:
- T=0s: Carrier spawns
- T=20s: Carrier enters safe zone
- T=30s: Carrier fires weapon (infraction)
- T=35s: Sanction applied
- T=50s: Reputation delta logged

**Expectations**:
- `expectSanctionsTriggered: true`
- `expectReputationDelta < 0`
- `expectSafeZoneViolations >= 1`

#### carrier_ops.json

**Purpose**: Damage → dock → repair/refit workflow

**Setup**:
- Carrier: Position (0, 0, 0), damaged modules
- Station: Position (200, 0, 0)

**Flow**:
- T=0s: Carrier spawns with damage
- T=30s: Carrier moves to station
- T=60s: Carrier docks
- T=80s: Field repair begins
- T=100s: Facility refit (laser → missile)
- T=120s: Operations complete

**Expectations**:
- `expectRefitCount: 1`
- `expectFieldRepairCount: 1`
- `expectModulesRestoredTo >= 0.95`

**Location**: `Assets/Scenarios/space4x_demo_refit.json` (similar)

### Godgame Scenarios

#### villager_loop_small.json

**Purpose**: 10 villagers, 1 storehouse, 2 nodes demonstrate villager loop

**Setup**:
- Villagers: 10 entities
- Storehouse: 1 entity
- Resource nodes: 2 entities

**Flow**:
- T=0s: Villagers spawn
- T=10s: Villagers navigate to nodes
- T=30s: Gathering begins
- T=60s: Delivery to storehouse
- T=90s: Loop completes

**Expectations**:
- `expectVillagersActive: 10`
- `expectItemsGathered > 0`
- `expectDeliveries > 0`

#### construction_ghost.json

**Purpose**: Construction ghost → build completion

**Setup**:
- Ghost: 1 construction ghost entity
- Storehouse: Tickets available (cost 100)

**Flow**:
- T=0s: Ghost spawns
- T=10s: Tickets withdraw from storehouse
- T=30s: Build progress advances
- T=60s: Build completes, effect emitted

**Expectations**:
- `expectBuildCompleted: true`
- `expectTicketsConsumed: 100`
- `expectEffectEmitted: true`

#### time_rewind_smoke.json

**Purpose**: Scripted input for rewind demo

**Setup**:
- Scripted actions at specific ticks
- Rewind checkpoints

**Flow**:
- T=0s: Record begins
- T=5s: Rewind to T=3s
- T=3s: Resim to T=5s
- T=5s: Verify state match

**Expectations**:
- `expectRewindDeterministic: true`
- `expectStateMatch: true`

## Scenario Runner Integration

### Loading Scenarios

```csharp
// Scenario bootstrap (DemoBootstrap) loads scenario
var scenarioPath = demoOptions.ScenarioPath;
var scenarioRunner = World.GetOrCreateSystemManaged<ScenarioRunner>();
scenarioRunner.LoadScenario(scenarioPath);
```

### CLI Execution

```bash
-executeMethod Demos.Build.Run --game=Space4X --scenario=combat_duel_weapons.json
```

### Batchmode Execution

```bash
Unity -batchmode -projectPath . -executeMethod PureDOTS.Runtime.Devtools.ScenarioRunnerEntryPoints.RunScenarioFromArgs --scenario Assets/Scenarios/space4x_demo_mining_combat.json --report Reports/mining_combat_demo_report.json
```

## Scenario Validation

### Preflight Checks

Before running scenario:
1. Validate JSON schema
2. Check entity references
3. Verify action timing
4. Validate expectations

### Runtime Validation

During execution:
1. Assert expectations at specified ticks
2. Log telemetry metrics
3. Capture state snapshots
4. Generate report

### Post-Run Validation

After completion:
1. Compare metrics to expectations
2. Verify determinism (if rewind used)
3. Generate artifact reports
4. Store in `Reports/<game>/<scenario>/<timestamp>.*`

## Scenario Development

### Creating New Scenarios

1. Define entities and initial state
2. Specify time-based actions
3. Set expectations for validation
4. Test determinism (30/60/120Hz)
5. Add to known-good list

### Testing Scenarios

```bash
# Dry-run determinism test
-executeMethod Demos.Preflight.Run --game=Space4X --scenario=combat_duel_weapons.json --test-determinism

# Full scenario run
-executeMethod Demos.Build.Run --game=Space4X --scenario=combat_duel_weapons.json
```

## References

- Existing scenario docs: `Docs/Scenarios/MiningCombatDemo.md`, `Docs/Scenarios/RefitDemo.md` (now consolidated here)
- Scenario JSONs: `Assets/Scenarios/*.json`
- ScenarioRunner: `PureDOTS.Runtime.Devtools.ScenarioRunner`
