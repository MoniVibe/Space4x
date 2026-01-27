using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Extraction
{
    /// <summary>
    /// Attachment points for harvesters on a resource source.
    /// Enables multiple workers to gather from the same source at different positions.
    /// Game-agnostic: works for trees (Godgame), asteroids (Space4X), or any extractable resource.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct HarvestSlot : IBufferElementData
    {
        /// <summary>
        /// Local offset from source entity position where harvester should stand.
        /// </summary>
        public float3 LocalOffset;

        /// <summary>
        /// Agent currently assigned to this slot (or Entity.Null if empty).
        /// </summary>
        public Entity AssignedAgent;

        /// <summary>
        /// Tick when slot was last actively harvested.
        /// </summary>
        public uint LastHarvestTick;

        /// <summary>
        /// Slot-specific efficiency modifier (1.0 = normal, 0.5 = half speed).
        /// Allows some slots to be better than others (e.g., shaded side of tree).
        /// </summary>
        public float EfficiencyMultiplier;

        /// <summary>
        /// Slot flags for special behavior.
        /// </summary>
        public byte Flags;

        /// <summary>
        /// Creates a default slot at the given offset.
        /// </summary>
        public static HarvestSlot Create(float3 localOffset, float efficiency = 1f)
        {
            return new HarvestSlot
            {
                LocalOffset = localOffset,
                AssignedAgent = Entity.Null,
                LastHarvestTick = 0,
                EfficiencyMultiplier = efficiency,
                Flags = 0
            };
        }
    }

    /// <summary>
    /// Flags for harvest slot behavior.
    /// </summary>
    public static class HarvestSlotFlags
    {
        /// <summary>Slot is temporarily blocked (e.g., obstacle).</summary>
        public const byte Blocked = 1 << 0;
        /// <summary>Slot requires special tool or skill.</summary>
        public const byte RequiresSpecialist = 1 << 1;
        /// <summary>Slot is reserved but agent hasn't arrived yet.</summary>
        public const byte PendingArrival = 1 << 2;
    }

    /// <summary>
    /// Queued extraction request for deterministic processing.
    /// Requests are sorted by (Priority, RequestTick, EntityIndex) for deterministic ordering.
    /// </summary>
    public struct ExtractionRequest : IBufferElementData
    {
        /// <summary>
        /// Source entity to extract from.
        /// </summary>
        public Entity SourceEntity;

        /// <summary>
        /// Agent requesting extraction.
        /// </summary>
        public Entity AgentEntity;

        /// <summary>
        /// Amount requested (may be capped by source availability).
        /// </summary>
        public float RequestedAmount;

        /// <summary>
        /// Tick when request was submitted.
        /// </summary>
        public uint RequestTick;

        /// <summary>
        /// Priority level (higher = processed first).
        /// </summary>
        public byte Priority;

        /// <summary>
        /// Request status.
        /// </summary>
        public ExtractionRequestStatus Status;

        /// <summary>
        /// Slot index assigned (-1 if not yet assigned).
        /// </summary>
        public sbyte AssignedSlotIndex;
    }

    /// <summary>
    /// Status of an extraction request.
    /// </summary>
    public enum ExtractionRequestStatus : byte
    {
        /// <summary>Request is pending assignment.</summary>
        Pending = 0,
        /// <summary>Request has been assigned a slot.</summary>
        Assigned = 1,
        /// <summary>Agent is actively extracting.</summary>
        InProgress = 2,
        /// <summary>Extraction completed successfully.</summary>
        Completed = 3,
        /// <summary>Request was cancelled or failed.</summary>
        Cancelled = 4
    }

    /// <summary>
    /// Singleton tag for the extraction request queue entity.
    /// </summary>
    public struct ExtractionRequestQueue : IComponentData
    {
        /// <summary>
        /// Total pending requests this tick.
        /// </summary>
        public int PendingCount;

        /// <summary>
        /// Total active extractions this tick.
        /// </summary>
        public int ActiveCount;

        /// <summary>
        /// Last tick the queue was processed.
        /// </summary>
        public uint LastProcessedTick;
    }

    /// <summary>
    /// Configuration for extraction request processing.
    /// </summary>
    public struct ExtractionConfig : IComponentData
    {
        /// <summary>
        /// Maximum requests to process per tick.
        /// </summary>
        public int MaxRequestsPerTick;

        /// <summary>
        /// Maximum distance for slot assignment (0 = unlimited).
        /// </summary>
        public float MaxAssignmentDistance;

        /// <summary>
        /// Seconds before a pending request expires.
        /// </summary>
        public float RequestTimeoutSeconds;

        /// <summary>
        /// Creates default configuration.
        /// </summary>
        public static ExtractionConfig Default => new ExtractionConfig
        {
            MaxRequestsPerTick = 64,
            MaxAssignmentDistance = 0f, // Unlimited
            RequestTimeoutSeconds = 30f
        };
    }

    /// <summary>
    /// Extraction telemetry for debugging and balancing.
    /// </summary>
    public struct ExtractionTelemetry : IComponentData
    {
        /// <summary>
        /// Total sources with harvest slots.
        /// </summary>
        public int TotalSources;

        /// <summary>
        /// Total slots across all sources.
        /// </summary>
        public int TotalSlots;

        /// <summary>
        /// Currently occupied slots.
        /// </summary>
        public int OccupiedSlots;

        /// <summary>
        /// Requests submitted this tick.
        /// </summary>
        public int RequestsSubmitted;

        /// <summary>
        /// Requests assigned this tick.
        /// </summary>
        public int RequestsAssigned;

        /// <summary>
        /// Requests completed this tick.
        /// </summary>
        public int RequestsCompleted;

        /// <summary>
        /// Requests timed out this tick.
        /// </summary>
        public int RequestsTimedOut;

        /// <summary>
        /// Last update tick.
        /// </summary>
        public uint LastUpdateTick;
    }
}

