using Unity.Entities;

namespace PureDOTS.Runtime.Navigation
{
    /// <summary>
    /// Navigation path component - tracks a planned path for an entity or group.
    /// </summary>
    public struct NavPath : IComponentData
    {
        /// <summary>
        /// Entity that owns this path (the entity or group planning to move).
        /// </summary>
        public Entity Owner;

        /// <summary>
        /// Current segment index in the path (0 = first segment).
        /// </summary>
        public int CurrentSegmentIndex;

        /// <summary>
        /// Total estimated cost for the entire path.
        /// </summary>
        public float TotalCost;

        /// <summary>
        /// Tick when path was computed.
        /// </summary>
        public uint PathComputedTick;

        /// <summary>
        /// Whether path is still valid (may become invalid if world state changes).
        /// </summary>
        public byte IsValid;
    }

    /// <summary>
    /// Buffer of navigation segments making up a complete path.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct NavPathSegment : IBufferElementData
    {
        /// <summary>
        /// The navigation segment.
        /// </summary>
        public NavSegment Segment;
    }
}






















