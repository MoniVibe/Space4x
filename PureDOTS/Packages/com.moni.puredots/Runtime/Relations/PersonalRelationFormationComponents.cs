using Unity.Entities;
using Unity.Collections;

namespace PureDOTS.Runtime.Relations
{
    /// <summary>
    /// Relation formation event - event that triggers relation formation.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct RelationFormationEvent : IBufferElementData
    {
        public Entity SourceEntity;
        public Entity TargetEntity;
        public RelationFormationEventType EventType;
        public uint EventTick;
    }

    /// <summary>
    /// Relation formation event types.
    /// </summary>
    public enum RelationFormationEventType : byte
    {
        SharedExperience = 0,
        Betrayal = 1,
        FamilyEvent = 2,
        Teaching = 3,
        Learning = 4,
        Combat = 5,
        Trade = 6
    }

    /// <summary>
    /// Relation formation trigger - what triggered the formation.
    /// </summary>
    public enum RelationFormationTrigger : byte
    {
        SharedExperience = 0,
        Betrayal = 1,
        FamilyBond = 2,
        Teaching = 3,
        Combat = 4
    }
}

