# MiningCombatDemo Scene Setup Guide

## Overview
This guide describes how to set up the `MiningCombatDemo.unity` scene for the mining + combat showcase demo.

## Scene Structure

### Required Components on Root GameObject
1. **PureDotsConfigAuthoring** - Bootstraps PureDOTS runtime config
2. **SpatialPartitionAuthoring** - Sets up spatial grid for registry queries
3. **Space4XMiningDemoAuthoring** - Main authoring component (see configuration below)

### Space4XMiningDemoAuthoring Configuration

#### Carriers (2-3 friendly, 1-2 hostile)

**Friendly Carrier 1:**
- CarrierId: "FRIENDLY-CARRIER-1"
- Position: (0, 0, 0)
- Speed: 5
- PatrolCenter: (0, 0, 0)
- PatrolRadius: 50
- IsHostile: false
- FleetPosture: Patrol
- FleetId: "FRIENDLY-FLEET-1"
- CanIntercept: true
- InterceptSpeed: 10

**Friendly Carrier 2:**
- CarrierId: "FRIENDLY-CARRIER-2"
- Position: (100, 0, 0)
- Speed: 5
- PatrolCenter: (100, 0, 0)
- PatrolRadius: 50
- IsHostile: false
- FleetPosture: Patrol
- FleetId: "FRIENDLY-FLEET-2"
- CanIntercept: true
- InterceptSpeed: 10

**Hostile Carrier 1:**
- CarrierId: "HOSTILE-CARRIER-1"
- Position: (400, 0, 0)  // Enters from edge
- Speed: 6
- PatrolCenter: (300, 0, 0)
- PatrolRadius: 100
- IsHostile: true
- FleetPosture: Engaging
- FleetId: "HOSTILE-FLEET-1"
- CanIntercept: true
- InterceptSpeed: 12

#### Mining Vessels (3-5 per carrier, start docked)

**For FRIENDLY-CARRIER-1:**
- VesselId: "MINER-F1-1" through "MINER-F1-5"
- CarrierId: "FRIENDLY-CARRIER-1"
- StartDocked: true
- Position: (0, 0, 0)  // Will be at carrier position when docked
- Speed: 10
- MiningEfficiency: 0.8
- CargoCapacity: 100
- ResourceId: "space4x.resource.minerals"

**For HOSTILE-CARRIER-1:**
- VesselId: "MINER-H1-1" through "MINER-H1-3"
- CarrierId: "HOSTILE-CARRIER-1"
- StartDocked: true
- Position: (400, 0, 0)
- Speed: 10
- MiningEfficiency: 0.75
- CargoCapacity: 100

#### Asteroids (scattered across 500-800m span)

Create 15-20 asteroids scattered across the belt:
- Positions ranging from (-400, 0, -200) to (400, 0, 200)
- Varying densities: dense clusters near (0, 0, 0), sparse at edges
- ResourceAmount: 200-1000 per asteroid
- MiningRate: 10-20 per asteroid

Example asteroid positions (spread across belt):
- Cluster 1: (0, 0, 0), (20, 0, 10), (-15, 0, 5)
- Cluster 2: (150, 0, 50), (170, 0, 60), (140, 0, 40)
- Cluster 3: (-200, 0, -100), (-180, 0, -90), (-220, 0, -110)
- Sparse: (300, 0, 150), (-350, 0, -180), (250, 0, -100)

#### Affiliations

**Friendly:**
- AffiliationId: "FRIENDLY-AFFILIATION"
- DisplayName: "Allied Mining Fleet"
- Loyalty: 1.0

**Hostile:**
- AffiliationId: "HOSTILE-AFFILIATION"
- DisplayName: "Raider Fleet"
- Loyalty: 0.0

### Additional GameObjects

#### Patrol Escorts (Optional)
Create 1-2 escort vessels per faction using Space4XCarrierCombatAuthoring:
- Add to carrier GameObjects as children or separate entities
- Configure with InterceptCapability
- Set patrol routes that cross the belt

#### Deposit Locations
- Position carriers/stations at strategic points
- One contested location at (200, 0, 0) - midpoint between friendly and hostile zones

### Telemetry/HUD Setup
- Ensure TelemetryStream bootstrap system runs (automatic via PureDotsConfigAuthoring)
- Add debug UI prefab if available to visualize metrics
- Enable console logging for telemetry events

## Validation Checklist

After setting up the scene, verify in Play Mode:

- [ ] Miners receive MiningOrder targets and dispatch from carriers
- [ ] Vessels move to asteroids and begin mining
- [ ] Mining loop completes: gather → spawn → carrier pickup
- [ ] Asteroids appear in ResourceRegistryEntry buffer
- [ ] Carriers appear in Space4XFleetRegistryEntry buffer
- [ ] Intercept requests generated when hostiles enter range
- [ ] Intercept courses calculated and applied
- [ ] Telemetry metrics published (mining ticks, intercept attempts)
- [ ] No singleton errors in console
- [ ] Registry buffers populate correctly

## Scene File Location
`Assets/Scenes/Demos/MiningCombatDemo.unity`

## Notes
- Scene should be added to Build Settings for batchmode testing
- Sub-scenes can be used for authoring/presentation separation if needed
- Consider creating prefabs for carriers/miners to simplify setup

