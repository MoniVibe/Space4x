using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Platform
{
    /// <summary>
    /// Tag component identifying a platform entity.
    /// </summary>
    public struct PlatformTag : IComponentData { }

    /// <summary>
    /// Platform classification flags. Platforms can have multiple flags (e.g., Capital | IsCarrier | IsColonyCapable).
    /// </summary>
    [System.Flags]
    public enum PlatformFlags : uint
    {
        Station = 1 << 0,
        Capital = 1 << 1,
        RegularShip = 1 << 2,
        Craft = 1 << 3,
        Drone = 1 << 4,
        Probe = 1 << 5,
        NanoSwarm = 1 << 6,

        HasHangar = 1 << 10,
        IsCarrier = 1 << 11,
        IsColonyCapable = 1 << 12,
        IsIndustrial = 1 << 13,
        IsCivilian = 1 << 14,
        IsMilitary = 1 << 15,
        IsDisposable = 1 << 16,
        WarpRelay = 1 << 20
    }

    /// <summary>
    /// Layout mode for platform module installation. Determined by hull archetype.
    /// </summary>
    public enum PlatformLayoutMode : byte
    {
        MassOnly = 0,
        Hardpoint = 1,
        VoxelHull = 2
    }

    /// <summary>
    /// Module slot state.
    /// </summary>
    public enum ModuleSlotState : byte
    {
        Installed = 0,
        Offline = 1,
        Damaged = 2,
        Destroyed = 3
    }

    /// <summary>
    /// Platform kind classification.
    /// </summary>
    public struct PlatformKind : IComponentData
    {
        public PlatformFlags Flags;
    }

    /// <summary>
    /// Reference to hull definition blob asset.
    /// </summary>
    public struct PlatformHullRef : IComponentData
    {
        public int HullId;
    }

    /// <summary>
    /// Segment status flags.
    /// </summary>
    [System.Flags]
    public enum SegmentStatusFlags : ushort
    {
        Intact = 1 << 0,
        Destroyed = 1 << 1,
        Detached = 1 << 2,
        Breached = 1 << 3,
        OnFire = 1 << 4,
        Depressurized = 1 << 5,
        ReactorPresent = 1 << 6,
        ReactorCritical = 1 << 7,
        Boarded = 1 << 8,
        ConnectedToCore = 1 << 9
    }

    /// <summary>
    /// Module damage flags.
    /// </summary>
    [System.Flags]
    public enum ModuleDamageFlags : ushort
    {
        None = 0,
        Disabled = 1 << 0,
        OnFire = 1 << 1,
        Breached = 1 << 2,
        Overloaded = 1 << 3,
        Hacked = 1 << 4
    }

    /// <summary>
    /// Unified module slot representation. Used for all layout modes.
    /// </summary>
    public struct PlatformModuleSlot : IBufferElementData
    {
        public int ModuleId;
        public short SlotIndex;
        public int CellIndex;
        public short SegmentIndex;
        public byte IsExternal;
        public ModuleSlotState State;
    }

    /// <summary>
    /// Aggregated stats computed from installed modules.
    /// </summary>
    public struct PlatformAggregatedStats : IComponentData
    {
        public float TotalMass;
        public float MaxThrust;
        public float MaxHP;
        public float ShieldStrength;
        public float ShieldCoverage;
        public float HangarCapacity;
        public float PowerConsumed;
        public float PowerGenerated;
    }

    /// <summary>
    /// Crew member assignment to platform.
    /// </summary>
    public struct PlatformCrewMember : IBufferElementData
    {
        public Entity CrewEntity;
        public int RoleId;
    }

    /// <summary>
    /// Manufacturer information affecting base quality.
    /// </summary>
    public struct PlatformManufacturer : IComponentData
    {
        public int ManufacturerId;
        public byte BaseQualityTier;
        public byte TechTier;
    }

    /// <summary>
    /// Tuning state affecting reliability and performance.
    /// </summary>
    public struct PlatformTuningState : IComponentData
    {
        public float Reliability;
        public float PerformanceFactor;
        public float MaintenanceDebt;
        public float WearLevel;
    }

    /// <summary>
    /// Pilot preferences for accepting craft assignments.
    /// </summary>
    public struct PlatformPilotPreference : IComponentData
    {
        public float MinReliability;
        public float MinPerformance;
        public byte WillFlyIfBelow;
    }

    /// <summary>
    /// Hangar bay definition on carrier platforms.
    /// </summary>
    public struct HangarBay : IBufferElementData
    {
        public int HangarClassId;
        public int Capacity;
        public int ReservedSlots;
        public int OccupiedSlots;
        public float LaunchRate;
        public float RecoveryRate;
    }

    /// <summary>
    /// Assignment of sub-platform to hangar bay.
    /// </summary>
    public struct HangarAssignment : IBufferElementData
    {
        public Entity SubPlatform;
        public int HangarIndex;
    }

    /// <summary>
    /// Nano-swarm cloud state.
    /// </summary>
    public struct NanoSwarmState : IComponentData
    {
        public int SwarmTypeId;
        public int ParticleCount;
        public float Radius;
        public float Density;
        public float EnergyReserve;
    }

    /// <summary>
    /// Arc instance for weapons/shields. Built from module positions.
    /// </summary>
    public struct PlatformArcInstance : IBufferElementData
    {
        public int ModuleId;
        public float3 WorldPosition;
        public float3 ForwardDirection;
        public float ArcAngle;
    }

    /// <summary>
    /// Platform segment runtime state.
    /// </summary>
    public struct PlatformSegmentState : IBufferElementData
    {
        public int SegmentIndex;
        public float HP;
        public SegmentStatusFlags Status;
        public float MassUsed;
        public float PowerUsed;
        public int ControlFactionId;
    }

    /// <summary>
    /// Platform module health tracking.
    /// </summary>
    public struct PlatformModuleHealth : IBufferElementData
    {
        public int ModuleIndex;
        public float HP;
        public float MaxHP;
        public ModuleDamageFlags Flags;
    }

    /// <summary>
    /// Tag component for derelict platforms.
    /// </summary>
    public struct DerelictTag : IComponentData
    {
    }

    /// <summary>
    /// Salvageable state for derelicts/husks.
    /// </summary>
    public struct SalvageableState : IComponentData
    {
        public byte Infested;
        public byte HasLootCache;
        public float StructuralIntegrity;
    }

    /// <summary>
    /// Resource storage for a platform (carriers, stations, etc.).
    /// Similar to VillageResources but for Space4X platforms.
    /// </summary>
    public struct PlatformResources : IComponentData
    {
        public float Ore;
        public float RefinedOre;
        public float Fuel;
        public float Supplies;
        public float RawMaterials;
        public float ProcessedMaterials;
    }
}

