# Space4X RenderKey Truth Source

**Canonical RenderKey Type for Space4X**: `PureDOTS.Rendering.RenderKey`

**Source of Truth**: `Assets/Scripts/Space4x/Presentation/Space4XPresentationLifecycleSystem.cs` (gameplay spawners) and `Assets/Scripts/Space4x/Rendering/Authoring/RenderKeyAuthoring.cs` (prefabs).

**Evidence**: `Space4XRenderCatalogSmokeTest` + `CheckRenderEntitiesSystem` both query `PureDOTS.Rendering.RenderKey` and verify the expected entity counts. The shared PureDOTS resolver/apply stack consumes that exact type, so Space4X must never introduce a local duplication.

**Fields Used**: `RenderKey.ArchetypeId`, `RenderKey.LOD` (PureDOTS layout).

**Contract**: All gameplay and presentation systems must reference the PureDOTS namespace (`PureDOTS.Rendering`). Local structs named `RenderKey`, `RenderVariantKey`, `RenderFlags`, or `RenderPresenterMask` are forbidden because duplicate component definitions trigger Entities source-gen errors (SGJE/SGQC).

## Current Status
- ✅ **CheckRenderEntitiesSystem** – Queries `PureDOTS.Rendering.RenderKey` to assert renderables exist in every world.
- ✅ **RenderSanitySystem / DebugVerifyVisualsSystem** – Validate `RenderKey + MaterialMeshInfo` bindings to catch missing catalog assignments.
- ✅ **Space4XPresentationLifecycleSystem** – Default authoring path that adds `RenderKey`, `RenderSemanticKey`, `RenderVariantKey`, and enables `MeshPresenter`.
- ✅ **Space4X_TestRenderKeySpawnerSystem** – Debug spawner that seeds the PureDOTS stack with canonical components.
- ✅ **RenderKeyAuthoring** – Prefab baker that writes the shared component set so Apply/Resolve systems run without structural churn.

## Usage & Migration Notes
1. **Import `PureDOTS.Rendering`** (no `Space4X.Rendering.RenderKey` aliases remain).
2. **Author semantic first**: gameplay adds `RenderSemanticKey`, `MeshPresenter`, and optionally `RenderKey` for LOD guidance. The resolver writes `RenderVariantKey`.
3. **Presentation overrides** (`RenderTint`, `RenderUvTransform`, `RenderTexSlice`) are per-entity components—no material duplication required.
4. **Theme safety**: Theme 0 in the catalog asset maps every semantic key to a visible primitive + fallback variant so no entity renders invisible during development.

## Diagnostic Pipeline
1. **ResolveRenderVariantSystem** (PureDOTS) change-filters on `RenderSemanticKey`, active theme, overrides, catalog version, and `RenderKey.LOD`. It writes `RenderVariantKey`.
2. **ApplyRenderVariantSystem / SpritePresenterSystem / DebugPresenterSystem** apply meshes/bounds when `RenderVariantKey` or the catalog version changes.
3. **Space4XPresenterToggleSystem / Space4XRenderThemeDebugSystem** flip presenters or themes to confirm runtime swaps without rebakes.

If any entity fails to render:
- Confirm it has `PureDOTS.Rendering.RenderKey`, `RenderSemanticKey`, and an enabled presenter.
- Ensure the catalog blob contains a mapping for that semantic key in Theme 0 (or a fallback entry).
- Verify `RenderCatalogVersion` increments when authoring changes; Apply systems rebind automatically on bump.

## Expected Warnings
Unity may still log `"Ignoring invalid [UpdateAfterAttribute]"` for third-party packages. These messages do **not** indicate a RenderKey mismatch and can be filtered out while testing rendering. Focus on the diagnostics above if visuals disappear.
