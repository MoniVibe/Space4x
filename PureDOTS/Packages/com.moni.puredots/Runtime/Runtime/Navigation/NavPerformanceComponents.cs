using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Navigation
{
    /// <summary>
    /// Performance budget configuration singleton for navigation systems.
    /// Defines hard caps on expensive operations per tick.
    /// </summary>
    public struct NavPerformanceBudget : IComponentData
    {
        /// <summary>
        /// Maximum local pathfinding queries (WARM path) per tick.
        /// Default: 50 queries/tick.
        /// </summary>
        public int MaxLocalPathQueriesPerTick;

        /// <summary>
        /// Maximum strategic route planning queries (COLD path) per tick.
        /// Default: 5 queries/tick.
        /// </summary>
        public int MaxStrategicRoutePlansPerTick;

        /// <summary>
        /// Maximum flow field builds per tick.
        /// Default: 10 builds/tick.
        /// </summary>
        public int MaxFlowFieldBuildsPerTick;

        /// <summary>
        /// Warning threshold for queue size (log warning if exceeded).
        /// Default: 100 queued requests.
        /// </summary>
        public int QueueSizeWarningThreshold;

        /// <summary>
        /// Creates default budget configuration.
        /// </summary>
        public static NavPerformanceBudget CreateDefaults()
        {
            return new NavPerformanceBudget
            {
                MaxLocalPathQueriesPerTick = 50,
                MaxStrategicRoutePlansPerTick = 5,
                MaxFlowFieldBuildsPerTick = 10,
                QueueSizeWarningThreshold = 100
            };
        }
    }

    /// <summary>
    /// Performance counters singleton for navigation systems.
    /// Tracks actual usage per tick for monitoring and enforcement.
    /// </summary>
    public struct NavPerformanceCounters : IComponentData
    {
        /// <summary>
        /// Number of local path queries processed this tick.
        /// </summary>
        public int LocalPathQueriesThisTick;

        /// <summary>
        /// Number of strategic route plans processed this tick.
        /// </summary>
        public int StrategicRouteQueriesThisTick;

        /// <summary>
        /// Number of flow field builds this tick.
        /// </summary>
        public int FlowFieldBuildsThisTick;

        /// <summary>
        /// Current number of requests queued (waiting for next tick).
        /// </summary>
        public int NavRequestsQueued;

        /// <summary>
        /// Number of requests dropped this tick due to budget exceeded.
        /// </summary>
        public int RequestsDroppedThisTick;

        /// <summary>
        /// Maximum queue size reached this tick.
        /// </summary>
        public int MaxQueueSizeThisTick;

        /// <summary>
        /// Tick when counters were last reset.
        /// </summary>
        public uint LastResetTick;
    }

    /// <summary>
    /// Priority queue entry for deferred navigation requests.
    /// Used when budget is exceeded to defer requests to future ticks.
    /// </summary>
    [InternalBufferCapacity(32)]
    public struct NavRequestQueue : IBufferElementData
    {
        /// <summary>
        /// Entity requesting the path.
        /// </summary>
        public Entity RequestingEntity;

        /// <summary>
        /// Start position.
        /// </summary>
        public float3 StartPosition;

        /// <summary>
        /// Goal position.
        /// </summary>
        public float3 GoalPosition;

        /// <summary>
        /// Request priority (0=Critical, 1=Important, 2=Normal, 3=Low).
        /// </summary>
        public byte Priority;

        /// <summary>
        /// Heat tier of the request (Hot/Warm/Cold).
        /// </summary>
        public NavHeatTier HeatTier;

        /// <summary>
        /// Tick when request was enqueued.
        /// </summary>
        public uint EnqueueTick;

        /// <summary>
        /// Locomotion mode for this request.
        /// </summary>
        public LocomotionMode LocomotionMode;
    }
}

