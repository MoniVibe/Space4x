# Repository Guidelines

## Project Structure & Module Organization
This Unity project keeps gameplay code under `Assets/Scripts/Space4x`. Authoring components live in `Authoring/`, runtime systems in `Registry/`, and editor-safe tests in `Tests/`. Scenes sit in `Assets/Scenes` (`SampleScene.unity` is the entry point). Render pipeline assets and profiles reside in `Assets/Settings`, while reference material belongs in `Docs/`. Keep this repo Space4x-only; PureDOTS and Godgame live as sibling projects in `C:/Users/shonh/OneDrive/Documents/claudeprojects/Unity` (see `TRI_PROJECT_BRIEFING.md`) and should not be nested here.

## Build, Test, and Development Commands
- `Unity -projectPath .` opens the project in the Unity Editor; load `SampleScene.unity` for play-mode checks.
- `Unity -batchmode -projectPath . -quit -buildWindows64Player Build/Space4x.exe` creates a Windows desktop build; change the output path per platform.
- `Unity -batchmode -projectPath . -runTests -testResults Logs/EditModeResults.xml -testPlatform editmode` runs automated tests headlessly and writes NUnit XML results.

## Coding Style & Naming Conventions
Follow standard C# conventions: four-space indentation, PascalCase for public types and methods, camelCase for locals and private fields (prefix `_` only when needed for clarity). Components and systems should mirror their ECS roles, e.g., `Space4XRegistryBridgeSystem`. Group related source inside existing assembly definitions (`Space4x.Gameplay.asmdef`) to keep dependencies tight, and format C# files via the Unity inspector before submitting.

## Testing Guidelines
Tests rely on the Unity Test Framework with NUnit attributes (`[Test]`, `[SetUp]`, `[TearDown]`) as seen in `Assets/Scripts/Space4x/Tests/Space4XRegistryBridgeSystemTests.cs`. Name test classes after the system under test and prefer descriptive methods such as `BridgeRegistersColoniesAndFleetsAndEmitsTelemetry`. Build deterministic ECS worlds by creating and disposing `World` instances in setup/teardown, and extend coverage whenever you add registry components or telemetry metrics.

## Commit & Pull Request Guidelines
Available commits show short, imperative subjects (e.g., `Add registry bridge telemetry hook`). Continue that style, optionally prefixing a scope such as `registry:` when it clarifies intent. Pull requests should summarize behavior changes, list validation steps (editor playtest, batchmode build, test run), and reference tracked issues. Add screenshots when scene updates affect visuals, and keep PRs focused—split mechanical refactors from gameplay changes.

## Scene & Asset Management
Create new scenes or prefabs inside feature-specific subfolders under `Assets/Scenes` or `Assets/Prefabs`, and update assembly definitions if scripts accompany the assets. When modifying render pipeline assets in `Assets/Settings`, clone the existing profile first to avoid breaking shared configurations, and document notable changes in `Docs/` for future tuning.
