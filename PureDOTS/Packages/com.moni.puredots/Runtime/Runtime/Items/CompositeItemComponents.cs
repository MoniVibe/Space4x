using PureDOTS.Runtime.Shared;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Items
{
    /// <summary>
    /// Component marking an entity as a composite item (wagon, weapon, module, etc.)
    /// that aggregates stats from its parts.
    /// </summary>
    public struct CompositeItem : IComponentData
    {
        /// <summary>
        /// Parent entity that owns this composite (e.g., wagon entity, ship module entity).
        /// May be Entity.Null if this is the root item.
        /// </summary>
        public Entity OwnerEntity;

        /// <summary>
        /// Aggregated durability (0-1 normalized) computed from weighted part durabilities.
        /// </summary>
        public half AggregatedDurability;

        /// <summary>
        /// Aggregated quality tier computed from part quality tiers and weights.
        /// </summary>
        public QualityTier AggregatedTier;

        /// <summary>
        /// Hash of part data for change detection (avoids unnecessary recalculation).
        /// </summary>
        public uint AggregationHash;

        /// <summary>
        /// Flags indicating item state.
        /// </summary>
        public CompositeItemFlags Flags;
    }

    /// <summary>
    /// Flags for composite item state.
    /// </summary>
    [System.Flags]
    public enum CompositeItemFlags : byte
    {
        None = 0,
        Broken = 1 << 0,        // Critical part(s) at zero durability
        Damaged = 1 << 1,       // Some parts below threshold
        Flawed = 1 << 2,        // Repaired below skill cap (latent flaws)
        NeedsRepair = 1 << 3    // Durability below maintenance threshold
    }

    /// <summary>
    /// Buffer element representing a single part of a composite item.
    /// Parts carry lightweight data for quality, durability, and material tracking.
    /// </summary>
    public struct ItemPart : IBufferElementData
    {
        /// <summary>
        /// Part type ID from catalog (e.g., Wheel, Axle, Blade, PowerCell).
        /// </summary>
        public ushort PartTypeId;

        /// <summary>
        /// Material name (e.g., "Iron", "Mithril", "Titanium").
        /// </summary>
        public FixedString32Bytes Material;

        /// <summary>
        /// Quality score (0-1) for this part.
        /// </summary>
        public half Quality01;

        /// <summary>
        /// Durability (0-1 normalized). Part breaks when this reaches 0.
        /// </summary>
        public half Durability01;

        /// <summary>
        /// Rarity weight (0-255) influencing aggregate rarity calculation.
        /// Higher weight = more influence on final rarity tier.
        /// </summary>
        public byte RarityWeight;

        /// <summary>
        /// Part state flags.
        /// </summary>
        public PartFlags Flags;
    }

    /// <summary>
    /// Flags for individual part state.
    /// </summary>
    [System.Flags]
    public enum PartFlags : byte
    {
        None = 0,
        Damaged = 1 << 0,       // Durability below threshold
        Broken = 1 << 1,        // Durability at zero
        Flawed = 1 << 2,        // Repaired below skill cap
        Critical = 1 << 3       // Critical part (item breaks if this breaks)
    }

    /// <summary>
    /// Request to repair a composite item.
    /// </summary>
    public struct RepairRequest : IComponentData
    {
        /// <summary>
        /// Entity of the craftsman/technician performing repair.
        /// </summary>
        public Entity RepairerEntity;

        /// <summary>
        /// Repair skill level (0-100) determining restoration cap.
        /// </summary>
        public byte RepairSkillLevel;

        /// <summary>
        /// Target durability (0-1) to restore to (capped by skill).
        /// </summary>
        public half TargetDurability01;

        /// <summary>
        /// Tick when repair started.
        /// </summary>
        public uint RepairStartTick;
    }

    /// <summary>
    /// Event buffer for durability wear/damage to composite items.
    /// </summary>
    public struct DurabilityWearEvent : IBufferElementData
    {
        /// <summary>
        /// Amount of wear to apply (0-1 normalized).
        /// </summary>
        public half WearAmount01;

        /// <summary>
        /// Specific part type to target (0 = distribute to all parts).
        /// </summary>
        public ushort TargetPartTypeId;

        /// <summary>
        /// Wear type affecting distribution.
        /// </summary>
        public WearType Type;

        /// <summary>
        /// Tick when wear occurred.
        /// </summary>
        public uint WearTick;
    }

    /// <summary>
    /// Type of wear affecting distribution logic.
    /// </summary>
    public enum WearType : byte
    {
        Uniform = 0,        // Distribute evenly across all parts
        Weighted = 1,       // Distribute by part weight (heavier parts wear faster)
        Targeted = 2,      // Apply to specific part type only
        Random = 3         // Random distribution (deterministic via seed)
    }
}

