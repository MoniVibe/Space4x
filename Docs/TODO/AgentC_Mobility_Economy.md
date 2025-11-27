# Agent C – Mobility, Infrastructure, Economy, Tech/Time

Start/resume here when working on Agent C. Keep `Docs/Progress.md` updated when you begin/end work.

## Scope
- Mobility graph: waypoint/highway/gateway pathfinding, maintenance, queue handling; interception/rendezvous broadcasts and HUD/telemetry for graph health.
- Economy/logistics: supply/demand pricing, trade-op identification, deterministic FIFO consumption with spoilage; registry/telemetry wiring.
- Tech/time/growth: tech diffusion state and upgrade application; enforce `TimeState` scaling across systems; carry breeding/cloning framework as disabled gates with validation.

## Deliverables
- Mobility systems with deterministic ordering and tests for intercept calculations/maintenance; HUD/telemetry signals.
- Economy/logistics systems for pricing, trade ops, FIFO/spoilage consumption with telemetry + command-log entries.
- Tech diffusion/time-scale compliance updates; guarded growth configs/tests proving disabled-by-default behavior.

## Open items / next steps
- Finish pathfinding + queue handling and interception/rendezvous broadcasts; add tests for mobility graph maintenance.
- Extend batch inventory with dynamic pricing and FIFO/spoilage consumption; wire metrics into registry snapshots/telemetry.
- Extend tech diffusion: bridge per-faction metrics into registry telemetry snapshots, add command-log playback coverage, and audit TimeState scaling across mobility/economy systems.

### Specific Tasks for Mobility Graph
- **Pathfinding System**: Implement waypoint/highway/gateway pathfinding for carriers
  - Create `MobilityGraph` component/system that maintains waypoint network
  - Implement A* pathfinding using waypoints as nodes
  - Support highway shortcuts (faster routes) and gateway jumps (instant travel)
- **Queue Handling**: Implement deterministic queue system for pathfinding requests
  - Create `PathfindingRequest` buffer for queued pathfinding jobs
  - Process requests in deterministic order (tick-based priority)
  - Cache path results for reuse
- **Interception/Rendezvous**: Add broadcast system for fleet positions/velocities
  - Extend `FleetMovementBroadcast` component with velocity/heading data
  - Calculate intercept courses using relative velocities
  - Broadcast intercept opportunities to HUD/telemetry
- **Graph Maintenance**: System to add/remove waypoints dynamically
  - Support player-created waypoints (future)
  - Handle waypoint destruction/disconnection
  - Maintain graph connectivity

### Specific Tasks for Economy/Pricing
- **Dynamic Pricing**: Implement supply/demand based pricing system
  - Create `ResourcePrice` component/system that tracks market prices per resource type
  - Price = basePrice * (1 + demandMultiplier - supplyMultiplier)
  - Update prices based on colony supply/demand from `Space4XColonySupply`
- **Trade Operations**: Identify profitable trade opportunities
  - Create `TradeOpportunity` component that identifies price differences between colonies
  - Calculate profit margins (price difference - transport cost)
  - Emit trade opportunities to AI/player systems
- **FIFO Consumption**: Implement deterministic resource consumption with spoilage
  - Extend `ResourceStorage` buffer with timestamp/expiration data
  - Process consumption in FIFO order (oldest resources first)
  - Apply spoilage rates based on resource type and storage conditions
  - Log consumption events for replay support
- **Registry Integration**: Wire economy metrics into registry snapshots
  - Add `EconomySnapshot` component with price averages, trade volume, spoilage rates
  - Emit telemetry metrics: `space4x.economy.prices.*`, `space4x.economy.trade.volume`, `space4x.economy.spoilage.*`

## Notes
- Status/hand-off lives in `Docs/Progress.md`. Update it when you start/stop to keep the single source of truth accurate.
- Surface engine-level gaps back to PureDOTS TODOs if you uncover them.
