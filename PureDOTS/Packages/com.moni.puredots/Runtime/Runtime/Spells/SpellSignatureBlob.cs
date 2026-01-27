using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Spells
{
    /// <summary>
    /// Catalog of spell signature definitions.
    /// Signatures are unlocked at 200%, 300%, and 400% mastery milestones.
    /// </summary>
    public struct SpellSignatureBlob
    {
        public BlobArray<SignatureEntry> Signatures;
    }

    /// <summary>
    /// Individual signature definition.
    /// </summary>
    public struct SignatureEntry
    {
        /// <summary>
        /// Unique signature identifier.
        /// </summary>
        public FixedString64Bytes SignatureId;

        /// <summary>
        /// Display name for UI.
        /// </summary>
        public FixedString64Bytes DisplayName;

        /// <summary>
        /// Type of signature modification.
        /// </summary>
        public SignatureType Type;

        /// <summary>
        /// Modifier value (multiplier, flat bonus, etc.).
        /// </summary>
        public float ModifierValue;

        /// <summary>
        /// Optional: Target spell school (empty = applies to all).
        /// </summary>
        public FixedString64Bytes TargetSpellSchool;

        /// <summary>
        /// Description of what this signature does.
        /// </summary>
        public FixedString128Bytes Description;
    }

    /// <summary>
    /// Type of signature modification.
    /// </summary>
    public enum SignatureType : byte
    {
        Multishot = 0,          // Multiple projectiles/instances
        Amplified = 1,          // Increased effect (damage/heal)
        Extended = 2,           // Longer duration/range
        Efficient = 3,          // Reduced mana cost
        Swift = 4,              // Reduced cast time
        Persistent = 5,         // Lingering effects
        Penetrating = 6,        // Ignores resistances
        Hybridization = 7       // Required for combining spells
    }

    /// <summary>
    /// Singleton reference to signature catalog blob.
    /// </summary>
    public struct SpellSignatureCatalogRef : IComponentData
    {
        public BlobAssetReference<SpellSignatureBlob> Blob;
    }
}

