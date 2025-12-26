using Unity.Entities;

namespace Space4X.Runtime
{
    public enum Space4XCombatOutcomeType : byte
    {
        Destroyed = 0,
        Damaged = 1
    }

    public struct Space4XCombatOutcomeStream : IComponentData { }

    [InternalBufferCapacity(16)]
    public struct Space4XCombatOutcomeEvent : IBufferElementData
    {
        public Entity Attacker;
        public Entity Victim;
        public ushort AttackerFactionId;
        public ushort VictimFactionId;
        public Space4XCombatOutcomeType Outcome;
        public uint Tick;
    }
}
