using Unity.Entities;

namespace PureDOTS.Runtime.Relations
{
    /// <summary>
    /// Performance budget configuration singleton for relations/econ/social systems.
    /// Defines hard caps on expensive operations per tick.
    /// </summary>
    public struct RelationPerformanceBudget : IComponentData
    {
        /// <summary>
        /// Maximum relation events processed per tick.
        /// Default: 20 events/tick.
        /// </summary>
        public int MaxRelationEventsPerTick;

        /// <summary>
        /// Maximum market updates per tick.
        /// Default: 10 markets/tick.
        /// </summary>
        public int MaxMarketUpdatesPerTick;

        /// <summary>
        /// Maximum political decisions processed per tick.
        /// Default: 5 decisions/tick.
        /// </summary>
        public int MaxPoliticalDecisionsPerTick;

        /// <summary>
        /// Maximum social interactions processed per tick.
        /// Default: 15 interactions/tick.
        /// </summary>
        public int MaxSocialInteractionsPerTick;

        /// <summary>
        /// Maximum personal relations per individual.
        /// Default: 16 relations.
        /// </summary>
        public int MaxPersonalRelationsPerIndividual;

        /// <summary>
        /// Maximum org relations per organization.
        /// Default: 32 relations.
        /// </summary>
        public int MaxOrgRelationsPerOrg;

        /// <summary>
        /// Warning threshold for relation graph size.
        /// Default: 1000 edges.
        /// </summary>
        public int RelationGraphWarningThreshold;

        /// <summary>
        /// Creates default budget configuration.
        /// </summary>
        public static RelationPerformanceBudget CreateDefaults()
        {
            return new RelationPerformanceBudget
            {
                MaxRelationEventsPerTick = 20,
                MaxMarketUpdatesPerTick = 10,
                MaxPoliticalDecisionsPerTick = 5,
                MaxSocialInteractionsPerTick = 15,
                MaxPersonalRelationsPerIndividual = 16,
                MaxOrgRelationsPerOrg = 32,
                RelationGraphWarningThreshold = 1000
            };
        }
    }

    /// <summary>
    /// Performance counters singleton for relations/econ/social systems.
    /// Tracks actual usage per tick for monitoring and enforcement.
    /// </summary>
    public struct RelationPerformanceCounters : IComponentData
    {
        /// <summary>
        /// Number of relation events processed this tick.
        /// </summary>
        public int RelationEventsThisTick;

        /// <summary>
        /// Number of market updates processed this tick.
        /// </summary>
        public int MarketUpdatesThisTick;

        /// <summary>
        /// Number of political decisions processed this tick.
        /// </summary>
        public int PoliticalDecisionsThisTick;

        /// <summary>
        /// Number of social interactions processed this tick.
        /// </summary>
        public int SocialInteractionsThisTick;

        /// <summary>
        /// Current total personal relation edges in graph.
        /// </summary>
        public int TotalPersonalRelations;

        /// <summary>
        /// Current total org relation edges in graph.
        /// </summary>
        public int TotalOrgRelations;

        /// <summary>
        /// Number of operations dropped this tick due to budget exceeded.
        /// </summary>
        public int OperationsDroppedThisTick;

        /// <summary>
        /// Tick when counters were last reset.
        /// </summary>
        public uint LastResetTick;
    }
}

