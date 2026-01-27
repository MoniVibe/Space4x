using Unity.Entities;

namespace PureDOTS.Runtime.Navigation
{
    /// <summary>
    /// Heat tier classification for navigation and AI systems.
    /// Determines update frequency, budget allocation, and allowed operations.
    /// </summary>
    public enum NavHeatTier : byte
    {
        /// <summary>
        /// Hot path - runs every tick on many entities. Must be tiny, branch-light, data tight.
        /// Only simple math, no allocations, no pathfinding, reads NavPath only.
        /// </summary>
        Hot = 0,

        /// <summary>
        /// Warm path - runs regularly but throttled. Local pathfinding, group decisions, replanning.
        /// Throttled (K queries/tick), staggered updates, local A* only.
        /// </summary>
        Warm = 1,

        /// <summary>
        /// Cold path - runs rarely or on long intervals. Strategic planning, graph building, multi-modal routing.
        /// Event-driven or long intervals (50-200 ticks), strategic planning.
        /// </summary>
        Cold = 2
    }

    /// <summary>
    /// Tags a system with its heat tier classification.
    /// Used for system group organization and performance monitoring.
    /// </summary>
    public struct SystemHeatTier : IComponentData
    {
        /// <summary>
        /// The heat tier of this system.
        /// </summary>
        public NavHeatTier Tier;
    }

    /// <summary>
    /// Classifies an entity's navigation update frequency needs.
    /// Determines which systems should process this entity and how often.
    /// </summary>
    public struct EntityHeatTier : IComponentData
    {
        /// <summary>
        /// The heat tier classification for this entity.
        /// </summary>
        public NavHeatTier Tier;

        /// <summary>
        /// Whether this entity requires hot path updates (every tick).
        /// </summary>
        public byte RequiresHotPath;
    }
}

