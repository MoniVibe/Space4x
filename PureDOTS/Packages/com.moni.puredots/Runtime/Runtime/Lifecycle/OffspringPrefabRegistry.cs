using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Lifecycle
{
    /// <summary>
    /// Tag component for entities that host offspring prefab entries.
    /// Game-side systems should create a singleton entity with this component and populate the buffer.
    /// </summary>
    public struct OffspringPrefabRegistry : IComponentData
    {
    }

    /// <summary>
    /// Maps offspring type identifiers to prefab entities that should be instantiated for newborns.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct OffspringPrefabEntry : IBufferElementData
    {
        public FixedString64Bytes OffspringTypeId;
        public Entity Prefab;
    }
}

