using Unity.Entities;
using Unity.Collections;

namespace PureDOTS.Runtime.Contract
{
    /// <summary>
    /// Type of contract.
    /// </summary>
    public enum ContractType : byte
    {
        Employment = 0,     // Standard work contract
        Service = 1,        // Time-limited service
        Apprenticeship = 2, // Training contract
        Military = 3,       // Military service
        Mercenary = 4,      // Combat for hire
        Guild = 5,          // Guild membership
        Partnership = 6,    // Business partnership
        Ownership = 7,      // Ownership stake
        Tenancy = 8,        // Renting/leasing
        Commission = 9      // One-time job
    }

    /// <summary>
    /// Status of a contract.
    /// </summary>
    public enum ContractStatus : byte
    {
        Negotiating = 0,
        Active = 1,
        Suspended = 2,
        Expiring = 3,       // Near end, can renew
        Expired = 4,
        Breached = 5,
        Terminated = 6,
        Completed = 7
    }

    /// <summary>
    /// Main contract component.
    /// </summary>
    public struct Contract : IComponentData
    {
        public ContractType Type;
        public ContractStatus Status;
        public Entity EmployerEntity;      // Who provides the contract
        public Entity ContractorEntity;    // Who fulfills the contract
        public uint StartTick;
        public uint EndTick;               // 0 = indefinite
        public uint LastPaymentTick;
        public float PaymentAmount;        // Per period
        public float PaymentPeriod;        // Ticks between payments
        public float OwnershipStake;       // 0-1 for ownership contracts
        public byte AutoRenew;             // Auto-renew on expiry
        public byte RequiresNotice;        // Needs advance notice to end
        public uint NoticePeriod;          // Ticks of notice required
    }

    /// <summary>
    /// Benefits provided by contract.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct ContractBenefit : IBufferElementData
    {
        public FixedString32Bytes BenefitType;
        public float Value;
        public byte IsActive;
    }

    /// <summary>
    /// Obligations under contract.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct ContractObligation : IBufferElementData
    {
        public FixedString32Bytes ObligationType;
        public float RequiredValue;
        public float CurrentValue;
        public uint DeadlineTick;
        public byte IsMet;
    }

    /// <summary>
    /// Breach record for contract.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct ContractBreach : IBufferElementData
    {
        public FixedString32Bytes BreachType;
        public float Severity;             // 0-1
        public uint OccurredTick;
        public byte WasResolved;
        public float PenaltyPaid;
    }

    /// <summary>
    /// Assignment to a specific role/location.
    /// </summary>
    public struct Assignment : IComponentData
    {
        public Entity AssignedTo;          // Entity being assigned
        public Entity Location;            // Where they're assigned
        public FixedString32Bytes Role;    // What role they fill
        public float Efficiency;           // 0-1 how well they fit
        public uint StartTick;
        public uint ScheduledEndTick;
        public byte IsTemporary;
        public byte CanReassign;           // Allowed to move elsewhere
    }

    /// <summary>
    /// Ownership stake in an entity.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct OwnershipStake : IBufferElementData
    {
        public Entity OwnerEntity;
        public float Percentage;           // 0-1 ownership %
        public float DividendsOwed;
        public uint AcquiredTick;
        public byte HasVotingRights;
        public byte CanSell;
    }

    /// <summary>
    /// Negotiation state for contracts.
    /// </summary>
    public struct ContractNegotiation : IComponentData
    {
        public Entity ProposerEntity;
        public Entity RecipientEntity;
        public ContractType ProposedType;
        public float ProposedPayment;
        public uint ProposedDuration;
        public float CounterOfferPayment;
        public uint CounterOfferDuration;
        public byte NegotiationRounds;
        public byte IsAccepted;
        public byte IsRejected;
    }
}

