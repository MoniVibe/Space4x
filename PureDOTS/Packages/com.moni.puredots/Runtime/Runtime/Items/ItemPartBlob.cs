using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Items
{
    /// <summary>
    /// Blob catalog defining part specifications and material properties.
    /// </summary>
    public struct ItemPartCatalogBlob
    {
        /// <summary>
        /// Part specifications indexed by PartTypeId.
        /// </summary>
        public BlobArray<ItemPartSpec> PartSpecs;

        /// <summary>
        /// Material names indexed by MaterialId.
        /// </summary>
        public BlobArray<FixedString32Bytes> MaterialNames;

        /// <summary>
        /// Material durability modifiers (multipliers) indexed by MaterialId.
        /// Example: Mithril = 1.5x, Iron = 1.0x, Wood = 0.7x
        /// </summary>
        public BlobArray<float> MaterialDurabilityMods;
    }

    /// <summary>
    /// Specification for a part type in the catalog.
    /// </summary>
    public struct ItemPartSpec
    {
        /// <summary>
        /// Part type ID (must match index in PartSpecs array).
        /// </summary>
        public ushort PartTypeId;

        /// <summary>
        /// Part name for display/debugging.
        /// </summary>
        public FixedString64Bytes PartName;

        /// <summary>
        /// Aggregation weight (0-1) determining contribution to parent item.
        /// Example: Wheels = 0.3 (30%), Axle = 0.4 (40%), Bolts = 0.1 (10%).
        /// </summary>
        public float AggregationWeight;

        /// <summary>
        /// Durability multiplier applied to base material durability.
        /// </summary>
        public float DurabilityMultiplier;

        /// <summary>
        /// Minimum repair skill level (0-100) required to restore this part.
        /// </summary>
        public byte RepairSkillRequired;

        /// <summary>
        /// Whether this part is critical (item breaks if this part breaks).
        /// </summary>
        public bool IsCritical;

        /// <summary>
        /// Durability threshold (0-1) below which part is considered "damaged".
        /// </summary>
        public half DamageThreshold01;
    }
}

