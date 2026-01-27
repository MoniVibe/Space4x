using Unity.Entities;

namespace PureDOTS.Runtime.Relations
{
    /// <summary>
    /// Personal relationship type.
    /// </summary>
    public enum PersonalRelationType : byte
    {
        None = 0,
        Family = 1,        // Family member
        Comrade = 2,       // Same band/squad/ship crew
        Friend = 3,        // Close friend
        Rival = 4,         // Explicit rival
        Enemy = 5,         // Explicit enemy
        Companion = 6,     // Traveling companion
        Lover = 7,         // Romantic partner
        Spouse = 8         // Married partner
    }

    /// <summary>
    /// Personal relation entry - bounded buffer for important personal relationships.
    /// Only stores explicit edges for family, comrades, friends, rivals, enemies.
    /// All others are derived on-the-fly from group membership/culture compatibility.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct PersonalRelation : IBufferElementData
    {
        /// <summary>
        /// Entity this relation is with.
        /// </summary>
        public Entity TargetEntity;

        /// <summary>
        /// Type of relationship.
        /// </summary>
        public PersonalRelationType RelationType;

        /// <summary>
        /// Relationship strength (-100..+100).
        /// Positive = friendly, negative = hostile.
        /// </summary>
        public float Strength;

        /// <summary>
        /// Trust level (0..1).
        /// </summary>
        public float Trust;

        /// <summary>
        /// Tick when this relation was last updated.
        /// </summary>
        public uint LastUpdateTick;
    }
}

