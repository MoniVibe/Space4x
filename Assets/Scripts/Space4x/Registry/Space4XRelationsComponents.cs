using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Lightweight personal relation between two entities.
    /// </summary>
    public enum PersonalRelationKind : byte
    {
        None = 0,
        Friend = 1,
        Rival = 2,
        Family = 3,
        Mentor = 4,
        Protege = 5,
        Comrade = 6,
        Debtor = 7,
        Creditor = 8,
        BloodFeud = 9
    }

    /// <summary>
    /// Per-entity buffer of personal relations used for recognition and narrative hooks.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct PersonalRelationEntry : IBufferElementData
    {
        public Entity Other;
        public sbyte Score;
        public PersonalRelationKind Kind;
        public half Trust;
        public half Fear;
        public uint LastInteractionTick;
    }
}
