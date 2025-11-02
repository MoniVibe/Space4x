# Repository Guidelines

## Project Structure & Module Organization
This Unity DOTS project keeps gameplay code under `Assets/Scripts/Godgame`. Runtime systems live in `Registry/`; authoring components in `Authoring/`; tests in `Tests/`. Scenes and sample SubScenes are in `Assets/Scenes` (start with `SampleScene.unity`). Shared packages and configuration assets sit in `Packages/` and `Assets/Settings/`. Long-running integration work is tracked under `Docs/TODO/`, which should be updated alongside gameplay changes.

## Build, Test, and Development Commands
- `Unity -projectPath "$(pwd)" -batchmode -quit -buildWindows64Player Builds/Godgame.exe` builds a Windows player to `Builds/`.
- `Unity -projectPath "$(pwd)" -batchmode -quit -runTests -testPlatform editmode -testResults Logs/editmode-tests.xml -testFilter GodgameRegistryBridgeSystemTests` runs the registry bridge EditMode suite headlessly.
- `Unity -projectPath "$(pwd)" -batchmode -quit -executeMethod UnityEditor.TestTools.TestRunner.CommandLineTest.RunAllTests` runs every registered test when filters are not needed.
When iterating in the Editor, keep the `Game` and `DOTS Hierarchy` inspectors visible to verify that registries populate as expected.

## Coding Style & Naming Conventions
C# files use 4-space indentation, `namespace Godgame.*`, and PascalCase for types/methods with camelCase locals. Prefer explicit struct layouts for DOTS components and annotate baker intent with short XML documentation (`///`). Runtime code should stay Burst-friendly: avoid managed allocations inside `ISystem.OnUpdate`, cache entity queries, and use `math.*` helpers. Keep authoring MonoBehaviours in `Authoring/`; runtime-only code should be `partial struct` systems. Validate new strings fit existing `FixedString64Bytes` usages.

## Testing Guidelines
Tests live under `Assets/Scripts/Godgame/Tests` and rely on NUnit plus the Unity Entities test runner. Start new fixture names with the system under test (e.g., `GodgameRegistry*Tests`) and group helper methods locally. Add at least one PlayMode or EditMode test per feature that exercises the PureDOTS registries; assert both component data and telemetry buffers. Update `Logs/*.xml` artifacts after new tests to keep CI diagnostics actionable.

## Commit & Pull Request Guidelines
Use imperative, subsystem-prefixed commit subjects such as `Registry: extend villager metrics`. Include a short body listing key systems touched and any follow-up TODOs. Pull requests should link the relevant item in `Docs/TODO`, describe gameplay impact, and attach a screenshot or brief clip when modifying authoring assets. Mention how you validated the change (commands above or editor walkthrough) so reviewers can reproduce quickly.

## Agent Workflow Notes
Document open questions or pipeline friction in `Docs/TODO/Godgame_PureDOTS_Integration_TODO.md` after each session. When adding registries or telemetry, cross-reference shared package APIs so future agents know which PureDOTS touchpoints are stable.
