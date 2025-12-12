using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Godgame.Rendering
{
    /// <summary>
    /// Mapping from RenderKey archetypes to Entities Graphics resources.
    /// </summary>
    public struct GodgameRenderMeshCatalogEntry
    {
        public int ArchetypeId;
        public int MaterialIndex;
        public int MeshIndex;
        public ushort SubMesh;
        public float3 BoundsCenter;
        public float3 BoundsExtents;
    }

    public struct GodgameRenderMeshCatalog
    {
        public BlobArray<GodgameRenderMeshCatalogEntry> Entries;
    }

    public struct GodgameRenderCatalogSingleton : IComponentData
    {
        public BlobAssetReference<GodgameRenderMeshCatalog> Catalog;
    }
}
