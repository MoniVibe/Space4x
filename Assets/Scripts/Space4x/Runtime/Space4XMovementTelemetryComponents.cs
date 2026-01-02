using Unity.Entities;

namespace Space4X.Runtime
{
    /// <summary>
    /// Aggregated movement oracle counters between telemetry exports.
    /// </summary>
    public struct Space4XMovementOracleAccumulator : IComponentData
    {
        public uint HeadingFlipCount;
        public uint ApproachFlipCount;
        public uint LastAccelClampTotal;
        public uint SampleTicks;
        public float SampleSeconds;
    }

    /// <summary>
    /// Per-vessel tracking for heading oscillation + approach mode flip detection.
    /// </summary>
    public struct Space4XMovementOracleState : IComponentData
    {
        public sbyte LastHeadingSign;
        public byte HeadingInitialized;
        public MovePlanMode LastPlanMode;
        public byte PlanInitialized;
    }
}
