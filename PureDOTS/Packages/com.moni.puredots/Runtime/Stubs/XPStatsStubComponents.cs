// [TRI-STUB] Stub components for XP pools system
using Unity.Entities;

namespace PureDOTS.Runtime.Stats
{
    /// <summary>
    /// Experience point pools for stat progression.
    /// These pools feed into derived attributes (Strength, Agility, Intelligence).
    /// </summary>
    public struct XPStats : IComponentData
    {
        /// <summary>
        /// Physique experience pool. Feeds Strength development.
        /// </summary>
        public float PhysiqueXP;

        /// <summary>
        /// Finesse experience pool. Feeds Agility development.
        /// </summary>
        public float FinesseXP;

        /// <summary>
        /// Will experience pool. Feeds Intelligence development.
        /// </summary>
        public float WillXP;

        /// <summary>
        /// Wisdom experience pool. General learning/cross-discipline + global gain modifier.
        /// </summary>
        public float WisdomXP;

        /// <summary>
        /// Total XP across all pools.
        /// </summary>
        public readonly float TotalXP => PhysiqueXP + FinesseXP + WillXP + WisdomXP;
    }

    /// <summary>
    /// XP pool configuration - decay rates and gain modifiers.
    /// </summary>
    public struct XPConfig : IComponentData
    {
        /// <summary>
        /// XP decay rate per day (0-1).
        /// </summary>
        public float DecayRatePerDay;

        /// <summary>
        /// Global XP gain modifier (multiplier).
        /// </summary>
        public float GainModifier;

        /// <summary>
        /// Wisdom-based gain modifier (multiplier from WisdomXP).
        /// </summary>
        public float WisdomGainModifier;
    }

    /// <summary>
    /// XP type for spending/transferring.
    /// </summary>
    public enum XPType : byte
    {
        Physique = 0,
        Finesse = 1,
        Will = 2,
        Wisdom = 3
    }
}

