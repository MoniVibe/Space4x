using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Aggregate
{
    /// <summary>
    /// Enumeration of source traits that can be aggregated.
    /// Used as indices in AggregateAggregationRule.SourceTrait.
    /// </summary>
    public enum AggregateSourceTrait : byte
    {
        Initiative = 0,
        VengefulForgiving = 1,
        BoldCraven = 2,
        CorruptPure = 3,
        ChaoticLawful = 4,
        EvilGood = 5,
        MightMagic = 6,
        Ambition = 7,
        DesireStatus = 8,
        DesireWealth = 9,
        DesirePower = 10,
        DesireKnowledge = 11
    }

    /// <summary>
    /// Enumeration of target ambient metrics.
    /// Used as indices in AggregateAggregationRule.TargetMetric.
    /// </summary>
    public enum AggregateTargetMetric : byte
    {
        AmbientCourage = 0,
        AmbientCaution = 1,
        AmbientAnger = 2,
        AmbientCompassion = 3,
        AmbientDrive = 4,
        ExpectationLoyalty = 5,
        ExpectationConformity = 6,
        ToleranceForOutliers = 7
    }

    /// <summary>
    /// Defines how a source trait contributes to a target ambient metric.
    /// </summary>
    public struct AggregateAggregationRule
    {
        /// <summary>Source trait index (enum: BoldCraven, VengefulForgiving, etc.).</summary>
        public byte SourceTrait;
        
        /// <summary>Target metric index (enum: AmbientCourage, AmbientAnger, etc.).</summary>
        public byte TargetMetric;
        
        /// <summary>Contribution weight.</summary>
        public float Weight;
    }
    
    /// <summary>
    /// Configuration for how a specific aggregate type aggregates traits.
    /// </summary>
    public struct AggregateTypeConfig
    {
        /// <summary>Type ID matching AggregateIdentity.TypeId.</summary>
        public ushort TypeId;
        
        /// <summary>Rules for aggregating traits → ambient conditions.</summary>
        public BlobArray<AggregateAggregationRule> Rules;
        
        /// <summary>Threshold for triggering cascade effects (delta magnitude).</summary>
        public float CompositionChangeThreshold;
    }
    
    /// <summary>
    /// Blob asset catalog containing all aggregate type configs.
    /// </summary>
    public struct AggregateConfigCatalog
    {
        public BlobArray<AggregateTypeConfig> TypeConfigs;
    }
    
    /// <summary>
    /// Singleton component holding the global aggregation config catalog.
    /// </summary>
    public struct AggregateConfigState : IComponentData
    {
        /// <summary>Reference to the aggregation config catalog blob asset.</summary>
        public BlobAssetReference<AggregateConfigCatalog> Catalog;
        
        /// <summary>Frequency of ambient condition updates in ticks.</summary>
        public uint AmbientUpdateFrequency;
    }
}
























