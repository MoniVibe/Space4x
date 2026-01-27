using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Environment
{
    /// <summary>
    /// Specification for vegetation needs in a catalog.
    /// Used to define environmental requirements for different plant types.
    /// </summary>
    public struct VegetationNeedsSpec
    {
        /// <summary>Unique identifier for this vegetation needs spec.</summary>
        public ushort SpecId;

        /// <summary>Minimum required sunlight (0-1).</summary>
        public float SunlightMin;

        /// <summary>Maximum tolerated sunlight (0-1).</summary>
        public float SunlightMax;

        /// <summary>Minimum required moisture (0-1).</summary>
        public float MoistureMin;

        /// <summary>Maximum tolerated moisture (0-1).</summary>
        public float MoistureMax;

        /// <summary>Minimum required temperature.</summary>
        public float TempMin;

        /// <summary>Maximum tolerated temperature.</summary>
        public float TempMax;

        /// <summary>Root depth (affects drainage interaction if implemented).</summary>
        public float RootDepth;

        /// <summary>Moisture consumption rate (how much moisture this plant uses per tick).</summary>
        public float MoistureUsage;
    }

    /// <summary>
    /// Blob asset catalog containing all vegetation needs specs.
    /// Built from ScriptableObjects/JSON and merged at bootstrap.
    /// </summary>
    public struct VegetationNeedsCatalog
    {
        /// <summary>Array of vegetation needs specs.</summary>
        public BlobArray<VegetationNeedsSpec> Specs;
    }

    /// <summary>
    /// Configuration state for vegetation needs system.
    /// One singleton in the world, set per-game during bootstrap.
    /// </summary>
    public struct VegetationNeedsConfigState : IComponentData
    {
        /// <summary>Reference to the vegetation needs catalog blob asset.</summary>
        public BlobAssetReference<VegetationNeedsCatalog> Catalog;

        /// <summary>
        /// Default configuration with empty catalog.
        /// </summary>
        public static VegetationNeedsConfigState Default => new VegetationNeedsConfigState
        {
            Catalog = default
        };
    }
}
























