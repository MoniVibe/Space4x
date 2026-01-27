using Unity.Entities;

namespace PureDOTS.Runtime.AI.Contracts
{
    public enum AIContractExecutionPhase : byte
    {
        Idle = 0,
        Running = 1,
        Success = 2,
        Failed = 3,
        Interrupted = 4
    }

    public struct AIContractIntent : IComponentData
    {
        public byte IntentId;
        public byte Priority;
        public Entity Issuer;
        public uint LastUpdatedTick;
    }

    public struct AIContractActionSelectionState : IComponentData
    {
        public byte ActionId;
        public uint ChosenTick;
    }

    public struct AIContractActionExecutionState : IComponentData
    {
        public byte ActionId;
        public AIContractExecutionPhase Phase;
        public uint PhaseStartTick;
        public uint LastTransitionTick;
        public byte FailureCode;
        public uint LastInterruptedTick;
    }

    public struct AIContractInterruptRequest : IComponentData
    {
        public byte Reason;
        public byte Priority;
        public uint RequestedTick;
    }

    public struct AIContractRecoveryState : IComponentData
    {
        public uint CooldownUntilTick;
        public byte RetryBudget;
        public uint LastRecoveryTick;
    }
}
