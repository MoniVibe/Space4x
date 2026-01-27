using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Spells
{
    /// <summary>
    /// Selected signature for a spell at a milestone.
    /// Entities choose one signature per milestone (200%, 300%, 400%).
    /// </summary>
    public struct SignatureSelection : IBufferElementData
    {
        /// <summary>
        /// Spell identifier.
        /// </summary>
        public FixedString64Bytes SpellId;

        /// <summary>
        /// Selected signature identifier.
        /// </summary>
        public FixedString64Bytes SignatureId;

        /// <summary>
        /// Milestone index (0=200%, 1=300%, 2=400%).
        /// </summary>
        public byte MilestoneIndex;

        /// <summary>
        /// Tick when signature was selected.
        /// </summary>
        public uint SelectedTick;
    }

    /// <summary>
    /// Request to unlock/select a signature at a milestone.
    /// Processed by SignatureUnlockSystem.
    /// </summary>
    public struct SignatureUnlockRequest : IBufferElementData
    {
        /// <summary>
        /// Spell identifier.
        /// </summary>
        public FixedString64Bytes SpellId;

        /// <summary>
        /// Signature identifier to unlock.
        /// </summary>
        public FixedString64Bytes SignatureId;

        /// <summary>
        /// Milestone index (0=200%, 1=300%, 2=400%).
        /// </summary>
        public byte MilestoneIndex;

        /// <summary>
        /// Tick when request was created.
        /// </summary>
        public uint RequestTick;
    }

    /// <summary>
    /// Event raised when a signature is unlocked.
    /// </summary>
    public struct SignatureUnlockedEvent : IBufferElementData
    {
        public FixedString64Bytes SpellId;
        public FixedString64Bytes SignatureId;
        public Entity Entity;
        public byte MilestoneIndex;
        public uint UnlockedTick;
    }
}

