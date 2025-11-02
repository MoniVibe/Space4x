# System Ordering Overview

The Pure DOTS template uses the following custom groups and ordering rules:

## Initialization
- `TimeSystemGroup` runs first inside `InitializationSystemGroup` (orderFirst).
  - `CoreSingletonBootstrapSystem` seeds time/history/rewind singletons.
  - `TimeSettingsConfigSystem` applies authoring overrides before other time systems.
  - `HistorySettingsConfigSystem` updates history singleton after time settings.
  - `TimeTickSystem` runs after history config to advance deterministic ticks.

## Simulation
- `VillagerSystemGroup` runs after `FixedStepSimulationSystemGroup`.
  - `VillagerNeedsSystem` updates hunger/energy/health.
  - `VillagerStatusSystem` adjusts availability/mood after needs.
  - `VillagerJobAssignmentSystem` assigns worksites after status calculations.
  - `VillagerTargetingSystem` resolves target entities to positions.
  - `VillagerAISystem` evaluates goals (after needs) and feeds movement.
  - `VillagerMovementSystem` updates positions after targeting.

- `ResourceSystemGroup` runs after `VillagerSystemGroup`.
  - `ResourceGatheringSystem` consumes worksite assignments.
  - `ResourceDepositSystem` executes after gathering.
  - `StorehouseInventorySystem` updates aggregate totals after deposits.
  - History systems and respawn management follow to capture state.

- `LateSimulationSystemGroup` (custom) is ordered last within `SimulationSystemGroup` for history/cleanup.

## Physics
- Combat and hand interaction groups are slotted between `BuildPhysicsWorld` and `ExportPhysicsWorld` via `UpdateAfter/Before` attributes.

## Rewind Routing
- `RewindCoordinatorSystem` runs early in simulation to enable/disable record, catch-up, or playback groups.

Consult this document when adding new systems—ensure their `UpdateInGroup`, `UpdateAfter`, or `UpdateBefore` attributes align with the deterministic scheduling expectations.
