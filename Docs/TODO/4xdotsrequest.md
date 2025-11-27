# Space4X DOTS Framework Request (Index)

Use this file as the jump-off for active DOTS tasks. Detailed scopes live in per-agent TODOs.

## Active Error Fix Requests (2025-11-27)

Requests submitted to PureDOTS team in `PureDOTS/Docs/ExtensionRequests/`:

- ~~**🔴 P0** Burst BC1016 errors: `2025-11-27-burst-bc1016-fixedstring-errors.md`~~ (RESOLVED but broken)
- **🔴 P0 BLOCKING** Burst BC1091 static constructor: `2025-11-27-burst-bc1091-static-constructor-errors.md` ← **NEW**
- **🟡 P1 MEDIUM** Missing SpatialPartitionAuthoring: `2025-11-27-spatial-partition-authoring-export.md`
- **🟢 P2 LOW** CreateAssetMenu warnings: `2025-11-27-createassetmenu-warnings.md`

**⚠️ NOTE:** The BC1016 fix used `static readonly FixedString*` fields which causes BC1091.
The correct fix is to use instance fields initialized in `OnCreate()`.

## Feature Requests

- Stat simulation support: `Docs/PureDOTS_Request_Space4xStats.md`

## Task TODOs

- Alignment/compliance/doctrine: `Docs/TODO/AgentA_Alignment.md`
- Modules, degradation, skills follow-through: `Docs/TODO/AgentB_Modules_Degradation.md`
- Mobility/economy/tech/time: `Docs/TODO/AgentC_Mobility_Economy.md`
- Phase 2 rewind/time validation: `Docs/TODO/Phase2_Demo_TODO.md`

When someone is told to "proceed work," they should open their agent file above and update `Docs/Progress.md` at start and end so it remains the single source of truth.

## Open integration items
- Align time/continuity (TimeState, rewind, continuity validation) with PureDOTS expectations; ensure deterministic fleet/colony updates.
- Confirm Burst compatibility for bridge systems once spatial and continuity hookups are live.
- Add colony supply/bottleneck metrics to the registry snapshot and telemetry stream.
- Capture ability UX telemetry hooks (casting latency, cancellation) once the HUD layer is ready.
- Audit scenes/prefabs for bakers/MonoBehaviours that feed registry data; remove service locators.
- Update ScriptableObject catalogs (fleet archetypes, resource definitions, anomaly types) to align with shared registry IDs.
- Validate production scenes against the bootstrap/tagging checklist; document gaps.
- Add focused integration tests for fleet spawning, trade route updates, miracle execution, colony supply; provide mock registry utilities.

## Validation commands
- Edit-mode telemetry suite: `Unity -batchmode -projectPath . -runTests -testResults Logs/EditModeResults.xml -testPlatform editmode`
- Inspect `Logs/EditModeResults.xml` after the run; confirm `Space4XRegistryBridgeSystemTests` passes with spatial continuity counters populated.
