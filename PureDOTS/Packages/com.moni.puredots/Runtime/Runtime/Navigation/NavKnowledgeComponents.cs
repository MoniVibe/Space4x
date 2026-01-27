using Unity.Entities;

namespace PureDOTS.Runtime.Navigation
{
    /// <summary>
    /// Edge state modifier applied by operations (blockades, sieges, wars).
    /// Applied to NavEdge, RegionEdge, or TransitEdge to affect pathfinding.
    /// </summary>
    public struct NavEdgeState : IComponentData
    {
        /// <summary>
        /// Whether edge is closed (hard deny - cannot traverse).
        /// </summary>
        public byte Closed;

        /// <summary>
        /// Risk modifier multiplier (1.0 = no change, 2.0 = double risk).
        /// </summary>
        public float RiskModifier;

        /// <summary>
        /// Time multiplier (1.0 = no change, 2.0 = double time).
        /// </summary>
        public float TimeMultiplier;

        /// <summary>
        /// Cost multiplier (1.0 = no change, 2.0 = double cost).
        /// </summary>
        public float CostMultiplier;

        /// <summary>
        /// Tick when this state was applied.
        /// </summary>
        public uint AppliedTick;

        /// <summary>
        /// Tick when this state expires (0 = permanent until removed).
        /// </summary>
        public uint ExpirationTick;
    }

    /// <summary>
    /// Knowledge filter for building faction-specific navigation graphs.
    /// When KnownFacts system exists, this will reference it to filter graph edges/nodes.
    /// </summary>
    public struct NavKnowledgeFilter : IComponentData
    {
        /// <summary>
        /// Faction/Entity ID that owns this knowledge filter.
        /// </summary>
        public int FactionId;

        /// <summary>
        /// Entity reference to KnownFacts component (when KnownFacts system exists).
        /// </summary>
        public Entity KnownFactsEntity;

        /// <summary>
        /// Tick when knowledge was last updated.
        /// </summary>
        public uint LastUpdateTick;
    }
}






















