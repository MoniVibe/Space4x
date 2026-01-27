using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Stats
{
    /// <summary>
    /// Semantic tags for filtering trait axes (metadata only; systems query by AxisId).
    /// </summary>
    [System.Flags]
    public enum TraitAxisTag : byte
    {
        None = 0,
        Alignment = 1 << 0,
        Behavior = 1 << 1,
        Outlook = 1 << 2,
        Stat = 1 << 3,
        Communication = 1 << 4,
        Cooperation = 1 << 5
    }

    /// <summary>
    /// Definition of a single trait axis (ID, range, tags, semantic labels).
    /// </summary>
    public struct TraitAxisDefinition
    {
        /// <summary>Unique identifier (e.g., "LawfulChaotic", "Cohesion").</summary>
        public FixedString32Bytes AxisId;
        
        /// <summary>Human-readable display name.</summary>
        public FixedString64Bytes DisplayName;
        
        /// <summary>Minimum value (typically -100 for bipolar, 0 for unipolar).</summary>
        public float MinValue;
        
        /// <summary>Maximum value (typically +100 for bipolar, 100 for unipolar).</summary>
        public float MaxValue;
        
        /// <summary>Default/neutral value (typically 0 for bipolar, 50 for unipolar).</summary>
        public float DefaultValue;
        
        /// <summary>Semantic tags for filtering (Alignment, Behavior, Outlook, etc.).</summary>
        public TraitAxisTag Tags;
        
        /// <summary>For bipolar axes: negative pole label (e.g., "Chaotic", "Evil", "Corrupt").</summary>
        public FixedString32Bytes NegativePoleLabel;
        
        /// <summary>For bipolar axes: neutral label (e.g., "Neutral").</summary>
        public FixedString32Bytes NeutralLabel;
        
        /// <summary>For bipolar axes: positive pole label (e.g., "Lawful", "Good", "Pure").</summary>
        public FixedString32Bytes PositivePoleLabel;
    }

    /// <summary>
    /// Blob asset catalog of trait axis definitions.
    /// </summary>
    public struct TraitAxisCatalogBlob
    {
        /// <summary>Array of axis definitions.</summary>
        public BlobArray<TraitAxisDefinition> Axes;
    }

    /// <summary>
    /// Single axis delta (modifies one trait axis value).
    /// </summary>
    public struct TraitAxisDelta
    {
        /// <summary>Axis identifier to modify.</summary>
        public FixedString32Bytes AxisId;
        
        /// <summary>Delta value to apply (additive).</summary>
        public float Delta;
    }

    /// <summary>
    /// Action footprint: set of axis deltas emitted by an action type.
    /// </summary>
    public struct ActionFootprintBlob
    {
        /// <summary>Action type identifier (e.g., "Kill", "Torture", "Mercy", "Charity").</summary>
        public FixedString32Bytes ActionTypeId;
        
        /// <summary>Array of axis deltas this action emits.</summary>
        public BlobArray<TraitAxisDelta> Deltas;
    }

    /// <summary>
    /// Intent modifier: scales or adjusts action footprint deltas based on intent.
    /// </summary>
    public struct IntentModifierBlob
    {
        /// <summary>Intent identifier (e.g., "ProtectOthers", "PersonalGain", "Duty", "Revenge").</summary>
        public FixedString32Bytes IntentId;
        
        /// <summary>Multiplier per axis (1.0 = no change, 0.5 = half impact, 2.0 = double impact, -1.0 = flip sign).</summary>
        public BlobArray<TraitAxisDelta> Multipliers;
        
        /// <summary>Additional fixed offsets per axis (applied after multiplier).</summary>
        public BlobArray<TraitAxisDelta> Offsets;
    }

    /// <summary>
    /// Context modifier: adjusts action footprint deltas based on target classification.
    /// </summary>
    public struct ContextModifierBlob
    {
        /// <summary>Context/target classification (e.g., "Innocent", "Threat", "Criminal", "ManEatingBeast").</summary>
        public FixedString32Bytes ContextId;
        
        /// <summary>Additional deltas per axis (applied after intent modifier).</summary>
        public BlobArray<TraitAxisDelta> Deltas;
    }
}



