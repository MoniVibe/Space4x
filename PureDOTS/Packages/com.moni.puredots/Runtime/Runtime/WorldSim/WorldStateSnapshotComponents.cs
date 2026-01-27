using Unity.Entities;

namespace PureDOTS.Runtime.WorldSim
{
    /// <summary>
    /// World state snapshot for hot path reads.
    /// Local effects from existing state (fire damage, weather modifiers, power coverage).
    /// Hot path reads snapshots only, no calculations.
    /// </summary>
    public struct WorldStateSnapshot : IComponentData
    {
        /// <summary>
        /// Current fire damage multiplier (0..1, 0 = no fire, 1 = maximum fire).
        /// </summary>
        public float FireDamageMultiplier;

        /// <summary>
        /// Current weather modifier for accuracy/movement (0..1).
        /// </summary>
        public float WeatherModifier;

        /// <summary>
        /// Current power coverage (0..1, 0 = no power, 1 = full power).
        /// </summary>
        public float PowerCoverage;

        /// <summary>
        /// Current disease level (0..1, 0 = no disease, 1 = maximum disease).
        /// </summary>
        public float DiseaseLevel;

        /// <summary>
        /// Current pollution level (0..1, 0 = no pollution, 1 = maximum pollution).
        /// </summary>
        public float PollutionLevel;

        /// <summary>
        /// Current flood level (0..1, 0 = no flood, 1 = maximum flood).
        /// </summary>
        public float FloodLevel;

        /// <summary>
        /// Tick when this snapshot was last updated.
        /// </summary>
        public uint LastUpdateTick;
    }
}

