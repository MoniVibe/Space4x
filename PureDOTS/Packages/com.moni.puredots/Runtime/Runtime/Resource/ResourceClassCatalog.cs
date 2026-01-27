using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Resource
{
    public enum ResourceClass : byte
    {
        Unknown = 0,
        Extracted = 1,
        Produced = 2,
        Finished = 3
    }

    [InternalBufferCapacity(32)]
    public struct ResourceClassEntry : IBufferElementData
    {
        public FixedString64Bytes ResourceTypeId;
        public ResourceClass Class;
    }

    public struct ResourceClassCatalogTag : IComponentData
    {
    }
}
