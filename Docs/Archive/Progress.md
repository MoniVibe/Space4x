# Space4X Progress (rolling)

Keep this file up to date; overwrite status as work advances. Snapshot to `Docs/Archive/` after major milestones.

Last updated: 2025-12-11 (Stub pass: PureDOTS shared concepts + Space4X/Godgame scene glue + AI behavior/perception + fighter squadron/formation + navigation/movement + economy placeholders + shared service bridges + sensors/time/narrative/save + behavior/initiative/needs MVP + decision + ambition + interception + communication + trade + morphing scaffolding + aggregate (bands/fleets/guilds) + crafting hooks added to unblock demo wiring while scenes are built.)

When told to "proceed work," open your agent TODO (A/B/C below) and update this file at start and end. This is the single source of truth for current status.

## Current milestone
- Phase 2 Demo rewind/time validation; Phase 3 Agents spinning up (A: alignment/compliance; B: modules/degradation; C: mobility/economy/tech-time).

## Agent statuses
- Agent 1 (Phase 2 rewind/time): In progress. Open: rewind determinism tests for mining/haul; registry continuity validation.
- Agent A (Alignment/compliance/doctrine): In progress. Doctrine baker + StanceId enum added; compliance now emits telemetry (including suspicion max/alerts), planner tickets + inbox, and aggregation normalizes stance/race/culture buffers. Mining demo carriers/miners now bake alignment/affiliation. Next: mutiny/desertion demo + scene-wide affiliation pass.
- Agent B (Modules/degradation/skills): In progress. Module maintenance log + telemetry added; refit/repair/health hooks emit events + XP; station-only refit gating + overhaul repairs landed; maintenance playback rebuilds telemetry. **COMPLETE**: Module/hull catalog system with blob assets (`ModuleCatalogAuthoring`, `HullCatalogAuthoring`, `RefitRepairTuningAuthoring`), facility proximity detection (`FacilityProximitySystem`), catalog-based refit time calculation, rating aggregation (`Space4XModuleRatingAggregationSystem`), scenario loader (`Space4XRefitScenarioSystem`) and action processor (`Space4XRefitScenarioActionProcessor`), telemetry wiring (`Space4XModuleTelemetryAggregationSystem`), and tests. Burst compatibility verified. Next: extend skill XP/command logs into combat/haul/hazard flows. TODOs in `Docs/TODO/AgentB_Modules_Degradation.md`.
- Agent C (Mobility/economy/tech/time): In progress. Tech diffusion baseline + telemetry/logging landed; next: mobility graph maintenance + economy pricing/queue handling. TODOs in `Docs/TODO/AgentC_Mobility_Economy.md`.

## Next 3 steps
1) Add PlayMode rewind determinism tests for mining → haul and registry continuity assertions.
2) Wire alignment/affiliation buffers into prefabs + sample mutiny/desertion scene; bridge breach tickets into planner/narrative consumers (planner inbox + queue in place).
3) Extend skill XP + command-log coverage into combat/haul/hazard flows and add maintenance authoring/registry hooks (Agent B); audit mobility/economy TimeState scaling and pricing/trade-op hooks (Agent C). Stubbed components are ready for hookup across scenes.

## Links
- Agent A: `Docs/TODO/AgentA_Alignment.md`
- Agent B: `Docs/TODO/AgentB_Modules_Degradation.md`
- Phase 2: `Docs/TODO/Phase2_Demo_TODO.md`
- Integration index: `Docs/TODO/4xdotsrequest.md`

