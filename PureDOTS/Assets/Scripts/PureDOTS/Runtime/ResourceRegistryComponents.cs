using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Components
{
    /// <summary>
    /// Tag for the singleton entity that owns the resource site registry.
    /// </summary>
    public struct ResourceRegistryTag : IComponentData { }

    /// <summary>
    /// A compact record for a resource site, used by consumers for quick lookup.
    /// </summary>
    public struct ResourceRegistryEntry : IBufferElementData
    {
        public Entity Entity;
        public float3 Position;
        public FixedString64Bytes ResourceTypeId;
    }
}




