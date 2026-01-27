using Unity.Entities;

namespace PureDOTS.Runtime.Production.Contracts
{
    public struct ContractQualityRequest : IComponentData
    {
        public float InputA;
        public float InputB;
        public float WeightA;
        public float WeightB;
        public float MinValue;
        public float MaxValue;
        public uint LastProcessedTick;
    }

    public struct ContractQualityResult : IComponentData
    {
        public float OutputValue;
        public uint LastProcessedTick;
    }
}
