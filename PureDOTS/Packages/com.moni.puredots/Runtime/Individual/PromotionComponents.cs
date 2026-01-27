using Unity.Entities;

namespace PureDOTS.Runtime.Individual
{
    /// <summary>
    /// Flags entities eligible for promotion from Villager to SimIndividual.
    /// Promotion thresholds: fame, kills, achievements, player selection.
    /// </summary>
    public struct PromotionCandidate : IComponentData
    {
        /// <summary>
        /// Fame threshold met flag.
        /// </summary>
        public bool FameThresholdMet;

        /// <summary>
        /// Kills threshold met flag.
        /// </summary>
        public bool KillsThresholdMet;

        /// <summary>
        /// Achievement threshold met flag.
        /// </summary>
        public bool AchievementThresholdMet;

        /// <summary>
        /// Player selection flag (manually promoted by player).
        /// </summary>
        public bool PlayerSelected;

        /// <summary>
        /// Tick when promotion was requested (for tracking).
        /// </summary>
        public uint RequestedTick;
    }
}

