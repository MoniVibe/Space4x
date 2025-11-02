# Foundation Guidelines

When extending this template for future projects:

1. **Keep configuration data asset-driven.** Prefer updating `PureDotsRuntimeConfig.asset` and `ResourceTypeCatalog.asset` over hardcoding values in scenes or systems. If new domains require configuration, create additional ScriptableObjects under `Assets/PureDOTS/Config` and bake them through authoring components.
2. **Use reusable prefabs as starting points.** Duplicate prefabs in `Assets/PureDOTS/Prefabs` instead of editing them directly; keep the originals as pristine references.
3. **Avoid scene-specific logic in core systems.** Systems within `PureDOTS.Systems` should only rely on components/buffers, not scene names or MonoBehaviours. Authoring scripts should remain thin and conversion-focused.
4. **Validate authoring data.** Follow the pattern established in `ResourceSourceAuthoring` and `PureDotsConfigAssets` by adding `OnValidate` hooks to catch misconfigurations early.
5. **Extend tests alongside features.** Add playmode or editmode tests to `Assets/Tests/` whenever new deterministic systems are introduced. Headless tests allow CI pipelines to catch regressions without loading sample scenes.
6. **Document new tooling.** Update `Docs/EnvironmentSetup.md`, `Docs/SystemOrdering/SystemSchedule.md`, and `Docs/TestingGuidelines.md` whenever new workflows or scripts are added so future teams remain aligned.
