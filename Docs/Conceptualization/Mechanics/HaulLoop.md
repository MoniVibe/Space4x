# Mechanic: Haul Loop

## Overview

**One-line description**: Carriers shuttle mined resources, trade goods, and construction materials between extraction sites, stations, and colonies to keep the empire supplied.

## Core Concept

The haul loop is the fourth foundational priority, binding mining output to construction and trade demands. Carriers act as configurable logistics platforms, executing serialized routing plans that players can mod pre-launch. Efficient hauling maintains station build queues, feeds colonization efforts, and underwrites combat readiness through steady supply.

## How It Works

### Basic Rules

1. Define haul routes linking origin points (mines, trade hubs) to destinations (stations, shipyards, colonies).
2. Assign carriers with appropriate cargo modules and schedule cadence (continuous shuttle, convoy, on-demand).
3. Execute routes, transferring cargo according to priority tiers and clearing holds so mining and trade loops remain unblocked.

### Parameters and Variables

| Parameter | Default Value | Range | Effect |
|-----------|---------------|-------|--------|
| RouteLength | 2 jumps | 1-10 jumps | Impacts travel time and fuel usage.
| CargoPriority | Balanced | Low–Critical | Determines delivery order when capacity constrained.
| TransitRisk | 0.1 | 0-1 | Influences need for escorts or stealth modules.
| TransferRate | 1.0 | 0.2-3.0 | Throughput per tick at loading/unloading.
| ScheduleCadence | Continuous | Continuous–Batch | Controls whether carriers wait for full loads.

### Edge Cases

- **Bottlenecks**: Station queues overflow when hauling lags; triggers alerts for player intervention or auto-reassignment.
- **Route Disruption**: Combat or anomalies along a route force rerouting, integrating closely with exploration intel.
- **Overdelivery**: Excess stockpiles degrade if storage caps exceeded, encouraging distributed logistics planning.
  - **Spoilage Rate**: 2% per tick for consumables only (food, fuel, certain organics).
  - **Usage Priority**: Consumption prioritized from oldest stock first (FIFO).
  - **Durable Goods**: Ore, metals, electronics do not degrade.

## Player Interaction

### Player Decisions

- Selecting which resources get priority when capacity is limited.
  - **Priority Resolution**: Low-priority cargo finds alternate routing instead of waiting indefinitely.
  - **Emergency Reallocation**: Priorities can change mid-route for critical situations.
- Choosing between resilient convoys with escorts or agile single-carrier shuttles.
- Deciding when to spin up temporary forward depots to shorten routes.
- **Route Planning**: Players can plot waypoints via UI drawing, auto-pathfinding, or waypoint selection.
  - **Waypoint Persistence**: Waypoints are spatial and persistent, allowing other vessels to reuse them.
  - **Infrastructure**: Entities and players can construct hyper highways and gateways to connect routes at will.

### Skill Expression

Veteran players dynamically rebalance routes, anticipate combat threats, and leverage serialized configuration to script contingencies that keep stations supplied even during crises.

### Feedback to Player

- Visual: Route overlays, congestion heatmaps, and capacity indicators on carriers and stations.
- Numerical: Supply dashboards showing inflow/outflow rates, backlog timers, and resource deficits.
- Audio: Alerts for stalled deliveries or route blockages.

## Balance Considerations

### Balance Goals

- Hauling should be essential but not tedious; success depends on strategic planning rather than micromanagement.
- The loop must surface choke points that encourage combat or station construction responses.
- Risk scales with cargo: routine goods move safely, while hauling ordnance or war materiel into warzones demands hazard pay and escorts.
- **Fleet Resupply**: Haulers keep fleets resupplied on the move.
  - **High Tech**: Haulers intercept moving fleets (dynamic rendezvous).
  - **Low Tech**: Fleets rendezvous at designated waypoints.
  - **Consumables**: Fuel (mined from gas giants), food (synthesized from organics), ammo (fabricated from materials).
  - **Crew**: Cloned or bred over time passively if faction policies support it.
- **Trading**: Haulers can purchase from neutral hubs.
  - **Station Pricing**: Different stations/colonies have varying prices based on supply and demand.
  - **Payment Methods**: Haulers use resources on hand or faction's reputation/standing.
  - **Auto-Trade**: Haulers execute trades automatically when configured.

### Tuning Knobs

1. **Transfer Rate Scaling**: Adjust how upgrades or crew quality boost throughput.
2. **Transit Risk Multipliers**: Increase or decrease hazard impact per sector.
3. **Storage Limits**: Tune station capacity to create meaningful but manageable pressure.

### Known Issues

- TBD until logistics simulations expose dominant bottlenecks.

## Integration Points

| System/Mechanic | Type of Interaction | Priority |
|-----------------|---------------------|----------|
| Mining Loop | Supplies raw material, requires empty holds | Critical |
| Combat Loop | Provides escorts and route security | High |
| Exploration Loop | Updates safe paths and warns of disruptions | High |

### Emergent Possibilities

- Player-created modular carriers that switch between hauling and combat roles depending on doctrine triggers.
- Dynamic trade agreements with NPC factions once diplomacy layers exist, using haul routes as the enforcement mechanism.

## Shareability Assessment

**PureDOTS Candidate:** Partial

**Rationale:** Core logistics mechanics (routes, waypoints, load/unload) could be shared, but carrier-specific implementation and Space4x trade mechanics are game-specific.

**Shared Components:**
- `RouteComponent`: Route definition and waypoints
- `WaypointComponent`: Spatial waypoint entities
- `CargoComponent`: Generic cargo tracking
- `LoadUnloadCommand`: Generic transfer commands

**Game-Specific Adapters:**
- Space4x: Carrier modules, fleet interception, trade pricing
- Godgame: Would need different transport mechanics (villager-based logistics)

## Technical Implementation

### Technical Approach

- Represent routes as serialized graphs so modders can predefine or alter initial logistics networks.
- Use DOTS command buffers to queue load/unload operations, ensuring deterministic sequencing under heavy entity counts.
- Integrate with registry telemetry to track supply metrics.
- **Waypoint System**: Spatial waypoints stored as persistent entities with spatial grid registration.
  - Carriers query nearby waypoints for route planning.
  - Hyper highways/gateways modify pathfinding costs and travel times.
- **Supply/Demand Pricing**: Station pricing components track inflow/outflow; Burst jobs recalculate prices per tick.
- **Fleet Interception**: High-tech fleets broadcast position/velocity; haulers use spatial queries + predictive pathfinding for intercepts.

## Performance Budget

- **Max Entities:** 1,000 active routes, 5,000 waypoints
- **Update Frequency:** Per tick (route calculations batched)
- **Burst Compatibility:** Yes - all route/pathfinding systems Burst-compiled
- **Memory Budget:** ~128 bytes per route, ~32 bytes per waypoint

## Examples

### Example Scenario 1

**Setup**: Mining carriers fill holds faster than stations can receive.  
**Action**: Player creates a secondary route to a refinery station closer to the belt.  
**Result**: Congestion clears, mining uptime stays high, and hauling network stabilizes.

### Example Scenario 2

**Setup**: Player-modified start spawns long-haul trade convoy with high TransitRisk sectors.  
**Action**: Escorts rerouted from combat loop secure the trade lane while exploration scouts alternate safe corridors.  
**Result**: Supply chain persists despite elevated danger, at the cost of reduced frontier combat coverage.

## References and Inspiration

- **EVE Online** hauling logistics and convoy gameplay.
- **Supreme Commander** mass/fabricator transport mechanics.
