using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Rewind
{
    /// <summary>
    /// Header tracking the head tick and capacity for a snapshot ring buffer.
    /// Stored on entities that own snapshot buffers.
    /// </summary>
    public struct SnapshotHeader : IComponentData
    {
        public int HeadTick;   // newest snapshot tick index
        public int Capacity;   // ring buffer length in ticks
    }

    /// <summary>
    /// Transform snapshot element (position/rotation/scale) keyed by tick.
    /// </summary>
    public struct TransformSnapshot : IBufferElementData
    {
        public int Tick;
        public float3 Position;
        public quaternion Rotation;
        public float Scale;
    }
}



