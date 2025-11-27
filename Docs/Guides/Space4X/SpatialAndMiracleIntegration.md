# Space4X Spatial & Miracle Integration Guide

This guide captures the authoring and testing steps required to keep Space4X scenes aligned with the PureDOTS spatial services and the miracle registry pipeline.

## 1. Scene Bootstrap Requirements

- Attach `PureDotsConfigAuthoring` to a bootstrap GameObject in every Space4X scene.
  - Assign the desired `PureDotsRuntimeConfig` asset so time, rewind, and resource catalogs are baked.
- Attach `SpatialPartitionAuthoring` to the same bootstrap object (or a sibling).
  - Choose the appropriate `SpatialPartitionProfile` asset for the scene’s scale (e.g. sector vs. orbital).
  - Designers can swap profiles per scene; the baker will patch `SpatialGridConfig`/`SpatialGridState` singletons at conversion.
- **Space4X defaults:** the sample scenes now use `Assets/Space4X/Config/PureDotsRuntimeConfig.asset` together with `Assets/Space4X/Config/DefaultSpatialPartitionProfile.asset`. Reuse those assets unless a scene explicitly calls for different bounds.
- Keep these bootstrap GameObjects inside the SubScene that owns gameplay DOTS data to guarantee deterministic conversion.

## 2. Tagging Gameplay Bakers

- Every baker that produces runtime entities for colonies, fleets, logistics routes, anomalies, or miracles **must add** `SpatialIndexedTag`.
  - This includes `Space4XSampleRegistryAuthoring.Baker` and any custom feature bakers.
  - Without the tag, `SpatialGridResidency` will never be attached and the registry bridge cannot compute `CellId`/continuity metrics.
- When feasible, also add a `SpatialGridResidency` component during baking to provide deterministic seeds for grid population (leave the system to update positions later).

## 3. Using `Space4XMiracleAuthoring`

- Drop `Space4XMiracleAuthoring` onto scene objects or prefabs that should emit miracle definitions.
  - Configure the **Miracle Type**, casting mode, energy costs, and cooldown defaults in the inspector.
  - The baker emits `MiracleDefinition`, `MiracleRuntimeState`, and optional targeting data compatible with the shared miracle registry.
- Pair miracle authoring with the spatial bootstrap above so the registry bridge can resolve spatial metrics for active miracles.
- For scripted spawners, instantiate prefabs that already carry `Space4XMiracleAuthoring` to avoid duplicating data wiring.
- The demo scenes now include a `Space4XMiracleRig` authoring object that seeds both an instant strike and a sustained shield miracle so registry/telemetry coverage is available out of the box.

## 4. Telemetry & Test Verification

- After wiring a scene, run the edit-mode telemetry test suite:
  - `Unity -batchmode -projectPath . -runTests -testResults Logs/EditModeResults.xml -testPlatform editmode`
- Inspect the resulting `Logs/EditModeResults.xml` for failures and verify that `Space4XRegistryBridgeSystemTests` reports spatial continuity counters.
- While iterating in the Editor, open the debug HUD to confirm the new metrics:
  - Colonies and fleets show `CellId`-aware counts.
  - Miracle energy and cooldown totals update in real time.

## 5. Checklist for New Scenes

1. Create a bootstrap GameObject within the SubScene.
2. Add `PureDotsConfigAuthoring` and link the runtime config asset.
3. Add `SpatialPartitionAuthoring` and choose the correct spatial profile.
4. Ensure every Space4X authoring component/baker adds `SpatialIndexedTag`.
5. Place `Space4XMiracleAuthoring` on any prefabs that should register miracles.
6. Convert the SubScene and confirm registry buffers populate via the playmode tests above.

> Tip: keep this guide handy when onboarding new scenes so designers continue to deliver spatial-aware data to the shared registries.



















