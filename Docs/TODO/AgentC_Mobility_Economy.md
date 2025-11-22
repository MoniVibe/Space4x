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
- Add tech diffusion components and apply upgrades; validate time-scale gates and disabled growth configs.

## Notes
- Status/hand-off lives in `Docs/Progress.md`. Update it when you start/stop to keep the single source of truth accurate.
- Surface engine-level gaps back to PureDOTS TODOs if you uncover them.
