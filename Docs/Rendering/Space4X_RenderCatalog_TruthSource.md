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
- **Lookup**: Linear search by ArchetypeId in `ApplyRenderCatalogSystem`

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
Space4XRenderCatalogSingleton (IComponentData with BlobAssetReference)
    +
Space4XRenderMeshArraySingleton (ISharedComponentData with RenderMeshArray)
    ↓ [Runtime: ApplyRenderCatalogSystem]
Entities with RenderKey → MaterialMeshInfo + RenderBounds
```

### Baker Process (RenderCatalogBaker)
- **Input**: `Space4XRenderCatalogDefinition` assigned to `RenderCatalogAuthoring`
- **Output**:
  - `Space4XRenderCatalogSingleton` with blob reference
  - `Space4XRenderMeshArraySingleton` with `RenderMeshArray(meshes[], materials[])`
- **File**: `Assets/Scripts/Space4x/Rendering/Catalog/RenderCatalogAuthoring.cs`

### Runtime Application (ApplyRenderCatalogSystem)
- **Query**: `SystemAPI.Query<RefRO<RenderKey>>().WithNone<MaterialMeshInfo>()`
- **Lookup**: `RenderKey.ArchetypeId` → `RenderCatalogEntry` → mesh/material indices
- **Assignment**:
  - `MaterialMeshInfo.FromRenderMeshArrayIndices(meshIndex, materialIndex)`
  - `RenderBounds { Value = AABB { Center = entry.BoundsCenter, Extents = entry.BoundsExtents } }`
  - `WorldRenderBounds` (same bounds)
  - `AddSharedComponentManaged(entity, renderMeshArray)` for mesh/material access
- **File**: `Assets/Scripts/Space4x/Rendering/Systems/ApplyRenderCatalogSystem.cs`

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
- `Space4XRenderMeshArraySingleton.Value.MeshReferences.Length > 0`
- `Space4XRenderMeshArraySingleton.Value.MaterialReferences.Length > 0`
- `Space4XRenderCatalogSingleton.Blob.IsCreated == true`

### Entity Checks
- Entities with `RenderKey` component have `ArchetypeId` matching catalog entries
- After `ApplyRenderCatalogSystem`: entities have `MaterialMeshInfo` + `RenderBounds`
- `MaterialMeshInfo` indices are valid for the shared `RenderMeshArray`

## Files (Do Not Modify)

- `Assets/Scripts/Space4x/Rendering/Catalog/RenderCatalogTypes.cs`
- `Assets/Scripts/Space4x/Rendering/Catalog/RenderCatalogAuthoring.cs`
- `Assets/Scripts/Space4x/Rendering/Catalog/Space4XRenderCatalogDefinition.cs`
- `Assets/Scripts/Space4x/Rendering/Systems/ApplyRenderCatalogSystem.cs`
- `Assets/Editor/ConfigureRenderCatalog.cs`
- `Assets/Editor/DiagnoseRenderCatalog.cs` (legacy, guarded)

## Notes

- **PresentationSystemGroup**: System runs in presentation phase for proper rendering order
- **No Burst**: Managed system allows RenderMeshArray access without BC1016 errors
- **Shared Component**: `Space4XRenderMeshArraySingleton` is ISharedComponentData with managed RenderMeshArray
- **Fallback Assignment**: `Space4XAssignRenderKeySystem` assigns default ArchetypeIds to entities without RenderKey








