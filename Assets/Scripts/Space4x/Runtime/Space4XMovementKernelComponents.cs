using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Runtime
{
    /// <summary>
    /// Expected authoritative pose written by the pure movement kernel for the current simulation tick.
    /// Stored on a dedicated singleton entity to avoid inflating the flagship archetype.
    /// </summary>
    public struct PlayerFlagshipKernelPoseStamp : IComponentData
    {
        public Entity FlagshipEntity;
        public uint Tick;
        public float3 Position;
        public quaternion Rotation;
        public byte Active;
    }
}
