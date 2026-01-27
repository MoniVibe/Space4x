using Unity.Entities;

namespace PureDOTS.Runtime.AI.WorldFacts
{
    /// <summary>
    /// A single world fact for an entity.
    /// Facts are stored in a buffer and updated by WorldFactsBuilderSystem.
    /// </summary>
    [InternalBufferCapacity(32)]
    public struct WorldFact : IBufferElementData
    {
        /// <summary>
        /// Typed fact key (not a string).
        /// </summary>
        public WorldFactKey Key;

        /// <summary>
        /// Fact value (0.0 = false, 1.0 = true, or numeric value).
        /// </summary>
        public float Value;

        /// <summary>
        /// Where this fact came from.
        /// </summary>
        public WorldFactProvenance Provenance;

        /// <summary>
        /// Tick when fact was last updated.
        /// </summary>
        public uint LastUpdatedTick;

        /// <summary>
        /// Whether this fact is currently valid (may be stale).
        /// </summary>
        public byte IsValid;
    }
}



