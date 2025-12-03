# Space4X Demo Specification

## Overview

Single scene demo showcasing deterministic combat, mining, compliance, and carrier operations with live binding swaps.

## Demo Slices

### 1. Combat Duel

**Setup**: Two hulls, weapons/projectiles

**Demonstrates**:
- Hit totals, crit rate, modular damage (engine/bridge)
- Directional modules (aft volleys tag engines)
- Same duel yields identical damage at different frame rates

**Metrics**:
- `damage_total`
- `hits`
- `crit%`
- `modules_destroyed{}`
- `hull_HP`

### 2. Mining Loop

**Setup**: Miners + haulers + station

**Demonstrates**:
- Mining throughput & storage
- Resource gathering loop
- Carrier pickup and deposit

**Metrics**:
- `mining_throughput/s`
- `station_stock`
- `vessels_active`
- `resources_mined_total`

### 3. Compliance Demo

**Setup**: Safe zone infraction → sanction path

**Demonstrates**:
- Compliance triggers deterministically
- Reputation deltas
- Sanction application

**Metrics**:
- `sanctions_triggered`
- `reputation_delta`
- `safe_zone_violations`

### 4. Carrier Ops

**Setup**: Damage → dock → repair/refit

**Demonstrates**:
- Damage system
- Docking mechanics
- Repair/refit workflows
- Crew modifiers visible

**Metrics**:
- `modules_destroyed`
- `repair_time`
- `refit_count`
- `crew_buffs` (accuracy/heat/repair)

### 5. Avoidance (Optional)

**Setup**: Toggle veteran proficiency

**Demonstrates**:
- Reduced AoE/chain casualties
- Proficiency modifiers
- Crew skill effects

**Metrics**:
- `veteran_proficiency` (on/off)
- `casualties_reduced`
- `accuracy_bonus`

## Hotkeys

| Key | Action |
|-----|--------|
| `P` | Pause/Play |
| `J` | Toggle Jump/Flank planner |
| `B` | Swap Minimal/Fancy bindings |
| `V` | Toggle veteran proficiency on/off |
| `R` | Rewind sequence |

## HUD Layout

### Left Panel (Game State)

**Combat Metrics**:
- `damage_total` - Total damage dealt
- `hits` - Number of hits
- `crit%` - Critical hit percentage
- `modules_destroyed{}` - Breakdown by module type
- `hull_HP` - Current hull health

**Mining Metrics**:
- `mining_throughput/s` - Resources per second
- `station_stock` - Station inventory
- `sanctions_triggered` - Compliance events

**Crew Buffs**:
- Accuracy modifier
- Heat reduction
- Repair rate

### Right Panel (System Metrics)

- `fixed_tick_ms` - Fixed step duration
- `alloc_bytes` - Memory allocation
- `tick` - Current simulation tick
- `fps` - Frame rate
- `snapshot_kb` - Snapshot ring buffer usage

## Acceptance Criteria

### Determinism

- Same duel yields identical damage at 30/60/120 fps
- Log "deterministic OK" after rewind run
- Byte-equal check for state snapshots

### Compliance

- Compliance triggers deterministically
- Safe zone infractions produce consistent sanctions
- Reputation deltas match across runs

### Binding Swap

- Minimal↔Fancy swap: visuals change, metrics identical
- Removing PresentationBridge → sim runs; counters still tick
- No exceptions during swap

## Component Requirements

### For Combat Participation

**Required**:
```csharp
Space4XFleet fleet;           // Fleet identity
LocalTransform transform;       // Position
SpatialIndexedTag tag;         // Spatial indexing
FleetMovementBroadcast broadcast; // Position/velocity broadcast
```

**Optional**:
```csharp
InterceptCapability capability; // Interception capability
VesselStanceComponent stance;   // Stance-based AI
FormationData formation;        // Formation coordination
```

### For Mining Loop

**Required**:
```csharp
MiningVessel vessel;           // Vessel identity
MiningOrder order;             // Mining target
MiningState state;             // Mining phase
MiningYield yield;             // Accumulated yield
Carrier carrier;               // Carrier reference
ResourceStorage storage;       // Storage buffer
```

### For Compliance

**Required**:
```csharp
ComplianceState compliance;    // Compliance tracking
SafeZone zone;                 // Safe zone definition
SanctionHistory sanctions;     // Sanction buffer
```

## System Dependencies

### Bootstrap Systems (InitializationSystemGroup)

1. `Space4XCoreSingletonGuardSystem` - Creates TimeState, RewindState, GameplayFixedStep
2. `Space4XMiningTimeSpineBootstrapSystem` - Creates mining time spine
3. `Space4XFleetInterceptBootstrapSystem` - Creates intercept queue
4. `Space4XResourceRegistryBootstrapSystem` - Ensures resource registry

### Combat Systems (FixedStepSimulationSystemGroup)

1. `FleetBroadcastSystem` - Updates fleet positions
2. `FleetInterceptRequestSystem` - Generates intercept requests
3. `InterceptPathfindingSystem` - Calculates intercept courses
4. `CombatResolutionSystem` - Resolves damage

### Mining Systems (FixedStepSimulationSystemGroup)

1. `Space4XMinerMiningSystem` - Processes mining ticks
2. `Space4XMiningYieldSpawnBridgeSystem` - Bridges yield to spawn requests
3. `MiningResourceSpawnSystem` - Spawns resource pickups
4. `CarrierPickupSystem` - Handles carrier pickup

### Compliance Systems (FixedStepSimulationSystemGroup)

1. `ComplianceDetectionSystem` - Detects violations
2. `SanctionApplicationSystem` - Applies sanctions
3. `ReputationUpdateSystem` - Updates reputation

### Telemetry Systems (PresentationSystemGroup)

1. `Space4XMiningTelemetrySystem` - Publishes mining metrics
2. `Space4XFleetInterceptTelemetrySystem` - Publishes intercept metrics
3. `Space4XComplianceTelemetrySystem` - Publishes compliance metrics

## Known-Good Scenarios

- `combat_duel_weapons.json` - Two carriers, mixed modules
- `mining_loop.json` - 4 miners, 2 haulers, 1 station
- `compliance_demo.json` - Infraction path
- `carrier_ops.json` - Damage → dock → refit

See [Scenarios.md](Scenarios.md) for detailed scenario specifications.

## Talk Track (2-3 minutes)

"Here's the deterministic loop—input→sim→present. Modules are directional; aft volleys tag engines. Compliance fires on safe-zone shots. Crew changes heat, repair, aim. Same duel yields identical damage at different frame rates; Minimal/Fancy doesn't change outcomes."

