using Unity.Entities;

namespace PureDOTS.Runtime.Vehicles
{
    // STUB: squadron/formations placeholders so fighter systems can wire up before full specs.

    public struct FighterSquadronTag : IComponentData { }

    public struct SquadronFormation : IComponentData
    {
        public int FormationId;
        public byte State; // travel/attack/defend
        public float Spread;
    }

    public struct SquadronMember : IBufferElementData
    {
        public Entity Craft;
        public byte SlotIndex;
    }

    public struct FormationSlot : IBufferElementData
    {
        public byte SlotIndex;
        public float OffsetX;
        public float OffsetY;
        public float OffsetZ;
    }

    public struct AttackRunTicket : IComponentData
    {
        public Entity Target;
        public byte Priority;
        public uint RequestedTick;
    }

    public struct AttackRunState : IComponentData
    {
        public byte Phase; // approach/commit/break
        public float Progress;
    }
}
