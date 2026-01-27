using System;
using Unity.Entities;

namespace PureDOTS.Runtime.Shared
{
    /// <summary>
    /// Rarity tiers used by both Godgame and Space4X for items, modules, and equipment.
    /// Rarity is economy/availability-driven, separate from quality.
    /// </summary>
    public enum Rarity : byte
    {
        Common = 0,
        Uncommon = 1,
        Rare = 2,
        Epic = 3,
        Legendary = 4
    }

    /// <summary>
    /// Quality flags for instance quality (e.g., Masterwork, Flawed).
    /// </summary>
    [Flags]
    public enum QualityFlags : uint
    {
        None = 0,
        Masterwork = 1 << 0,
        Flawed = 1 << 1,
        Blessed = 1 << 2,
        Cursed = 1 << 3
    }

    /// <summary>
    /// Quality tier classification for items and materials.
    /// Used for display and quality-based naming (e.g., "Poor Iron Sword" vs "Masterwork Iron Sword").
    /// </summary>
    public enum QualityTier : byte
    {
        Poor = 0,
        Common = 1,
        Good = 2,
        Excellent = 3,
        Masterwork = 4
    }

    /// <summary>
    /// Runtime component storing quality instance data for an item/module.
    /// Quality is separate from rarity (economy-driven).
    /// </summary>
    public struct InstanceQuality : IComponentData
    {
        /// <summary>
        /// Quality score (0-1), computed from material purity, skill, station, recipe.
        /// </summary>
        public float Score01;

        /// <summary>
        /// Quality tier derived from Score01 via formula blob cutoffs.
        /// </summary>
        public QualityTier Tier;

        /// <summary>
        /// Quality flags (Masterwork, Flawed, etc.).
        /// </summary>
        public QualityFlags Flags;

        /// <summary>
        /// Deterministic hash of inputs for audit/determinism verification.
        /// </summary>
        public uint ProvenanceHash;
    }

    /// <summary>
    /// Utility helpers for converting quality values to rarity and quality tiers.
    /// </summary>
    public static class QualityRarityUtility
    {
        /// <summary>
        /// [OBSOLETE] Converts a quality value (0-100) to a Rarity enum.
        /// TODO: Remove this - rarity should come from economy, not quality.
        /// Thresholds: Common (0-40), Uncommon (41-60), Rare (61-80), Epic (81-90), Legendary (91-100)
        /// </summary>
        [Obsolete("Rarity should come from economy data, not quality. Use temporary mapping only if needed.")]
        public static Rarity QualityToRarity(float quality)
        {
            if (quality >= 90f) return Rarity.Legendary;
            if (quality >= 70f) return Rarity.Epic;
            if (quality >= 50f) return Rarity.Rare;
            if (quality >= 30f) return Rarity.Uncommon;
            return Rarity.Common;
        }

        /// <summary>
        /// Converts a quality value (0-100) to a QualityTier enum.
        /// Thresholds: Poor (0-20), Common (21-40), Good (41-60), Excellent (61-80), Masterwork (81-100)
        /// </summary>
        public static QualityTier QualityToTier(float quality)
        {
            if (quality >= 80f) return QualityTier.Masterwork;
            if (quality >= 60f) return QualityTier.Excellent;
            if (quality >= 40f) return QualityTier.Good;
            if (quality >= 20f) return QualityTier.Common;
            return QualityTier.Poor;
        }

        /// <summary>
        /// Gets the rarity modifier multiplier for stat calculations.
        /// Common: 1.0x, Uncommon: 1.05x, Rare: 1.10x, Epic: 1.15x, Legendary: 1.20x
        /// </summary>
        public static float GetRarityModifier(Rarity rarity)
        {
            return rarity switch
            {
                Rarity.Common => 1.0f,
                Rarity.Uncommon => 1.05f,
                Rarity.Rare => 1.10f,
                Rarity.Epic => 1.15f,
                Rarity.Legendary => 1.20f,
                _ => 1.0f
            };
        }
    }
}

