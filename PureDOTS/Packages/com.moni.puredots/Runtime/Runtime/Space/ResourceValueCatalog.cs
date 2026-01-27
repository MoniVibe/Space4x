using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Space
{
    [InternalBufferCapacity(32)]
    public struct ResourceValueEntry : IBufferElementData
    {
        public FixedString64Bytes ResourceTypeId;
        public float BaseValue;
    }

    public struct ResourceValueCatalogTag : IComponentData {}
}
