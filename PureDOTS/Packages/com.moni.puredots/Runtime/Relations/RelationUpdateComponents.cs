using Unity.Entities;
using Unity.Collections;
using PureDOTS.Runtime.Social;

namespace PureDOTS.Runtime.Relations
{
    /// <summary>
    /// Relation event - event that affects relations.
    /// Used for event-driven relation updates.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct RelationEvent : IBufferElementData
    {
        public Entity SourceEntity;
        public Entity TargetEntity;
        public InteractionOutcome Outcome;
        public sbyte RelationDelta;
        public uint EventTick;
    }

    /// <summary>
    /// Interaction type for categorizing interactions.
    /// Maps to InteractionOutcome for processing.
    /// </summary>
    public enum InteractionType : byte
    {
        Help = 0,
        Attack = 1,
        Betrayal = 2,
        Trade = 3,
        Communication = 4,
        Gift = 5,
        Insult = 6,
        Threat = 7
    }
}

