# Demo_02 Combat Scenario Schema

**Status**: Schema Definition  
**Target**: Demo_02 Combat Scenario  
**Last Updated**: 2025-12-01

---

## Overview

This document defines the JSON schema for Demo_02 combat scenarios. Demo_02 demonstrates fleet combat with multiple factions engaging in an asteroid field.

---

## JSON Schema

```json
{
  "name": "demo_02_combat",
  "game": "Space4X",
  "seed": 54321,
  "duration_seconds": 120,
  "fleets": [
    {
      "faction_id": "FACTION-1",
      "position": [0, 0, 0],
      "carrier_count": 2,
      "crafts_per_carrier": 4,
      "initial_order": "attack",
      "target_faction": "FACTION-2"
    },
    {
      "faction_id": "FACTION-2",
      "position": [100, 0, 100],
      "carrier_count": 2,
      "crafts_per_carrier": 4,
      "initial_order": "defend",
      "target_faction": null
    }
  ],
  "asteroid_field": {
    "count": 10,
    "center": [50, 0, 50],
    "radius": 30,
    "resource_types": ["Minerals", "RareMetals"]
  },
  "expectations": {
    "expectCombatEngagement": true,
    "expectFleetRetreat": false,
    "expectDamageTotal": 1000
  }
}
```

---

## Schema Fields

### Root Level

- **`name`** (string, required): Scenario identifier (e.g., "demo_02_combat")
- **`game`** (string, required): Must be "Space4X"
- **`seed`** (integer, required): RNG seed for determinism
- **`duration_seconds`** (number, required): Scenario runtime in seconds
- **`fleets`** (array, required): Array of fleet definitions
- **`asteroid_field`** (object, optional): Asteroid field configuration
- **`expectations`** (object, optional): Validation assertions

### Fleet Object

- **`faction_id`** (string, required): Faction identifier (e.g., "FACTION-1")
- **`position`** (array[3], required): Initial fleet position [x, y, z]
- **`carrier_count`** (integer, required): Number of carriers in fleet (1-10)
- **`crafts_per_carrier`** (integer, required): Number of crafts per carrier (1-10)
- **`initial_order`** (string, required): Initial fleet order
  - `"attack"`: Fleet attacks target faction
  - `"defend"`: Fleet defends position
  - `"patrol"`: Fleet patrols area
- **`target_faction`** (string|null, optional): Target faction ID (required if `initial_order` is "attack")

### Asteroid Field Object

- **`count`** (integer, required): Number of asteroids to spawn (5-50)
- **`center`** (array[3], required): Center position [x, y, z]
- **`radius`** (number, required): Spawn radius around center
- **`resource_types`** (array[string], optional): Resource types to use (default: all types)

### Expectations Object

- **`expectCombatEngagement`** (boolean, optional): Expect combat to occur
- **`expectFleetRetreat`** (boolean, optional): Expect a fleet to retreat
- **`expectDamageTotal`** (number, optional): Expected total damage dealt

---

## Example Scenario

**File**: `Assets/Scenarios/demo_02_combat.json`

```json
{
  "name": "demo_02_combat",
  "game": "Space4X",
  "seed": 54321,
  "duration_seconds": 120,
  "fleets": [
    {
      "faction_id": "FACTION-1",
      "position": [-50, 0, 0],
      "carrier_count": 2,
      "crafts_per_carrier": 4,
      "initial_order": "attack",
      "target_faction": "FACTION-2"
    },
    {
      "faction_id": "FACTION-2",
      "position": [50, 0, 0],
      "carrier_count": 2,
      "crafts_per_carrier": 4,
      "initial_order": "defend",
      "target_faction": null
    }
  ],
  "asteroid_field": {
    "count": 10,
    "center": [0, 0, 0],
    "radius": 30,
    "resource_types": ["Minerals", "RareMetals"]
  },
  "expectations": {
    "expectCombatEngagement": true,
    "expectFleetRetreat": false
  }
}
```

---

## Integration with PureDOTS ScenarioRunner

Demo_02 scenarios are loaded via PureDOTS `ScenarioRunner`:

```csharp
var scenarioRunner = World.GetOrCreateSystemManaged<ScenarioRunner>();
scenarioRunner.LoadScenario("Assets/Scenarios/demo_02_combat.json");
```

The ScenarioRunner will:
1. Parse the JSON schema
2. Spawn entities according to fleet definitions
3. Set initial orders (attack/defend/patrol)
4. Spawn asteroid field
5. Track expectations for validation

---

## Phase Flow

**Phase 1 (0-30s)**: Fleets approach asteroid field
- Fleets move toward asteroid field center
- Carriers maintain formation
- Crafts stay with carriers

**Phase 2 (30-90s)**: Fleets engage
- Fleets detect each other
- Combat begins (CombatState.IsInCombat = true)
- Projectiles fire
- Damage occurs

**Phase 3 (90-120s)**: Resolution
- One fleet retreats or is destroyed
- Remaining fleet secures asteroid field
- Combat ends

---

## Notes

- Fleet positions should be far enough apart to allow approach phase
- Asteroid field should be between fleets for contested resource
- Initial orders determine fleet behavior (attack vs defend)
- CombatState should be set by sim systems when engagement begins

---

**End of Schema**

