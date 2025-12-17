# Space4X Render Catalog Truth Source

**Last Updated**: 2025-12-10
**Status**: Working pipeline - do not modify unless explicitly requested

## Canonical Types (Truth)

### RenderCatalogBlob
```csharp
public struct RenderCatalogBlob
{
    public BlobArray<RenderCatalogEntry> Entries;
}
```
- **File**: `Assets/Scripts/Space4x/Rendering/Catalog/RenderCatalogTypes.cs`
- **Purpose**: Immutable blob data containing catalog entries
- **Lifetime**: Persistent blob asset created by baker

### RenderCatalogEntry
```csharp
public struct RenderCatalogEntry
{
    public ushort ArchetypeId;
    public int MeshIndex;
    public int MaterialIndex;
    public float3 BoundsCenter;
    public float3 BoundsExtents;
}
```
- **File**: `Assets/Scripts/Space4x/Rendering/Catalog/RenderCatalogTypes.cs`
- **Purpose**: Maps visual archetype IDs to mesh/material/bounds data
- **Lookup**: Consumed by `PureDOTS.Rendering.ResolveRenderVariantSystem`, which scans the blob entries when resolving `RenderVariantKey`

### Space4XRenderCatalogDefinition.Entry (Authoring)
```csharp
[Serializable]
public struct Entry
{
    public ushort ArchetypeId;
    public Mesh Mesh;
    public Material Material;
    public Vector3 BoundsCenter;
    public Vector3 BoundsExtents;
}
```
- **File**: `Assets/Scripts/Space4x/Rendering/Catalog/Space4XRenderCatalogDefinition.cs`
- **Purpose**: ScriptableObject-based authoring data
- **Conversion**: Baker converts to blob entries + RenderMeshArray

## Data Flow Pipeline

```
Space4XRenderCatalogDefinition (ScriptableObject)
    ↓ [Baker: RenderCatalogBaker]
RenderPresentationCatalog (IComponentData with BlobAssetReference)
    +
RenderMeshArray shared component
    ↓ [Runtime: PureDOTS.ResolveRenderVariantSystem + ApplyRenderVariantSystem]
Entities with RenderSemanticKey + presenters → MaterialMeshInfo + RenderBounds
```

### Baker Process (RenderCatalogBaker)
- **Input**: `Space4XRenderCatalogDefinition` assigned to `RenderCatalogAuthoring`
- **Output**:
  - `RenderPresentationCatalog` with themed+variant blob reference
  - `RenderMeshArray` shared component with meshes/materials
- **File**: `Assets/Scripts/Space4x/Rendering/Catalog/RenderCatalogAuthoring.cs`

### Runtime Application (PureDOTS Resolve/Apply)
- **Resolve**: `PureDOTS.Rendering.ResolveRenderVariantSystem` change-filters on `RenderSemanticKey`, `RenderKey.LOD`, themes, and overrides to write `RenderVariantKey`.
- **Apply**: `PureDOTS.Rendering.ApplyRenderVariantSystem` (MeshPresenter) + `SpritePresenterSystem`/`DebugPresenterSystem` bind `MaterialMeshInfo`, `RenderBounds`, and `WorldRenderBounds` whenever `RenderVariantKey` changes or the catalog version increments.
- **Shared Data**: Entities store `RenderSemanticKey`, optional `RenderKey` (for LOD), `RenderVariantKey`, and enableable presenter components (`MeshPresenter`, `SpritePresenter`, `DebugPresenter`).

## Archetype ID Mapping

| Entity Type | ArchetypeId | Mesh Shape | Notes |
|-------------|-------------|------------|-------|
| Carrier | 200 | Capsule | Space4XRenderKeys.Carrier |
| Miner | 210 | Cylinder | Space4XRenderKeys.Miner |
| Asteroid | 220 | Sphere | Space4XRenderKeys.Asteroid |
| Projectile | 230 | - | Space4XRenderKeys.Projectile |
| FleetImpostor | 240 | - | Space4XRenderKeys.FleetImpostor |

## Editor Setup

### ConfigureRenderCatalog.cs
- **Menu**: `Space4X → Configure Render Catalog`
- **Creates**: `Assets/Space4XRenderCatalog.asset` (Space4XRenderCatalogDefinition)
- **Populates**: 3 entries with primitive meshes (Capsule/Cylinder/Sphere)
- **Assigns**: CatalogDefinition to RenderCatalogAuthoring GameObject
- **File**: `Assets/Editor/ConfigureRenderCatalog.cs`

### Scene Requirements
- **RenderCatalog GameObject** with `RenderCatalogAuthoring` component
- **CatalogDefinition** field assigned to `Space4XRenderCatalogDefinition.asset`
- **SubScene conversion** or scene baking to trigger baker

## Runtime Verification

### System Checks
- `RenderPresentationCatalog.Blob.IsCreated == true`
- `RenderPresentationCatalog.RenderMeshArrayEntity` has `RenderMeshArray` shared component with mesh/material references

### Entity Checks
- Entities with `RenderSemanticKey` map to valid theme entries (see `Space4XRenderKeys`)
- After the PureDOTS apply systems run: entities with enabled presenters have `MaterialMeshInfo` + `RenderBounds`
- `MaterialMeshInfo` indices are valid for the shared `RenderMeshArray`

## Files (Do Not Modify)

- `Assets/Scripts/Space4x/Rendering/Catalog/RenderCatalogAuthoring.cs`
- `Assets/Scripts/Space4x/Rendering/Catalog/Space4XRenderCatalogDefinition.cs`
- `Assets/Editor/ConfigureRenderCatalog.cs`
- `Assets/Editor/DiagnoseRenderCatalog.cs` (legacy, guarded)

## Notes

- **PresentationSystemGroup**: PureDOTS presenter systems run in the presentation phase before Entities Graphics.
- **Shared Component**: `RenderMeshArray` lives on the entity referenced by `RenderPresentationCatalog.RenderMeshArrayEntity`.





