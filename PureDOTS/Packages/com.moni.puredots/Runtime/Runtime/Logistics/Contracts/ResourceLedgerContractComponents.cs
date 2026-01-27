using Unity.Entities;

namespace PureDOTS.Runtime.Logistics.Contracts
{
    public enum ReservationState : byte
    {
        Held = 0,
        Committed = 1,
        Released = 2
    }

    [InternalBufferCapacity(4)]
    public struct ContractInventory : IBufferElementData
    {
        public int ResourceId;
        public int Amount;
    }

    [InternalBufferCapacity(4)]
    public struct ContractReservationRequest : IBufferElementData
    {
        public int ResourceId;
        public int Amount;
        public Entity Requester;
        public ushort Purpose;
        public uint ExpireTick;
    }

    [InternalBufferCapacity(8)]
    public struct ContractReservationLedgerEntry : IBufferElementData
    {
        public uint ReservationId;
        public int ResourceId;
        public int Amount;
        public Entity Owner;
        public ReservationState State;
        public uint ExpireTick;
        public uint CommittedTick;
        public uint LastStateTick;
    }

    public struct ContractReservationLedgerState : IComponentData
    {
        public uint NextReservationId;
    }

    public struct ContractInvariantCounters : IComponentData
    {
        public int NegativeInventoryCount;
        public int ReservedOverAvailableCount;
        public int ExpiredReservationCount;
        public int DuplicateReservationIdCount;
        public int IllegalStateTransitionCount;
        public int DoubleCommitAttemptCount;
        public int CommitWithoutHoldCount;
    }
}
