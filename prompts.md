# Agent Prompts (split)

## Agent 1: Phase 2 Demo wrap — time/rewind validation
Goal: Close the Phase 2 milestone by proving deterministic rewind and registry continuity for mining/haul flows.
Scope:
- Validate rewind determinism for resource counts/state and ensure time spine snapshot + command log resims cleanly.
- Add PlayMode tests for rewind determinism (mining/haul) and registry continuity for mining/haul entities.
- Verify TimeState/RewindState cadence for mining spine, carrier pickup, and telemetry; document gaps.
Deliverables:
- PlayMode/NUnit coverage that exercises rewind/resim loops and asserts deterministic resource/telemetry results.
- Verified mining spine + carrier haul continuity across snapshots/replays; notes on any fixes forwarded to engine TODO.
Read first: `Docs/TODO/Phase2_Demo_TODO.md` (rewind/time notes) and `PureDOTS/phase3.md` (time spine/rewind context).

## Agent 2: Phase 3 — Alignment/compliance + doctrine (Agent A)
Goal: Land alignment/affiliation chain and doctrine authoring with tests and planner/telemetry hooks.
Scope:
- Push alignment/affiliation buffers onto crew/fleet/colony/faction entities; author CrewAggregationSystem; slot `Space4XAffiliationComplianceSystem` after aggregation and route suspicion deltas into intel/alert surfaces.
- Ship `DoctrineAuthoring` baker + enum registry for ethics/outlooks/affiliations; add inspector validation (range clamps, fanatic caps) and stage a micro mutiny/desertion demo scene.
- Bridge breach outputs into AI planner tickets, telemetry snapshots, and narrative triggers; add runtime assertions for missing doctrine/affiliation data.
Deliverables:
- Alignment/compliance buffers + systems with deterministic ordering, registry bridge ties, and intel/telemetry hooks.
- Doctrine authoring/bakers/enums + inspector validation; sample scene for mutiny/desertion checks.
- NUnit coverage for compliance/loyalty/suspicion scaling and breach integration surfaces.
Read first: `PureDOTS/phase3.md` (Space4x DOTS TODO + Agent A) and `Docs/TODO/AgentA_Alignment.md`.

## Agent 3: Phase 3 — Modules/degradation + crew skill follow-through (Agent B)
Goal: Finish carrier module/refit pipeline and component degradation/repair, then extend skills into refit/repair/combat/haul/hazard flows.
Scope:
- Implement module slot/refit/archetype transitions (modules as entities) with stat aggregation, refit gating, and deterministic queues; add registry-friendly authoring/bakers.
- Build component health/degradation/field repair/station overhaul/failure flows with prioritized repair queues and hazard hooks.
- Extend crew skills: broaden XP sources, apply modifiers to refit/repair/combat/hauling, integrate hazard resistance, and expand tests.
Deliverables:
- Module + degradation systems/components/tests validating refit states, stat aggregation, repair priorities, and failure handling.
- Skill systems wired into refit/repair/combat/haul/hazard paths with telemetry/command-log entries and coverage.
- Authoring helpers/validation for module slots, health settings, and skill buffers.
Read first: `PureDOTS/phase3.md` (Agent B + Modules/Degradation/Skills) and `Docs/TODO/AgentB_Modules_Degradation.md`.

## Agent 4: Phase 3 — Mobility/infrastructure + economy/logistics + tech/time/growth (Agent C)
Goal: Complete mobility graph/interception work, extend economy/logistics systems, and enforce tech/time compliance with guarded growth.
Scope:
- Finish waypoint/highway/gateway pathfinding + maintenance + queue handling; solidify interception/rendezvous broadcasts/path requests and HUD/telemetry for mobility graph health.
- Extend batch inventory/spoilage with supply/demand pricing, trade-op identification, deterministic FIFO consumption; keep registry/telemetry wiring aligned.
- Add tech diffusion state + upgrade application; enforce `TimeState` scaling across systems; carry breeding/cloning framework as disabled gates with validation.
Deliverables:
- Mobility systems with deterministic ordering and tests for intercept calculations and maintenance.
- Economy/logistics systems for pricing, trade ops, FIFO/spoilage consumption with telemetry + command-log entries.
- Tech diffusion/time-scale compliance updates and guarded growth configs/tests proving disabled-by-default behavior.
Read first: `PureDOTS/phase3.md` (Agent C) and `Docs/TODO/AgentC_Mobility_Economy.md`.

## Coordination Notes
- Update `Docs/TODO/4xdotsrequest.md` when schemas/telemetry keys move so all agents and bridges stay aligned.
- Honor `TimeState`/snapshot + command-log flows for determinism; avoid service locators and keep systems Burst-friendly in `Space4x.Gameplay`.
- Keep defaults safe: breeding/cloning gated off, intercept tech gates explicit, presentation optional/headless-safe.
- Remember the tri-project split (`TRI_PROJECT_BRIEFING.md`): PureDOTS is the shared engine; surface engine-level gaps to `PureDOTS_TODO` and mirror patterns for Godgame where applicable.
