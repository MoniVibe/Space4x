using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Mining
{
    /// <summary>
    /// Tag component identifying a mining vessel entity.
    /// </summary>
    public struct MiningVesselTag : IComponentData { }

    /// <summary>
    /// Frame definition for a mining vessel (stub for demo).
    /// </summary>
    public struct MiningVesselFrameDef : IComponentData
    {
        public float MaxCargo;
        public float MiningRate; // Units per second
    }

    /// <summary>
    /// Reference to craft frame definition.
    /// </summary>
    public struct CraftFrameRef : IComponentData
    {
        public int FrameId; // Reference to frame definition catalog
    }

    /// <summary>
    /// Mining job state for a mining vessel.
    /// </summary>
    public struct MiningJob : IComponentData
    {
        public MiningPhase Phase;
        public Entity TargetAsteroid;
        public Entity CarrierEntity;
        public float CargoAmount; // Current cargo
        public float3 TargetPosition;
        public uint LastStateChangeTick;
    }

    /// <summary>
    /// Phases of the mining job loop.
    /// </summary>
    public enum MiningPhase : byte
    {
        Idle = 0,
        FlyToAsteroid = 1,
        Mining = 2,
        ReturnToCarrier = 3,
        Unloading = 4
    }
}

