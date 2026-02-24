using Unity.Collections;
using Unity.Entities;

namespace Space4X.Registry
{
    /// <summary>
    /// High-level facility type for production/runtime routing.
    /// </summary>
    public enum Space4XProductionFacilityKind : byte
    {
        Unknown = 0,
        Refinery = 1,
        Fabricator = 2,
        Shipyard = 3,
        HangarWorks = 4,
        ModuleWorks = 5
    }

    /// <summary>
    /// Origin of a production request in run/runtime terms.
    /// </summary>
    public enum Space4XProductionRequestSource : byte
    {
        Unknown = 0,
        Mission = 1,
        Market = 2,
        Salvage = 3,
        Loot = 4
    }

    /// <summary>
    /// Current lifecycle state for a queued production entry.
    /// </summary>
    public enum Space4XProductionEntryState : byte
    {
        Queued = 0,
        Running = 1,
        Blocked = 2,
        Completed = 3,
        Failed = 4
    }

    /// <summary>
    /// Runtime header for a facility's production loop.
    /// </summary>
    public struct Space4XProductionRuntime : IComponentData
    {
        public Space4XProductionFacilityKind FacilityKind;
        public byte QueueCapacity;
        public byte IsEnabled;
        public uint LastUpdatedTick;
    }

    /// <summary>
    /// Per-facility power/crew gating and assignments.
    /// </summary>
    public struct Space4XProductionPowerCrewConstraint : IComponentData
    {
        public float RequiredPowerMw;
        public float RequiredCrew;
        public float AssignedPowerMw;
        public float AssignedCrew;
    }

    /// <summary>
    /// Queue/runtime status summary for UI and diagnostics.
    /// </summary>
    public struct Space4XProductionStatus : IComponentData
    {
        public int ActiveQueueIndex; // -1 when idle
        public float ActiveEtaSeconds;
        public byte IsBlocked;
        public float SeatFill01;
        public float SkillFactor01;
        public float EffectiveThroughput;
    }

    /// <summary>
    /// Production queue entries. Supports recipe and/or blueprint-backed work.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct Space4XProductionQueueEntry : IBufferElementData
    {
        public FixedString64Bytes EntryId;
        public FixedString64Bytes RecipeId;
        public FixedString64Bytes BlueprintId;
        public Space4XProductionRequestSource Source;
        public Space4XProductionEntryState State;
        public byte Priority; // lower = higher priority
        public int BatchCount;
        public float EtaSeconds;
        public float RequiredPowerMw;
        public float RequiredCrew;
        public uint QueuedTick;
        public uint StartedTick;
    }

    /// <summary>
    /// Input lines associated with production queue entries.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct Space4XProductionInputLine : IBufferElementData
    {
        public FixedString64Bytes EntryId;
        public FixedString64Bytes ResourceOrPartId;
        public float RequiredAmount;
        public float ReservedAmount;
    }

    /// <summary>
    /// Output lines associated with production queue entries.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct Space4XProductionOutputLine : IBufferElementData
    {
        public FixedString64Bytes EntryId;
        public FixedString64Bytes ProductId;
        public float PlannedAmount;
        public float ProducedAmount;
    }
}
