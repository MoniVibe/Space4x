using Unity.Entities;

namespace PureDOTS.Runtime.Needs.Contracts
{
    public struct ContractNeedState : IComponentData
    {
        public float Hunger;
        public float Health;
        public float Rest;
        public float Morale;
    }

    public struct ContractNeedOverrideIntent : IComponentData
    {
        public byte Active;
        public byte Reason;
        public uint LastChangedTick;
    }

    public struct ContractNeedOverridePolicy : IComponentData
    {
        public float CriticalHunger;
        public float CriticalHealth;
        public uint MinStableTicks;
    }
}
