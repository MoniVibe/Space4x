using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Navigation
{
    /// <summary>
    /// Locomotion modes for pathfinding (flexible flags).
    /// Uses [Flags] pattern to allow multiple modes per edge.
    /// </summary>
    [System.Flags]
    public enum LocomotionMode : uint
    {
        None = 0,

        // Standard locomotion modes
        Ground = 1 << 0,      // Ground-based movement (respects terrain, slopes)
        Air = 1 << 1,         // Flying/hovering (3D movement, min/max heights)
        Space = 1 << 2,       // Free 3D space movement
        SubLight = 1 << 3,    // Sub-light speed space travel
        FTL = 1 << 4,         // Faster-than-light travel (hyperlanes)

        // Custom modes (bits 8-31) reserved for game-specific extensions
        Custom0 = 1 << 8,
        Custom1 = 1 << 9,
        Custom2 = 1 << 10,
        Custom3 = 1 << 11,
        Custom4 = 1 << 12,
        Custom5 = 1 << 13,
        Custom6 = 1 << 14,
        Custom7 = 1 << 15,

        All = 0xFFFFFFFF
    }

    /// <summary>
    /// Navigation graph container (singleton).
    /// Stores graph nodes and edges for pathfinding.
    /// Phase 1: Single graph.
    /// Phase 2: Multi-region graphs, chunked graphs, etc.
    /// </summary>
    public struct NavGraph : IComponentData
    {
        /// <summary>
        /// Graph version (incremented when graph changes).
        /// </summary>
        public uint Version;

        /// <summary>
        /// Number of nodes in graph.
        /// </summary>
        public int NodeCount;

        /// <summary>
        /// Number of edges in graph.
        /// </summary>
        public int EdgeCount;

        /// <summary>
        /// Graph bounds min (for spatial queries).
        /// </summary>
        public float3 BoundsMin;

        /// <summary>
        /// Graph bounds max (for spatial queries).
        /// </summary>
        public float3 BoundsMax;
    }

    /// <summary>
    /// Navigation graph node (position in graph).
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct NavNode : IBufferElementData
    {
        /// <summary>
        /// Node position (world space).
        /// </summary>
        public float3 Position;

        /// <summary>
        /// Node flags (obstacle, waypoint, etc.).
        /// </summary>
        public NavNodeFlags Flags;

        /// <summary>
        /// Base cost to traverse this node.
        /// </summary>
        public float BaseCost;

        /// <summary>
        /// Node ID (index in buffer).
        /// </summary>
        public int NodeId;
    }

    /// <summary>
    /// Navigation graph node flags.
    /// </summary>
    [System.Flags]
    public enum NavNodeFlags : byte
    {
        None = 0,
        Obstacle = 1 << 0,        // Blocks movement
        Waypoint = 1 << 1,        // Valid waypoint
        Start = 1 << 2,            // Start node
        Goal = 1 << 3,             // Goal node
        Temporary = 1 << 4,        // Temporary node (for dynamic pathfinding)
        Hazard = 1 << 5            // Hazardous area
    }

    /// <summary>
    /// Navigation graph edge (connection between nodes).
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct NavEdge : IBufferElementData
    {
        /// <summary>
        /// Source node index.
        /// </summary>
        public int FromNode;

        /// <summary>
        /// Destination node index.
        /// </summary>
        public int ToNode;

        /// <summary>
        /// Edge cost (distance + modifiers).
        /// </summary>
        public float Cost;

        /// <summary>
        /// Locomotion modes allowed on this edge (bitmask).
        /// </summary>
        public LocomotionMode AllowedModes;

        /// <summary>
        /// Edge flags.
        /// </summary>
        public NavEdgeFlags Flags;

        /// <summary>
        /// Whether edge is bidirectional.
        /// </summary>
        public byte IsBidirectional;
    }

    /// <summary>
    /// Navigation graph edge flags.
    /// </summary>
    [System.Flags]
    public enum NavEdgeFlags : byte
    {
        None = 0,
        OneWay = 1 << 0,          // One-way edge
        RequiresAbility = 1 << 1,  // Requires special ability to traverse
        Dangerous = 1 << 2         // Dangerous traversal
    }

    /// <summary>
    /// Path request priority levels.
    /// </summary>
    public enum NavRequestPriority : byte
    {
        /// <summary>
        /// Critical - player command, immediate danger (processed first).
        /// </summary>
        Critical = 0,

        /// <summary>
        /// Important - army re-route, high-value trade (processed second).
        /// </summary>
        Important = 1,

        /// <summary>
        /// Normal - routine movements (processed third).
        /// </summary>
        Normal = 2,

        /// <summary>
        /// Low - random wandering, ambience (processed last, dropped if budget exceeded).
        /// </summary>
        Low = 3
    }

    /// <summary>
    /// Path request - entity wants to find a path.
    /// </summary>
    public struct PathRequest : IComponentData
    {
        /// <summary>
        /// Entity requesting path.
        /// </summary>
        public Entity RequestingEntity;

        /// <summary>
        /// Start position (world space).
        /// </summary>
        public float3 StartPosition;

        /// <summary>
        /// Goal position (world space).
        /// </summary>
        public float3 GoalPosition;

        /// <summary>
        /// Locomotion mode for this path.
        /// </summary>
        public LocomotionMode LocomotionMode;

        /// <summary>
        /// Request priority level (0=Critical, 1=Important, 2=Normal, 3=Low).
        /// Lower values processed first.
        /// </summary>
        public NavRequestPriority Priority;

        /// <summary>
        /// Heat tier classification for this request (Hot/Warm/Cold).
        /// Determines which system processes it and budget allocation.
        /// </summary>
        public NavHeatTier HeatTier;

        /// <summary>
        /// Tick when request was made.
        /// </summary>
        public uint RequestTick;

        /// <summary>
        /// Whether request is still active.
        /// </summary>
        public byte IsActive;
    }

    /// <summary>
    /// Path result - computed path waypoints.
    /// </summary>
    [InternalBufferCapacity(32)]
    public struct PathResult : IBufferElementData
    {
        /// <summary>
        /// Waypoint position (world space).
        /// </summary>
        public float3 WaypointPosition;

        /// <summary>
        /// Node index (if applicable).
        /// </summary>
        public int NodeIndex;

        /// <summary>
        /// Cost to reach this waypoint from start.
        /// </summary>
        public float CostToReach;
    }

    /// <summary>
    /// Path status - result of pathfinding computation.
    /// </summary>
    public enum PathStatus : byte
    {
        Pending = 0,        // Path computation in progress
        Success = 1,        // Path found
        Failed = 2,         // No path exists
        Partial = 3,        // Partial path (reached closest possible)
        Invalid = 4         // Request invalid/cancelled
    }

    /// <summary>
    /// Path state component - tracks pathfinding state for an entity.
    /// </summary>
    public struct PathState : IComponentData
    {
        /// <summary>
        /// Current path status.
        /// </summary>
        public PathStatus Status;

        /// <summary>
        /// Current waypoint index in path.
        /// </summary>
        public int CurrentWaypointIndex;

        /// <summary>
        /// Total path cost.
        /// </summary>
        public float TotalCost;

        /// <summary>
        /// Tick when path was computed.
        /// </summary>
        public uint PathComputedTick;

        /// <summary>
        /// Whether path is still valid.
        /// </summary>
        public byte IsValid;
    }
}
