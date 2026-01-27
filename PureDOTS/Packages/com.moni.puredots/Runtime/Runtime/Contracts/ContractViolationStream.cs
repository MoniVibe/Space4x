using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Contracts
{
    public enum ContractViolationReason : byte
    {
        NegativeInventory = 0,
        ReservedExceedsAvailable = 1,
        IllegalLedgerTransition = 2,
        CommitWithoutHold = 3,
        DoubleCommitAttempt = 4,
        DuplicateReservationId = 5,
        ExpiredReservation = 6
    }

    public struct ContractViolationStream : IComponentData
    {
    }

    public struct ContractViolationRingState : IComponentData
    {
        public int WriteIndex;
        public int Capacity;
    }

    [InternalBufferCapacity(128)]
    public struct ContractViolationEvent : IBufferElementData
    {
        public FixedString64Bytes ContractId;
        public uint Tick;
        public Entity Subject;
        public uint ReservationId;
        public byte Reason;
    }
}
