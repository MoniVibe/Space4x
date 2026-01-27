using Unity.Entities;

namespace PureDOTS.Runtime.Narrative
{
    /// <summary>
    /// Participant entity in a situation with assigned role.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct SituationParticipant : IBufferElementData
    {
        public Entity Entity;
        public int RoleId;
        public byte IsPlayerControlled;   // 1 = direct player agents (god hand, fleet)
    }
}

