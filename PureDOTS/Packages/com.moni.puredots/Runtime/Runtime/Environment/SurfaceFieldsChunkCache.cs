using Unity.Entities;

namespace PureDOTS.Environment
{
    public struct SurfaceFieldsChunkRefCache : IComponentData
    {
        public int Count;
    }

    public struct SurfaceFieldsChunkRefCacheDirty : IComponentData
    {
    }
}
