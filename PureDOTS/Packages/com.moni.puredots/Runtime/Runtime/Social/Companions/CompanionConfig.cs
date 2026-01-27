using Unity.Entities;

namespace PureDOTS.Runtime.Social
{
    /// <summary>
    /// Configuration singleton for companion bond system.
    /// </summary>
    public struct CompanionConfig : IComponentData
    {
        /// <summary>
        /// Ticks between formation scans (default: 1000 ticks ≈ 16 seconds at 60tps).
        /// </summary>
        public uint FormationCheckInterval;

        /// <summary>
        /// Ticks between evolution updates (default: 100 ticks ≈ 1.6 seconds at 60tps).
        /// </summary>
        public uint EvolutionCheckInterval;

        /// <summary>
        /// Minimum EntityRelation intensity threshold for bond formation (default: 60).
        /// </summary>
        public sbyte MinRelationIntensityForBond;

        /// <summary>
        /// Minimum compatibility score threshold for bond formation (default: 50).
        /// </summary>
        public float MinCompatibilityForBond;

        /// <summary>
        /// Soft cap on bonds per entity (default: 3).
        /// </summary>
        public byte MaxBondsPerEntity;

        /// <summary>
        /// Intensity growth rate per positive interaction (default: 0.05).
        /// </summary>
        public float IntensityGrowthRate;

        /// <summary>
        /// Trust growth rate per reliable action (default: 0.03).
        /// </summary>
        public float TrustGrowthRate;

        /// <summary>
        /// Rivalry growth rate per competitive event (default: 0.08).
        /// </summary>
        public float RivalryGrowthRate;

        /// <summary>
        /// Decay rate per tick when no interaction (default: 0.0001).
        /// </summary>
        public float DecayRate;

        /// <summary>
        /// Create default configuration.
        /// </summary>
        public static CompanionConfig Default()
        {
            return new CompanionConfig
            {
                FormationCheckInterval = 1000,
                EvolutionCheckInterval = 100,
                MinRelationIntensityForBond = 60,
                MinCompatibilityForBond = 50f,
                MaxBondsPerEntity = 3,
                IntensityGrowthRate = 0.05f,
                TrustGrowthRate = 0.03f,
                RivalryGrowthRate = 0.08f,
                DecayRate = 0.0001f
            };
        }
    }
}

