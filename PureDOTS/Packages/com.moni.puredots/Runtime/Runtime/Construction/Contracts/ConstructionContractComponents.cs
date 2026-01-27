using Unity.Entities;

namespace PureDOTS.Runtime.Construction.Contracts
{
    public enum ContractConstructionState : byte
    {
        Planned = 0,
        Reserved = 1,
        Building = 2,
        Complete = 3,
        Cancelled = 4
    }

    [InternalBufferCapacity(2)]
    public struct ContractConstructionRequirement : IBufferElementData
    {
        public int ResourceId;
        public int Amount;
    }

    public struct ContractConstructionSite : IComponentData
    {
        public ContractConstructionState State;
        public uint StateTick;
    }

    public struct ContractConstructionCancel : IComponentData
    {
    }
}
