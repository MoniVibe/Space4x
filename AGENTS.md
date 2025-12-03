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

## Error Prevention (Required Reading)
Before adding new ECS components, systems, or modifying existing code, read `Docs/Guides/Unity_DOTS_Common_Errors.md`. This guide covers:
- **CS0101 Duplicate Types:** Always grep before creating new types
- **CS0104 Ambiguous References:** Use fully-qualified names for `Random`, `PresentationSystemGroup`
- **CS8377 ECS Blittability:** Components must use only blittable types (no strings, no nullable)
- **CS0315 Interface Mismatches:** Match `ComponentLookup` vs `BufferLookup` to interface type
- **CS0246 Missing Types:** Common `using` statements for Space4X namespaces
- **Burst Errors:** No managed types in Burst code; use `FixedString` instead of `string`

## Cross-Project Features
When implementing features that should work across both Space4x and Godgame, follow the **recipe templates** documented in `PureDOTS/Docs/Recipes/`. See the catalog (`PureDOTS/Docs/Recipes/README.md`) for available recipe types and worked examples. This ensures:
- Shared contracts are defined in `PureDOTS/Docs/Contracts.md` (if needed)
- Generic spine lives in PureDOTS
- Game-specific adapters stay in this project under `Assets/Scripts/Space4x/Adapters/`
- Start from the recipe template, then specialize for your feature type.