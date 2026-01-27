using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Stats
{
    /// <summary>
    /// Single trait axis value (sparse storage; only non-default values stored).
    /// </summary>
    [InternalBufferCapacity(2)]
    public struct TraitAxisValue : IBufferElementData
    {
        /// <summary>Axis identifier (e.g., "LawfulChaotic", "Cohesion", "Xenophobia").</summary>
        public FixedString32Bytes AxisId;
        
        /// <summary>Axis value (typically -100 to +100 for bipolar, 0 to 100 for unipolar).</summary>
        public float Value;
    }

    /// <summary>
    /// Reference to trait axis catalog (defines available axes and their metadata).
    /// </summary>
    public struct TraitAxisSet : IComponentData
    {
        /// <summary>Blob reference to catalog defining axis definitions (IDs, ranges, tags, semantics).</summary>
        public BlobAssetReference<TraitAxisCatalogBlob> Catalog;
    }

    /// <summary>
    /// Optional state tracking for trait drift (decay rates, resistance factors, drift history).
    /// </summary>
    public struct TraitDriftState : IComponentData
    {
        /// <summary>Global decay rate per tick (applied to all axes; typically small, e.g., 0.001).</summary>
        public float DecayRatePerTick;
        
        /// <summary>Resistance curve exponent (higher = more resistance at extremes; default 2.0).</summary>
        public float ResistanceExponent;
        
        /// <summary>Last tick when drift was applied.</summary>
        public uint LastDriftTick;
        
        /// <summary>Drift interval (apply decay every N ticks; default 60).</summary>
        public uint DriftInterval;
    }
}



