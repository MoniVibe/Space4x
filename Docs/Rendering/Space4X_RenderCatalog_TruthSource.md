# Space4X Render Catalog Truth Source

**Last Updated**: 2025-12-10
**Status**: Active pipeline - keep in sync with PureDOTS

## Canonical Types (Truth)

All catalog authoring and runtime types live in PureDOTS:

- `puredots/Packages/com.moni.puredots/Runtime/Rendering/RenderPresentationCatalogDefinition.cs`
  - `RenderPresentationCatalogDefinition` (ScriptableObject with `Variants` + `Themes`)
- `puredots/Packages/com.moni.puredots/Runtime/Rendering/RenderPresentationCatalogAuthoring.cs`
  - `RenderPresentationCatalogAuthoring` (MonoBehaviour for baking)
- `puredots/Packages/com.moni.puredots/Runtime/Authoring/Rendering/RenderPresentationCatalogBaker.cs`
  - `RenderPresentationCatalogBaker` (creates `RenderPresentationCatalog` + `RenderMeshArray`)
- `puredots/Packages/com.moni.puredots/Runtime/Rendering/RenderPresentationContract.cs`
  - `RenderPresentationCatalog` + `RenderPresentationCatalogBlob`

```csharp
public struct RenderPresentationCatalog : IComponentData
{
    public BlobAssetReference<RenderPresentationCatalogBlob> Blob;
    public Entity RenderMeshArrayEntity;
}
```

## Space4X Catalog Assets

- `space4x/Assets/Data/Space4XRenderCatalog_v2.asset` (canonical, referenced by scenes)
- `space4x/Assets/Data/Space4XRenderCatalog.asset` (legacy, not referenced by smoke/bootstrap scenes)

## Data Flow Pipeline

```
Space4XRenderCatalog_v2.asset (RenderPresentationCatalogDefinition)
    -> RenderPresentationCatalogAuthoring (scene/subscene)
    -> RenderPresentationCatalogBaker
    -> RenderPresentationCatalog + RenderMeshArray entity
    -> PureDOTS.ResolveRenderVariantSystem + ApplyRenderVariantSystem
    -> Entities with RenderSemanticKey / RenderKey + presenters
```

Fallback path when no baked catalog exists:

```
Space4XRenderCatalog_v2.asset
    -> RenderPresentationCatalogRuntimeBootstrap
    -> RenderPresentationCatalog + RenderMeshArray entity
```

## Scene Wiring

- `space4x/Assets/TRI_Space4X_Smoke.unity`
  - `RenderPresentationCatalogAuthoring` + `RenderPresentationCatalogRuntimeBootstrap`
  - Both reference `Space4XRenderCatalog_v2.asset`
- `space4x/Assets/Scenes/Space4X_Bootstrap.unity`
  - `RenderPresentationCatalogAuthoring` referencing `Space4XRenderCatalog_v2.asset`

## How to Add a Render Entry (Space4X)

1. Open `space4x/Assets/Data/Space4XRenderCatalog_v2.asset`.
2. Add a new item under `Variants` with Mesh/Material/Bounds/PresenterMask.
3. Update Theme 0 (and any active themes) to map the `SemanticKey` to the new variant indices (`Lod0Variant`, `Lod1Variant`, `Lod2Variant`).
4. Ensure gameplay writes `RenderSemanticKey` using `Space4XRenderKeys` values (`space4x/Assets/Scripts/Space4x/Presentation/Space4XRenderKeys.cs`).
5. The baker/runtime bootstrap bumps `RenderCatalogVersion` automatically; no manual versioning needed.

## Archetype / Semantic Key Map

| Key | Value | Notes |
| --- | --- | --- |
| Carrier | 200 | `Space4XRenderKeys.Carrier` |
| Miner | 210 | `Space4XRenderKeys.Miner` |
| Asteroid | 220 | `Space4XRenderKeys.Asteroid` |
| Projectile | 230 | `Space4XRenderKeys.Projectile` |
| FleetImpostor | 240 | `Space4XRenderKeys.FleetImpostor` |
| Individual | 250 | `Space4XRenderKeys.Individual` |
| StrikeCraft | 260 | `Space4XRenderKeys.StrikeCraft` |
| ResourcePickup | 270 | `Space4XRenderKeys.ResourcePickup` |
| GhostTether | 280 | `Space4XRenderKeys.GhostTether` |

## Runtime Verification

- `RenderPresentationCatalog.Blob.IsCreated == true`
- `RenderPresentationCatalog.RenderMeshArrayEntity` has `RenderMeshArray` shared component
- Entities with `RenderSemanticKey` + enabled presenters resolve to `MaterialMeshInfo` + `RenderBounds`



