// [TRI-STUB] Stub components for derived attributes system
using Unity.Entities;

namespace PureDOTS.Runtime.Stats
{
    /// <summary>
    /// Derived attributes computed from base attributes + experience.
    /// These are cached values recalculated when base stats or XP change.
    /// </summary>
    public struct DerivedAttributes : IComponentData
    {
        /// <summary>
        /// Physical power (0-100). Derived from Physique + PhysiqueXP.
        /// Default derivation: Strength = 0.8 * Physique + 0.2 * WeaponMastery
        /// </summary>
        public float Strength;

        /// <summary>
        /// Speed and dexterity (0-100). Derived from Finesse + FinesseXP.
        /// Default derivation: Agility = 0.8 * Finesse + 0.2 * Acrobatics
        /// </summary>
        public float Agility;

        /// <summary>
        /// Mental acuity (0-100). Derived from Will + WillXP.
        /// Default derivation: Intelligence = 0.6 * Will + 0.4 * Education
        /// </summary>
        public float Intelligence;

        /// <summary>
        /// General learning/cross-discipline (0-100). Derived from Wisdom + WisdomXP.
        /// Default derivation: WisdomDerived = 0.6 * Will + 0.4 * Lore
        /// </summary>
        public float WisdomDerived;

        /// <summary>
        /// Tick when attributes were last recalculated.
        /// </summary>
        public uint LastRecalculatedTick;

        /// <summary>
        /// Flag indicating if attributes need recalculation.
        /// </summary>
        public byte NeedsRecalculation;
    }

    /// <summary>
    /// Derivation weights for calculating derived attributes.
    /// Games may override these weights.
    /// </summary>
    public struct DerivationWeights : IComponentData
    {
        /// <summary>
        /// Weight for Physique in Strength calculation (default 0.8).
        /// </summary>
        public float StrengthPhysiqueWeight;

        /// <summary>
        /// Weight for WeaponMastery in Strength calculation (default 0.2).
        /// </summary>
        public float StrengthWeaponMasteryWeight;

        /// <summary>
        /// Weight for Finesse in Agility calculation (default 0.8).
        /// </summary>
        public float AgilityFinesseWeight;

        /// <summary>
        /// Weight for Acrobatics in Agility calculation (default 0.2).
        /// </summary>
        public float AgilityAcrobaticsWeight;

        /// <summary>
        /// Weight for Will in Intelligence calculation (default 0.6).
        /// </summary>
        public float IntelligenceWillWeight;

        /// <summary>
        /// Weight for Education in Intelligence calculation (default 0.4).
        /// </summary>
        public float IntelligenceEducationWeight;

        /// <summary>
        /// Weight for Will in WisdomDerived calculation (default 0.6).
        /// </summary>
        public float WisdomWillWeight;

        /// <summary>
        /// Weight for Lore in WisdomDerived calculation (default 0.4).
        /// </summary>
        public float WisdomLoreWeight;

        /// <summary>
        /// Create default derivation weights.
        /// </summary>
        public static DerivationWeights Default()
        {
            return new DerivationWeights
            {
                StrengthPhysiqueWeight = 0.8f,
                StrengthWeaponMasteryWeight = 0.2f,
                AgilityFinesseWeight = 0.8f,
                AgilityAcrobaticsWeight = 0.2f,
                IntelligenceWillWeight = 0.6f,
                IntelligenceEducationWeight = 0.4f,
                WisdomWillWeight = 0.6f,
                WisdomLoreWeight = 0.4f
            };
        }
    }
}

