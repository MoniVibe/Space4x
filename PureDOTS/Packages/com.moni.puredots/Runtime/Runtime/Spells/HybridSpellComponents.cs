using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Spells
{
    /// <summary>
    /// Hybrid spell created by combining two spells at 400% mastery.
    /// Creates a new unique spell that combines effects from both parents.
    /// </summary>
    public struct HybridSpell : IBufferElementData
    {
        /// <summary>
        /// Unique hybrid spell identifier (generated).
        /// </summary>
        public FixedString64Bytes HybridSpellId;

        /// <summary>
        /// First parent spell identifier.
        /// </summary>
        public FixedString64Bytes ParentSpellA;

        /// <summary>
        /// Second parent spell identifier.
        /// </summary>
        public FixedString64Bytes ParentSpellB;

        /// <summary>
        /// Entity that created this hybrid.
        /// </summary>
        public Entity CreatorEntity;

        /// <summary>
        /// Tick when hybrid was created.
        /// </summary>
        public uint CreatedTick;

        /// <summary>
        /// Derived school (may inherit from parents or create new).
        /// </summary>
        public SpellSchool DerivedSchool;

        /// <summary>
        /// Display name for the hybrid spell.
        /// </summary>
        public FixedString64Bytes DisplayName;
    }

    /// <summary>
    /// Request to hybridize two spells.
    /// Requires both spells at 400% mastery with Hybridization signature.
    /// </summary>
    public struct HybridizationRequest : IBufferElementData
    {
        /// <summary>
        /// First spell identifier.
        /// </summary>
        public FixedString64Bytes SpellA;

        /// <summary>
        /// Second spell identifier.
        /// </summary>
        public FixedString64Bytes SpellB;

        /// <summary>
        /// Tick when request was created.
        /// </summary>
        public uint RequestTick;
    }

    /// <summary>
    /// Event raised when a hybrid spell is created.
    /// </summary>
    public struct HybridSpellCreatedEvent : IBufferElementData
    {
        public FixedString64Bytes HybridSpellId;
        public FixedString64Bytes ParentSpellA;
        public FixedString64Bytes ParentSpellB;
        public Entity CreatorEntity;
        public uint CreatedTick;
    }
}

