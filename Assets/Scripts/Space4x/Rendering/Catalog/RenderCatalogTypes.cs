using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Rendering.Catalog
{
    public struct RenderCatalogBlob
    {
        public BlobArray<RenderCatalogEntry> Entries;
    }

    public struct RenderCatalogEntry
    {
        public ushort ArchetypeId;
        public int MeshIndex;
        public int MaterialIndex;
        public float3 BoundsCenter;
        public float3 BoundsExtents;
    }

    /// <summary>
    /// Singleton holding the baked catalog blob and the RenderMeshArray entity reference.
    /// </summary>
    public struct RenderCatalogSingleton : IComponentData
    {
        public BlobAssetReference<RenderCatalogBlob> Blob;
        public Entity RenderMeshArrayEntity;
    }
}