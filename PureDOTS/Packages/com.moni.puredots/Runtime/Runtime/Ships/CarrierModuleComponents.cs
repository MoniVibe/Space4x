using System;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Ships
{
    public enum MountType : byte
    {
        MainGun = 0,
        MissileRack = 1,
        PointDefense = 2,
        UtilityBay = 3,
        Spinal = 4
    }

    public enum MountSize : byte
    {
        Small = 0,
        Medium = 1,
        Large = 2,
        Spinal = 3
    }

    public enum ModuleFamily : byte
    {
        Weapon = 0,
        Defense = 1,
        Utility = 2,
        Facility = 3,
        Colony = 4
    }

    public enum ModuleClass : byte
    {
        BeamCannon = 0,
        MassDriver = 1,
        Missile = 2,
        PointDefense = 3,
        Shield = 10,
        Armor = 11,
        Engine = 20,
        Sensor = 21,
        Cargo = 22,
        Fabrication = 30,
        Research = 31,
        Medical = 32,
        Hangar = 33,
        Habitation = 40,
        Agriculture = 41,
        Mining = 42,
        Terraforming = 43,
        Administration = 44
    }

    public enum ModuleState : byte
    {
        Offline = 0,
        Standby = 1,
        Active = 2,
        Damaged = 3,
        Destroyed = 4
    }

    public enum ModuleHealthState : byte
    {
        Nominal = 0,
        Degraded = 1,
        Failed = 2,
        Destroyed = 3
    }

    [Flags]
    public enum ModuleHealthFlags : byte
    {
        None = 0,
        PendingRepairQueue = 1 << 0,
        RequiresStation = 1 << 1,
        Critical = 1 << 2
    }

    public enum ModuleRepairKind : byte
    {
        Field = 0,
        Station = 1
    }

    public enum ModuleRefitStatus : byte
    {
        Pending = 0,
        InProgress = 1
    }

    /// <summary>
    /// Base data for a module entity. Modules are standalone entities referenced by carrier slots.
    /// </summary>
    public struct ShipModule : IComponentData
    {
        public FixedString64Bytes ModuleId;
        public ModuleFamily Family;
        public ModuleClass Class;
        public MountType RequiredMount;
        public MountSize RequiredSize;
        public float Mass;
        public float PowerRequired;
        public float OffenseRating;
        public float DefenseRating;
        public float UtilityRating;
        public byte EfficiencyPercent;
        public ModuleState State;
    }

    /// <summary>
    /// Slot definition for a carrier or capital ship. Holds the installed module entity.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct CarrierModuleSlot : IBufferElementData
    {
        public MountType Type;
        public MountSize Size;
        public Entity InstalledModule;
        public byte Priority;
    }

    /// <summary>
    /// Aggregated module statistics per carrier for HUD/telemetry consumption.
    /// </summary>
    public struct CarrierModuleAggregate : IComponentData
    {
        public float TotalMass;
        public float TotalPowerRequired;
        public float OffenseRating;
        public float DefenseRating;
        public float UtilityRating;
        public float EfficiencyScalar;
        public byte DegradedCount;
        public byte FailedCount;
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Health state tracked for each module entity.
    /// </summary>
    public struct ModuleHealth : IComponentData
    {
        public float MaxHealth;
        public float Health;
        public float DegradationPerTick;
        public float FailureThreshold;
        public ModuleHealthState State;
        public ModuleHealthFlags Flags;
        public uint LastProcessedTick;
    }

    /// <summary>
    /// Operational state published by systems that drive module load (combat, hauling, etc).
    /// </summary>
    public struct ModuleOperationalState : IComponentData
    {
        public byte IsOnline;
        public byte InCombat;
        public float LoadFactor;
    }

    /// <summary>
    /// Reference back to the owning carrier so maintenance systems can enqueue repair/refit work.
    /// </summary>
    public struct CarrierOwner : IComponentData
    {
        public Entity Carrier;
    }

    /// <summary>
    /// Ticket representing a repair action queued on the carrier.
    /// Higher Priority wins; ties break on Severity then request tick.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct ModuleRepairTicket : IBufferElementData
    {
        public Entity Module;
        public ModuleRepairKind Kind;
        public byte Priority;
        public float Severity;
        public uint RequestedTick;
    }

    /// <summary>
    /// Per-carrier repair tuning.
    /// </summary>
    public struct ModuleRepairSettings : IComponentData
    {
        public byte MaxConcurrent;
        public float FieldRepairRate;
        public float StationRepairRate;
        public byte AllowFieldRepairs;

        public static ModuleRepairSettings CreateDefaults()
        {
            return new ModuleRepairSettings
            {
                MaxConcurrent = 2,
                FieldRepairRate = 2f,
                StationRepairRate = 6f,
                AllowFieldRepairs = 1
            };
        }
    }

    /// <summary>
    /// Active refit request queued on a carrier slot.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct ModuleRefitRequest : IBufferElementData
    {
        public byte SlotIndex;
        public Entity NewModule;
        public ModuleRefitStatus Status;
        public uint StartedTick;
        public uint ReadyTick;
        public ModuleRepairKind Kind;
    }

    /// <summary>
    /// Tuning for refit throughput and duration.
    /// </summary>
    public struct CarrierRefitSettings : IComponentData
    {
        public float BaseRefitDurationTicks;
        public float MassDurationFactor;
        public byte MaxConcurrent;
        public byte AllowFieldRefit;

        public static CarrierRefitSettings CreateDefaults()
        {
            return new CarrierRefitSettings
            {
                BaseRefitDurationTicks = 30f,
                MassDurationFactor = 0.15f,
                MaxConcurrent = 1,
                AllowFieldRefit = 0
            };
        }
    }

    /// <summary>
    /// Tracks whether the carrier is docked at a refit-capable facility.
    /// </summary>
    public struct CarrierRefitState : IComponentData
    {
        public byte InRefitFacility;
        public float SpeedMultiplier;
    }
}
