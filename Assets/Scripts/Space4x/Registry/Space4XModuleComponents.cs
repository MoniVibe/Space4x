using Unity.Collections;
using Unity.Entities;

namespace Space4X.Registry
{
    public enum ModuleSlotState : byte
    {
        Empty = 0,
        Active = 1,
        Removing = 2,
        Installing = 3
    }

    public enum ModuleSlotSize : byte
    {
        Small = 0,
        Medium = 1,
        Large = 2
    }

    /// <summary>
    /// Identifier for a specific module archetype.
    /// </summary>
    public struct ModuleTypeId : IComponentData
    {
        public FixedString64Bytes Value;
    }

    /// <summary>
    /// Declares the slot size a module requires.
    /// </summary>
    public struct ModuleSlotRequirement : IComponentData
    {
        public ModuleSlotSize SlotSize;
    }

    /// <summary>
    /// Declares a physical slot on a carrier and the module occupying it.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct CarrierModuleSlot : IBufferElementData
    {
        public int SlotIndex;
        public ModuleSlotSize SlotSize;
        public Entity CurrentModule;
        public Entity TargetModule;
        public float RefitProgress;
        public ModuleSlotState State;
    }

    /// <summary>
    /// Deterministic refit request queued on a carrier.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct ModuleRefitRequest : IBufferElementData
    {
        public int SlotIndex;
        public Entity TargetModule;
        public byte Priority;
        public uint RequestTick;
        public float RequiredWork;
    }

    /// <summary>
    /// Marks an entity as capable of processing refits.
    /// </summary>
    public struct ModuleRefitFacility : IComponentData
    {
        public float RefitRatePerSecond;
        public byte SupportsFieldRefit;
    }

    /// <summary>
    /// Per-module stat adjustments applied when installed.
    /// </summary>
    public struct ModuleStatModifier : IComponentData
    {
        public float SpeedMultiplier;
        public float CargoMultiplier;
        public float EnergyMultiplier;
        public float RefitRateMultiplier;
        public float RepairRateMultiplier;
    }

    /// <summary>
    /// Aggregated stat multipliers from all operational modules.
    /// </summary>
    public struct ModuleStatAggregate : IComponentData
    {
        public float SpeedMultiplier;
        public float CargoMultiplier;
        public float EnergyMultiplier;
        public float RefitRateMultiplier;
        public float RepairRateMultiplier;
        public int ActiveModuleCount;
    }

    /// <summary>
    /// Tag applied when a carrier is docked at a station that allows station-only refits/overhauls.
    /// </summary>
    public struct DockedAtStation : IComponentData
    {
    }

    /// <summary>
    /// Health and degradation state for an individual module instance.
    /// </summary>
    public struct ModuleHealth : IComponentData
    {
        public float CurrentHealth;
        public float MaxHealth;
        public float MaxFieldRepairHealth;
        public float DegradationPerSecond;
        public byte RepairPriority;
        public byte Failed;
    }

    /// <summary>
    /// Repair capability available to a carrier in the field.
    /// </summary>
    public struct FieldRepairCapability : IComponentData
    {
        public float RepairRatePerSecond;
        public float CriticalRepairRate;
        public byte CanRepairCritical;
    }

    /// <summary>
    /// Full overhaul capability available when docked at a station.
    /// </summary>
    public struct StationOverhaulFacility : IComponentData
    {
        public float OverhaulRatePerSecond;
    }

    public enum ModuleMaintenanceEventType : byte
    {
        RefitStarted = 0,
        RefitCompleted = 1,
        RepairApplied = 2,
        ModuleFailed = 3
    }

    /// <summary>
    /// Command-log style maintenance entry so rewind/telemetry can replay refits/repairs.
    /// </summary>
    public struct ModuleMaintenanceCommandLogEntry : IBufferElementData
    {
        public uint Tick;
        public Entity Carrier;
        public int SlotIndex;
        public Entity Module;
        public ModuleMaintenanceEventType EventType;
        public float Amount;
    }

    /// <summary>
    /// Aggregated maintenance telemetry counters.
    /// </summary>
    public struct ModuleMaintenanceTelemetry : IComponentData
    {
        public uint LastUpdateTick;
        public uint RefitStarted;
        public uint RefitCompleted;
        public uint Failures;
        public float RepairApplied;
        public float RefitWorkApplied;
    }

    /// <summary>
    /// Marker for the maintenance log singleton.
    /// </summary>
    public struct ModuleMaintenanceLog : IComponentData
    {
        public uint SnapshotHorizon;
        public uint LastPlaybackTick;
    }
}
