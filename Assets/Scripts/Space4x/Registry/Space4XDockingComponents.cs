using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Types of docking slots available on carriers.
    /// </summary>
    public enum DockingSlotType : byte
    {
        /// <summary>
        /// Small craft: fighters, scouts, drones.
        /// </summary>
        SmallCraft = 0,

        /// <summary>
        /// Medium craft: corvettes, gunboats, shuttles.
        /// </summary>
        MediumCraft = 1,

        /// <summary>
        /// Large craft: frigates, destroyers.
        /// </summary>
        LargeCraft = 2,

        /// <summary>
        /// External mooring for very large vessels.
        /// </summary>
        ExternalMooring = 3,

        /// <summary>
        /// Utility slots: mining vessels, repair drones, logistics.
        /// </summary>
        Utility = 4
    }

    /// <summary>
    /// Docking capacity for a carrier.
    /// </summary>
    public struct DockingCapacity : IComponentData
    {
        /// <summary>
        /// Maximum small craft slots.
        /// </summary>
        public byte MaxSmallCraft;

        /// <summary>
        /// Maximum medium craft slots.
        /// </summary>
        public byte MaxMediumCraft;

        /// <summary>
        /// Maximum large craft slots.
        /// </summary>
        public byte MaxLargeCraft;

        /// <summary>
        /// Maximum external mooring slots.
        /// </summary>
        public byte MaxExternalMooring;

        /// <summary>
        /// Maximum utility slots.
        /// </summary>
        public byte MaxUtility;

        /// <summary>
        /// Current small craft docked.
        /// </summary>
        public byte CurrentSmallCraft;

        /// <summary>
        /// Current medium craft docked.
        /// </summary>
        public byte CurrentMediumCraft;

        /// <summary>
        /// Current large craft docked.
        /// </summary>
        public byte CurrentLargeCraft;

        /// <summary>
        /// Current external mooring used.
        /// </summary>
        public byte CurrentExternalMooring;

        /// <summary>
        /// Current utility slots used.
        /// </summary>
        public byte CurrentUtility;

        /// <summary>
        /// Total docking capacity.
        /// </summary>
        public int TotalCapacity => MaxSmallCraft + MaxMediumCraft + MaxLargeCraft + MaxExternalMooring + MaxUtility;

        /// <summary>
        /// Total currently docked.
        /// </summary>
        public int TotalDocked => CurrentSmallCraft + CurrentMediumCraft + CurrentLargeCraft + CurrentExternalMooring + CurrentUtility;

        /// <summary>
        /// Overall utilization ratio [0, 1+].
        /// </summary>
        public float Utilization => TotalCapacity > 0 ? (float)TotalDocked / TotalCapacity : 0f;

        public bool HasSlotAvailable(DockingSlotType type)
        {
            return type switch
            {
                DockingSlotType.SmallCraft => CurrentSmallCraft < MaxSmallCraft,
                DockingSlotType.MediumCraft => CurrentMediumCraft < MaxMediumCraft,
                DockingSlotType.LargeCraft => CurrentLargeCraft < MaxLargeCraft,
                DockingSlotType.ExternalMooring => CurrentExternalMooring < MaxExternalMooring,
                DockingSlotType.Utility => CurrentUtility < MaxUtility,
                _ => false
            };
        }

        public static DockingCapacity LightCarrier => new DockingCapacity
        {
            MaxSmallCraft = 12,
            MaxMediumCraft = 4,
            MaxLargeCraft = 0,
            MaxExternalMooring = 2,
            MaxUtility = 4
        };

        public static DockingCapacity HeavyCarrier => new DockingCapacity
        {
            MaxSmallCraft = 24,
            MaxMediumCraft = 8,
            MaxLargeCraft = 2,
            MaxExternalMooring = 4,
            MaxUtility = 6
        };

        public static DockingCapacity SuperCarrier => new DockingCapacity
        {
            MaxSmallCraft = 48,
            MaxMediumCraft = 16,
            MaxLargeCraft = 6,
            MaxExternalMooring = 8,
            MaxUtility = 10
        };

        public static DockingCapacity MiningCarrier => new DockingCapacity
        {
            MaxSmallCraft = 4,
            MaxMediumCraft = 2,
            MaxLargeCraft = 0,
            MaxExternalMooring = 2,
            MaxUtility = 12 // Emphasis on utility/mining
        };
    }

    /// <summary>
    /// Crew capacity constraints for a carrier.
    /// </summary>
    public struct CrewCapacity : IComponentData
    {
        /// <summary>
        /// Maximum comfortable crew capacity.
        /// </summary>
        public int MaxCrew;

        /// <summary>
        /// Current crew count.
        /// </summary>
        public int CurrentCrew;

        /// <summary>
        /// Absolute maximum before critical overcrowding.
        /// </summary>
        public int CriticalMax;

        /// <summary>
        /// Crew ratio [0, 2+]. Above 1 = overcrowded.
        /// </summary>
        public float CrewRatio => MaxCrew > 0 ? (float)CurrentCrew / MaxCrew : 0f;

        /// <summary>
        /// Whether critically overcrowded.
        /// </summary>
        public bool IsCriticallyOvercrowded => CriticalMax > 0 && CurrentCrew > CriticalMax;

        /// <summary>
        /// Whether overcrowded (morale penalty).
        /// </summary>
        public bool IsOvercrowded => CurrentCrew > MaxCrew;

        /// <summary>
        /// Available crew slots.
        /// </summary>
        public int AvailableSlots => math.max(0, MaxCrew - CurrentCrew);

        public static CrewCapacity Create(int maxCrew, int criticalMultiplier = 150)
        {
            return new CrewCapacity
            {
                MaxCrew = maxCrew,
                CurrentCrew = 0,
                CriticalMax = (maxCrew * criticalMultiplier) / 100
            };
        }

        public static CrewCapacity LightCarrier => Create(200);
        public static CrewCapacity HeavyCarrier => Create(500);
        public static CrewCapacity SuperCarrier => Create(1200);
    }

    /// <summary>
    /// Command load for fleet coordination.
    /// </summary>
    public struct CommandLoad : IComponentData
    {
        /// <summary>
        /// Maximum command points available.
        /// </summary>
        public int MaxCommandPoints;

        /// <summary>
        /// Current command points used.
        /// </summary>
        public int CurrentCommandPoints;

        /// <summary>
        /// Command point regeneration rate per second.
        /// </summary>
        public half RegenRate;

        /// <summary>
        /// Command utilization ratio [0, 1+].
        /// </summary>
        public float Utilization => MaxCommandPoints > 0 ? (float)CurrentCommandPoints / MaxCommandPoints : 0f;

        /// <summary>
        /// Available command points.
        /// </summary>
        public int Available => math.max(0, MaxCommandPoints - CurrentCommandPoints);

        /// <summary>
        /// Whether command is overloaded.
        /// </summary>
        public bool IsOverloaded => CurrentCommandPoints > MaxCommandPoints;

        public static CommandLoad Create(int maxPoints, float regenRate = 1f)
        {
            return new CommandLoad
            {
                MaxCommandPoints = maxPoints,
                CurrentCommandPoints = 0,
                RegenRate = (half)regenRate
            };
        }

        public static CommandLoad LightCarrier => Create(10, 0.5f);
        public static CommandLoad HeavyCarrier => Create(25, 1f);
        public static CommandLoad SuperCarrier => Create(50, 2f);
    }

    /// <summary>
    /// Entity currently docked at a carrier.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct DockedEntity : IBufferElementData
    {
        /// <summary>
        /// The docked entity.
        /// </summary>
        public Entity Entity;

        /// <summary>
        /// Slot type occupied.
        /// </summary>
        public DockingSlotType SlotType;

        /// <summary>
        /// Tick when docking occurred.
        /// </summary>
        public uint DockedTick;

        /// <summary>
        /// Command points consumed by this entity.
        /// </summary>
        public byte CommandPointCost;
    }

    /// <summary>
    /// Docking request from a vessel.
    /// </summary>
    public struct DockingRequest : IComponentData
    {
        /// <summary>
        /// Target carrier to dock at.
        /// </summary>
        public Entity TargetCarrier;

        /// <summary>
        /// Required slot type.
        /// </summary>
        public DockingSlotType RequiredSlot;

        /// <summary>
        /// Tick when request was made.
        /// </summary>
        public uint RequestTick;

        /// <summary>
        /// Priority (lower = higher priority).
        /// </summary>
        public byte Priority;
    }

    /// <summary>
    /// Tag indicating an entity is currently docked.
    /// </summary>
    public struct DockedTag : IComponentData
    {
        /// <summary>
        /// Carrier this entity is docked at.
        /// </summary>
        public Entity CarrierEntity;

        /// <summary>
        /// Slot index within the carrier.
        /// </summary>
        public byte SlotIndex;
    }

    public enum DockingPhase : byte
    {
        None = 0,
        Approaching = 1,
        Docking = 2,
        Docked = 3,
        Undocking = 4,
        Departed = 5
    }

    public enum DockingPresenceMode : byte
    {
        Latch = 0,
        Attach = 1,
        Despawn = 2
    }

    /// <summary>
    /// Docking lifecycle state for a vessel.
    /// </summary>
    public struct DockingState : IComponentData
    {
        public DockingPhase Phase;
        public Entity Target;
        public DockingSlotType SlotType;
        public DockingPresenceMode PresenceMode;
        public uint RequestTick;
        public uint PhaseTick;
    }

    /// <summary>
    /// Docking throughput and policy settings for a carrier/station.
    /// </summary>
    public struct DockingPolicy : IComponentData
    {
        public DockingPresenceMode DefaultPresence;
        public float CrewTransferPerTick;
        public float CargoTransferPerTick;
        public float AmmoTransferPerTick;
        public float DockingRange;
        public byte AllowRendezvous;
        public byte AllowDocking;
        public byte AllowDespawn;

        public static DockingPolicy Default => new DockingPolicy
        {
            DefaultPresence = DockingPresenceMode.Latch,
            CrewTransferPerTick = 8f,
            CargoTransferPerTick = 25f,
            AmmoTransferPerTick = 40f,
            DockingRange = 4.5f,
            AllowRendezvous = 0,
            AllowDocking = 1,
            AllowDespawn = 0
        };

        public static DockingPolicy StationDefault => new DockingPolicy
        {
            DefaultPresence = DockingPresenceMode.Despawn,
            CrewTransferPerTick = 16f,
            CargoTransferPerTick = 60f,
            AmmoTransferPerTick = 80f,
            DockingRange = 6f,
            AllowRendezvous = 0,
            AllowDocking = 1,
            AllowDespawn = 1
        };
    }

    /// <summary>
    /// Per-host queue policy for docking request throughput.
    /// </summary>
    public struct DockingQueuePolicy : IComponentData
    {
        /// <summary>
        /// Max successful dock operations processed per tick.
        /// </summary>
        public byte MaxProcessedPerTick;

        public static DockingQueuePolicy Default => new DockingQueuePolicy
        {
            MaxProcessedPerTick = 1
        };

        public static DockingQueuePolicy StationDefault => new DockingQueuePolicy
        {
            MaxProcessedPerTick = 2
        };
    }

    /// <summary>
    /// Rolling queue counters for the current simulation tick.
    /// </summary>
    public struct DockingQueueState : IComponentData
    {
        public uint LastTick;
        public ushort PendingRequests;
        public ushort ProcessedRequests;
    }

    /// <summary>
    /// Per-tick throughput remaining for docking transfers.
    /// </summary>
    public struct DockingThroughputState : IComponentData
    {
        public float CrewRemaining;
        public float CargoRemaining;
        public float AmmoRemaining;
        public uint LastResetTick;
    }

    /// <summary>
    /// Presence/attachment hints for docked vessels.
    /// </summary>
    public struct DockedPresence : IComponentData
    {
        public Entity Carrier;
        public DockingPresenceMode Mode;
        public float3 LatchOffset;
        public byte IsLatched;
    }

    /// <summary>
    /// Utility functions for docking calculations.
    /// </summary>
    public static class DockingUtility
    {
        /// <summary>
        /// Gets overcrowding morale penalty.
        /// </summary>
        public static float GetOvercrowdingPenalty(in CrewCapacity capacity)
        {
            if (!capacity.IsOvercrowded)
            {
                return 0f;
            }

            float excess = capacity.CrewRatio - 1f;
            if (capacity.IsCriticallyOvercrowded)
            {
                return math.min(0.5f, excess * 0.5f); // Up to 50% penalty
            }
            return math.min(0.2f, excess * 0.2f); // Up to 20% penalty
        }

        /// <summary>
        /// Gets command overload efficiency penalty.
        /// </summary>
        public static float GetCommandOverloadPenalty(in CommandLoad load)
        {
            if (!load.IsOverloaded)
            {
                return 0f;
            }

            float excess = load.Utilization - 1f;
            return math.min(0.3f, excess * 0.3f); // Up to 30% penalty
        }

        /// <summary>
        /// Calculates command point cost for a vessel type.
        /// </summary>
        public static int GetCommandPointCost(DockingSlotType slotType)
        {
            return slotType switch
            {
                DockingSlotType.SmallCraft => 1,
                DockingSlotType.MediumCraft => 2,
                DockingSlotType.LargeCraft => 4,
                DockingSlotType.ExternalMooring => 3,
                DockingSlotType.Utility => 1,
                _ => 1
            };
        }
    }
}
