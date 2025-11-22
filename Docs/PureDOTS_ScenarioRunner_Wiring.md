PureDOTS ScenarioRunner Wiring - Space4x
========================================

Objective: wire Space4x demo spawns into the shared PureDOTS ScenarioRunner executor (headless + editor).

Next steps for agents:
1) Locate the minimal mining/hauling demo bootstrap. Add a thin adapter (in Space4x) that, when a ScenarioRunner scenario is active, seeds entities/registries per scenario JSON counts (see `PureDOTS/Packages/com.moni.puredots/Runtime/Runtime/Scenarios/Samples/space4x_smoke.json`).
2) Use existing PureDOTS registries/spawner systems; do not introduce time/rewind logic. ScenarioRunner already drives TickTimeState/RewindState with time-control commands.
3) Provide a CLI hook that forwards `--scenario`/`--report` to `PureDOTS.Runtime.Devtools.ScenarioRunnerEntryPoints.RunScenarioFromArgs`.
4) Add an editor button/menu item to trigger the same for local smoke tests.
5) Document the Space4x spawn mapping (registry IDs and archetypes) here after wiring.

Reminder: keep Space4x changes limited to spawning/adapters; ScenarioRunner and time live in the PureDOTS package.
Note: `Space4xScenarioSpawnLoggerSystem` currently logs scenario entity counts from ScenarioRunner. Replace it with real spawn/bootstrap wiring.
