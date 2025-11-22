# Phase 3 Agent Prompts

## Agent A: Alignment/compliance + doctrine tooling
Goal: Land alignment/affiliation chain and doctrine authoring with tests and planner/telemetry hooks.
Scope:
- Push alignment/affiliation buffers onto crew/fleet/colony/faction entities; author CrewAggregationSystem; slot `Space4XAffiliationComplianceSystem` after aggregation and route suspicion deltas into intel/alert surfaces.
- Ship `DoctrineAuthoring` baker + enum registry for ethics/outlooks/affiliations; add inspector validation (range clamps, fanatic caps) and stage a micro mutiny/desertion demo scene.
- Bridge breach outputs into AI planner tickets, telemetry snapshots, and narrative triggers; add runtime assertions for missing doctrine/affiliation data.
Deliverables:
- Alignment/compliance buffers + systems with deterministic ordering, registry bridge ties, and intel/telemetry hooks.
- Doctrine authoring/bakers/enums + inspector validation; sample scene for mutiny/desertion checks.
- NUnit coverage for compliance/loyalty/suspicion scaling and breach integration surfaces.
Read first: `PureDOTS/phase3.md` (Space4x DOTS TODO + Agent A) and `Docs/TODO/4xdotsrequest.md` (Alignment/Compliance + Doctrine sections).

## Agent B: Modules/degradation + crew skill follow-through
Goal: Finish carrier module/refit pipeline and component degradation/repair, then extend skills into refit/repair/combat/haul/hazard flows.
Scope:
- Implement module slot/refit/archetype transitions (modules as entities) with stat aggregation, refit gating, and deterministic queues; add registry-friendly authoring/bakers.
- Build component health/degradation/field repair/station overhaul/failure flows with prioritized repair queues and hazard hooks.
- Extend crew skills: broaden XP sources, apply modifiers to refit/repair/combat/hauling, integrate hazard resistance, and expand tests.
Deliverables:
- Module + degradation systems/components/tests validating refit states, stat aggregation, repair priorities, and failure handling.
- Skill systems wired into refit/repair/combat/haul/hazard paths with telemetry/command-log entries and coverage.
- Authoring helpers/validation for module slots, health settings, and skill buffers.
Read first: `PureDOTS/phase3.md` (Agent B + Modules/Degradation/Skills) and `Docs/TODO/4xdotsrequest.md` (Module System, Component Degradation, Crew Experience sections).

## Agent C: Mobility/infrastructure + economy/logistics + tech/time/growth
Goal: Complete mobility graph + interception queues, finish supply/demand pricing and FIFO/spoilage consumption, and wire tech diffusion/time compliance with growth gated off.
Scope:
- Finish waypoint/highway/gateway pathfinding + maintenance + queue handling; solidify interception/rendezvous broadcasts/path requests and HUD/telemetry for mobility graph health.
- Extend batch inventory/spoilage work with supply/demand pricing, trade-op identification, and deterministic FIFO consumption; keep registry/telemetry wiring aligned.
- Add tech diffusion state + upgrade application, enforce `TimeState` scaling across systems, and carry the breeding/cloning framework as disabled gates with validation.
Deliverables:
- Mobility systems (path requests, interception queues, rendezvous fallback) with spatial queries and deterministic ordering; tests covering intercept calculations and maintenance.
- Economy/logistics systems for pricing, trade ops, and FIFO/spoilage consumption with telemetry + command-log entries.
- Tech diffusion/time-scale compliance updates and guarded growth configs/tests proving disabled-by-default behavior.
Status note: mobility registry scaffold + batch inventory with spoilage/FIFO already landed in PureDOTS; pending pathfinding/queues/dynamic pricing/tech diffusion integration—do not duplicate the completed stubs.
Read first: `PureDOTS/phase3.md` (Agent C + status note) and `Docs/TODO/4xdotsrequest.md` (Waypoint/Infrastructure, Supply & Demand, Resource Spoilage, Tech Diffusion, Breeding/Cloning).

## Coordination Notes
- Update `Docs/TODO/4xdotsrequest.md` when schemas/telemetry keys move so all agents and bridges stay aligned.
- Honor `TimeState`/snapshot + command-log flows for determinism; avoid service locators and keep systems Burst-friendly in `Space4x.Gameplay`.
- Use spatial grid queries with deterministic sorting for mobility/interception; keep registry bridge changes visible to Godgame/PureDOTS consumers.
- Keep defaults safe: breeding/cloning gated off, intercept tech gates explicit, presentation optional/headless-safe.
- Remember the tri-project split (`TRI_PROJECT_BRIEFING.md`): PureDOTS is the shared engine; surface engine-level gaps to `PureDOTS_TODO` and mirror patterns for Godgame where applicable.
