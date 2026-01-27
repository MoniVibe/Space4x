// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
#if PUREDOTS_STUBS
using Unity.Entities;

namespace PureDOTS.Runtime.Vehicles
{
    public struct FighterSquadronTag : IComponentData { }

    public struct SquadronFormation : IComponentData
    {
        public int FormationId;
        public byte State;
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
        public byte Phase;
        public float Progress;
    }
}
#endif
