using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Construction
{
    /// <summary>
    /// Specification for a building pattern (blueprint).
    /// </summary>
    public struct BuildingPatternSpec
    {
        /// <summary>Unique pattern ID.</summary>
        public int PatternId;
        
        /// <summary>Primary category.</summary>
        public BuildCategory Category;
        
        /// <summary>Generic "build effort" cost.</summary>
        public float BaseCost;
        
        /// <summary>Base build time (seconds at 1 builder, baseline).</summary>
        public float BaseBuildTime;
        
        /// <summary>Tech/advancement level (0..N).</summary>
        public byte Tier;
        
        /// <summary>Minimum population to unlock.</summary>
        public float MinPopulation;
        
        /// <summary>Minimum food per capita to unlock.</summary>
        public float MinFoodPerCapita;
        
        /// <summary>Required advancement level.</summary>
        public byte RequiresAdvancementLevel;
        
        /// <summary>Whether auto-build is eligible (1=villagers/groups allowed to auto-request).</summary>
        public byte IsAutoBuildEligible;
        
        /// <summary>Pattern utility score (for scoring during selection).</summary>
        public float PatternUtility;
        
        /// <summary>Label for this pattern.</summary>
        public FixedString64Bytes Label;
    }
    
    /// <summary>
    /// Blob asset catalog containing all building pattern specs.
    /// </summary>
    public struct BuildingPatternCatalog
    {
        public BlobArray<BuildingPatternSpec> Specs;
    }
    
    /// <summary>
    /// Singleton component holding the global building pattern catalog.
    /// </summary>
    public struct ConstructionConfigState : IComponentData
    {
        /// <summary>Reference to the building pattern catalog blob asset.</summary>
        public BlobAssetReference<BuildingPatternCatalog> Catalog;
        
        /// <summary>Frequency of demand aggregation checks in ticks.</summary>
        public uint AggregationCheckFrequency;
    }
}
























