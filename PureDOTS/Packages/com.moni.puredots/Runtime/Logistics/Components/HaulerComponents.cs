using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Logistics.Components
{
    /// <summary>
    /// Marker component identifying an entity as a hauler.
    /// Any entity that can carry cargo should have this.
    /// </summary>
    public struct HaulerTag : IComponentData
    {
    }

    /// <summary>
    /// Hauler capacity constraints.
    /// Defines maximum mass, volume, and container slots.
    /// </summary>
    public struct HaulerCapacity : IComponentData
    {
        public float MaxMass;
        public float MaxVolume;
        public int MaxContainers; // 0 = containers fixed via CargoContainerSlot buffer
    }

    /// <summary>
    /// Cargo container slot buffer element.
    /// Represents a container installed on a hauler.
    /// </summary>
    public struct CargoContainerSlot : IBufferElementData
    {
        public int ContainerDefId;
        public float MassUsed;
        public float VolumeUsed;
    }
}

