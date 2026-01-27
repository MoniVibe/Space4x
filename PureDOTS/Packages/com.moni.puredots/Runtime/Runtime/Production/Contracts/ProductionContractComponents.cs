using Unity.Entities;

namespace PureDOTS.Runtime.Production.Contracts
{
    public struct ContractProductionRequest : IComponentData
    {
        public int InputResourceId;
        public int InputAmount;
        public int OutputResourceId;
        public int OutputAmount;
        public ushort RecipeId;
        public uint LastProcessedTick;
    }

    public struct ContractProductionResult : IComponentData
    {
        public byte Success;
        public ushort FailureReason;
        public int ConsumedAmount;
        public int ProducedAmount;
        public uint LastProcessedTick;
    }
}
