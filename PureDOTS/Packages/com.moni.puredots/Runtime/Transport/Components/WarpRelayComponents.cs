using Unity.Entities;

namespace PureDOTS.Runtime.Transport.Components
{
    /// <summary>
    /// Warp relay node status.
    /// </summary>
    public enum WarpRelayNodeStatus : byte
    {
        Online = 0,
        Damaged = 1,
        Offline = 2,
        Captured = 3,
        Destroyed = 4
    }

    /// <summary>
    /// Marker component identifying an entity as a warp relay node.
    /// </summary>
    public struct WarpRelayNodeTag : IComponentData
    {
    }

    /// <summary>
    /// Warp relay node component.
    /// Represents a node in the hyperway network.
    /// </summary>
    public struct WarpRelayNode : IComponentData
    {
        public int NodeId; // unique within hyperway network
        public int SystemId; // star system / region / island cluster
        public Entity Platform; // underlying platform entity (ship/station/ferry)
        public int OwnerFactionId;
        public byte NodeGrade; // 0 = local ferry, 1 = regional, 2+ = interstellar
        public WarpRelayNodeStatus Status;
    }

    /// <summary>
    /// Warp relay drive bank component.
    /// Aggregated warp drive capacity for carrying other entities.
    /// </summary>
    public struct WarpRelayDriveBank : IComponentData
    {
        public float TotalWarpPower; // aggregate from modules
        public float MaxPayloadMass;
        public float MaxPayloadVolume;
        public float FuelPerJump;
    }

    /// <summary>
    /// Warp relay docking buffer element.
    /// Tracks entities docked/attached to the relay for transport.
    /// </summary>
    public struct WarpRelayDocking : IBufferElementData
    {
        public Entity DockedEntity;
        public float Mass;
        public float Volume;
    }
}

