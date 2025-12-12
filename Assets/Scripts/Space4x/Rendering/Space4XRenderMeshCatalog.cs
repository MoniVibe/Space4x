using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Mathematics;
using Unity.Rendering;

namespace Space4X.Rendering
{
    // One mapping row: RenderKey.ArchetypeId -> indices into RenderMeshArray
    public struct Space4XRenderMeshCatalogEntry
    {
        public int ArchetypeId;
        public int MaterialIndex;
        public int MeshIndex;
        public ushort SubMesh;
        public float3 BoundsCenter;
        public float3 BoundsExtents;
    }

    // Blob root
    public struct Space4XRenderMeshCatalog
    {
        public BlobArray<Space4XRenderMeshCatalogEntry> Entries;
    }

    // Catalog singleton (blob only)
    public struct Space4XRenderCatalogSingleton : IComponentData
    {
        public BlobAssetReference<Space4XRenderMeshCatalog> Catalog;
    }
}

