# Pure DOTS Environment Setup

This repository is intended to serve as a reusable DOTS sandbox that future games can build on. The following assets and conventions provide a baseline configuration:

## Core Assets

- **Assets/PureDOTS/Config/PureDotsRuntimeConfig.asset** – central configuration for time step and history recording defaults. Assign this asset to a `PureDotsConfigAuthoring` component in any scene to generate the matching DOTS singletons at bake time.
- **Assets/PureDOTS/Config/PureDotsResourceTypes.asset** – catalog of logical resource type identifiers and display colours. Reference entries when authoring resource nodes or custom systems.
- **Assets/PureDOTS/Prefabs/** – reusable authoring prefabs for the common gameplay entities:
  - `ResourceNode.prefab`
  - `Storehouse.prefab`
  - `Villager.prefab`
  - `VillagerSpawner.prefab`

These prefabs are intentionally minimal and should be duplicated/customised per project.

## Debugging Utilities

- **DotsDebugHUD** (`Assets/Scripts/PureDOTS/Debug/DotsDebugHUD.cs`) – optional MonoBehaviour that displays key DOTS singleton data in-game.
- Resource nodes render a gizmo (selected) showing the gather radius defined on `ResourceSourceAuthoring` for quick spatial checks.

## Template Scene

`Assets/Scenes/PureDotsTemplate.unity` contains a ready-to-bake DOTS setup featuring:

- Global config GameObject (`PureDotsConfigAuthoring`) referencing the runtime config asset.
- Time controls authoring object for keyboard-driven time manipulation.
- Sample resource node, storehouse, villager prefab, and spawner wired together to validate gather/deposit loops.

Use this scene as a reference or starting point when bootstrapping new DOTS experiences.

## Project Settings

- **Script Define Symbols** – `PURE_DOTS_TEMPLATE` is defined across platforms for conditional compilation.
- **Enter Play Mode Options** – domain and scene reloads are disabled by default to improve iteration speed (`ProjectSettings/EditorSettings.asset`).

These defaults favour DOTS workflows and can be adjusted per project if needed.

## Testing & Automation

- Use the **PureDOTS** editor menu (`PureDOTS/Run PlayMode Tests` or `PureDOTS/Run EditMode Tests`) to trigger Unity Test Runner executions.
- In CI, invoke `Unity -runTests -testPlatform playmode -projectPath <path>` (and editmode) to reuse the same configuration.

## Packaging the Template

To export the reusable environment as a zip archive (using the system `zip` utility):

```bash
zip -r PureDOTS_TemplateBundle.zip Assets/PureDOTS Assets/Scenes/PureDotsTemplate.unity Docs/EnvironmentSetup.md
```

Distribute the resulting archive to seed new projects.
