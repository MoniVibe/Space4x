# PureDOTS Semantic → Variant → Presenter Pipeline

Shared, game-agnostic rendering infrastructure for DOTS 1.4. Gameplay code stays data-driven and only ever emits **semantic keys**; PureDOTS resolves those through themed catalogs into concrete presenter data (Meshes, Materials, bounds, overrides, etc.).

## Core Components

- `RenderSemanticKey { ushort Value; }` – stable, game-owned identifier (villager miner, storehouse, etc.).
- `RenderKey { ushort ArchetypeId; byte LOD; }` – optional gameplay-side authoring that feeds LOD guidance into the resolver.
- `ActiveRenderTheme { ushort ThemeId; }` + `RenderThemeOverride { ushort Value; }` – global or per-entity style selection.
- `RenderVariantKey { int Value; }` – concrete variant index resolved from the active theme (change-filtered).
- Presenters (enableable): `MeshPresenter`, `SpritePresenter`, `DebugPresenter` – toggle presentation paths without structural changes.
- Overrides: `RenderTint`, `RenderTexSlice`, `RenderUvTransform` – instancing-friendly per-entity material data.

## Catalogs & Authoring

- Author catalog assets with `RenderPresentationCatalogDefinition` (or game-specific subclasses). They declare **variants** (mesh/material/bounds/presenter mask/submesh) and **themes** (semantic key × LOD index → variant id mappings).
- Bake or runtime-bootstrap catalogs via `RenderPresentationCatalogAuthoring` / `RenderPresentationCatalogRuntimeBootstrap`. Both produce:
  - `RenderPresentationCatalogBlob` (variants + themed indices)
  - `RenderMeshArray` shared component
  - `RenderCatalogVersion` singleton (incremented whenever the catalog rebuilds)

## Systems

- `ResolveRenderVariantSystem`: change-filtered, Burst job that reacts to semantic/theme/override/LOD changes (or catalog version bumps) and writes `RenderVariantKey`.
- `ApplyRenderVariantSystem`: change-filtered Mesh presenter that maps variants to `MaterialMeshInfo`/bounds and respects catalog version invalidations. Adds missing presentation components once and avoids per-frame structural churn.
- `SpritePresenterSystem` & `DebugPresenterSystem`: map variants onto alternative presenters so games can flip to impostors or debugging glyphs without structural changes.
- Guard rails: `RenderPresentationValidationSystem` surfaces missing semantic/presenter components in dev builds; `RenderSanitySystem` logs world-level counts.

## Usage Cheat Sheet

1. **Define semantic ids** (`GodgameRenderKeys`, `Space4XRenderIds`, …) and author gameplay code to add `RenderSemanticKey`, `RenderKey` (for LOD), `MeshPresenter`, and `RenderFlags`.
2. **Create catalog asset**:
   - Populate `Variants` (meshes/materials/bounds/presenter mask).
   - Define one or more `Themes`, mapping semantic keys to variant indices (duplicate entries per theme to restyle on the fly).
   - Assign fallback mesh/material in case mappings are missing.
3. **Drop catalog authoring + runtime bootstrap** in your scene/subscene so conversion or headless bootstrapping seeds the singleton and mesh array entity.
4. **Set ActiveRenderTheme** singleton (and optional per-entity overrides) from gameplay/UI. Toggling the theme automatically re-resolves every entity through change filters – no full-world loops required.
5. **Use overrides** sparingly for instancing-friendly per-entity data: e.g., add `RenderTint` per villager role, `RenderTexSlice` for atlas selection, etc.
6. **Switch presenters** by enabling/disabling `MeshPresenter` / `SpritePresenter` / `DebugPresenter` on an entity instead of adding/removing components (e.g., LOD -> sprite impostor, dev/debug glyphs, etc.).

## Default Presentations & Theme 0

- **Theme 0 is the default look.** The first theme in your catalog should map every semantic key to a concrete variant so spawning entities always have a deterministic appearance (no invisible/default cases).
- **Only author RenderSemanticKey + MeshPresenter.** Spawners simply add those components (plus `RenderFlags`). `ResolveRenderVariantSystem` automatically picks Theme 0’s variant and `ApplyRenderVariantSystem` writes `MaterialMeshInfo`/`RenderBounds` on first resolve or when data changes.
- **Add primitive variants for debugging.** Cube, sphere, capsule, cylinder, and a tiny single-triangle mesh make it trivial to visualize different semantic buckets before real art is available. Assign them to Theme 0 for “it just renders” behavior.
- **Fallback variant guards gaps.** Anything unmapped falls back to catalog slot 0 (supply an obvious magenta cube mesh/material pair so missing mappings are obvious during testing).
- **Runtime swapping stays trivial.** Change the whole world’s look via `ActiveRenderTheme.ThemeId`, override a single entity via `RenderThemeOverride.Value`, or force a unique look via `RenderVariantKey` without touching spawners.
