PureDOTS Time Integration (Space4x)
===================================

Space4x must consume the shared PureDOTS time/rewind pipeline. Do not introduce game-local time systems.

- Source of truth: `PureDOTS/Packages/com.moni.puredots` seeds TickTimeState, RewindState, TimeControlCommand, time logs, and bootstrap (CoreSingletonBootstrapSystem + PureDotsWorldBootstrap).
- Scenarios/headless: run `-batchmode -nographics -executeMethod PureDOTS.Runtime.Devtools.ScenarioRunnerEntryPoints.RunScenarioFromArgs --scenario <path> [--report <path>]`. Scenario JSON samples live under `PureDOTS/Packages/com.moni.puredots/Runtime/Runtime/Scenarios/Samples`.
- Wire Space4x spawn/bootstrap into the shared ScenarioRunner executor; keep scenario JSONs asset-agnostic and avoid touching time code.
- HUD/debug: reuse DebugDisplayReader + RewindTimelineDebug bound to DebugDisplayData instead of creating a separate HUD.

Reminder: the PureDOTS repo has a `projects/` folder for tooling only. This directory (`C:\Users\shonh\OneDrive\Documents\claudeprojects\Unity\Space4x`) is the canonical Space4x worktree.
