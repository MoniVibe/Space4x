using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Space
{
    public enum MountType : byte
    {
        MainGun,
        MissileRack,
        PointDefense,
        UtilityBay,
        Spinal
    }

    public enum MountSize : byte
    {
        Small,
        Medium,
        Large,
        Spinal
    }

    public enum ModuleFamily : byte
    {
        Weapon,
        Defense,
        Utility,
        Facility,
        Colony
    }

    public enum ModuleClass : byte
    {
        BeamCannon,
        MassDriver,
        Missile,
        PointDefense,
        Shield,
        Armor,
        Engine,
        Sensor,
        Cargo,
        Fabrication,
        Research,
        Medical,
        Hangar,
        Habitation,
        Agriculture,
        Mining,
        Terraforming,
        Administration
    }

    public enum ModuleState : byte
    {
        Offline,
        Standby,
        Active,
        Damaged,
        Destroyed
    }

    public struct CarrierModuleSlot : IBufferElementData
    {
        public byte SlotIndex;
        public MountType Type;
        public MountSize Size;
        public Entity InstalledModule;
    }

    public struct ShipModule : IComponentData
    {
        public ModuleFamily Family;
        public ModuleClass Class;
        public MountType RequiredMount;
        public MountSize RequiredSize;
        public FixedString64Bytes ModuleName;
        public float Mass;
        public float PowerRequired;
        public float PowerGeneration;
        public byte EfficiencyPercent;
        public ModuleState State;
    }

    public struct ModuleStatModifier : IComponentData
    {
        public float Mass;
        public float PowerDraw;
        public float PowerGeneration;
        public float Armor;
        public float Shield;
        public float CargoCapacity;
        public float MiningRate;
        public float RepairRateBonus;
    }

    public struct CarrierModuleStatTotals : IComponentData
    {
        public float TotalMass;
        public float TotalPowerDraw;
        public float TotalPowerGeneration;
        public float TotalCargoCapacity;
        public float TotalMiningRate;
        public float TotalRepairRateBonus;
        public byte DamagedModuleCount;
        public byte DestroyedModuleCount;
        public float NetPower => TotalPowerGeneration - TotalPowerDraw;
    }

    public struct CarrierPowerBudget : IComponentData
    {
        public float MaxPowerOutput;
        public float CurrentDraw;
        public float CurrentGeneration;
        public bool OverBudget;
    }

    public struct ModuleHealth : IComponentData
    {
        public const byte FlagRequiresRepair = 1 << 0;

        public byte Integrity;
        public byte FailureThreshold;
        public byte RepairPriority;
        public byte Flags;

        public bool NeedsRepair => (Flags & FlagRequiresRepair) != 0;

        public void MarkRepairRequested()
        {
            Flags |= FlagRequiresRepair;
        }

        public void ClearRepairRequested()
        {
            Flags &= unchecked((byte)~FlagRequiresRepair);
        }
    }

    public struct ModuleDegradation : IComponentData
    {
        public float PassivePerSecond;
        public float ActivePerSecond;
        public float CombatMultiplier;
    }

    public enum ModuleRepairKind : byte
    {
        Field,
        Station
    }

    public struct ModuleRepairTicket : IBufferElementData
    {
        public Entity Module;
        public ModuleRepairKind Kind;
        public byte Priority;
        public float RemainingWork;
    }

    public struct CarrierRefitState : IComponentData
    {
        public float FieldRefitRate;
        public float StationRefitRate;
        public bool AtRefitFacility;
    }

    public struct CarrierModuleRefitRequest : IBufferElementData
    {
        public byte SlotIndex;
        public Entity ExistingModule;
        public Entity NewModulePrefab;
        public float WorkRemaining;
        public bool RequiresStation;
    }

    public struct CarrierModuleDefinition
    {
        public BlobString ModuleId;
        public ModuleFamily Family;
        public ModuleClass Class;
        public MountType RequiredMount;
        public MountSize RequiredSize;
        public float Mass;
        public float PowerDraw;
        public float PowerGeneration;
    }

    public struct CarrierModuleCatalogBlob
    {
        public BlobArray<CarrierModuleDefinition> Modules;
    }

    public struct CarrierModuleCatalog : IComponentData
    {
        public BlobAssetReference<CarrierModuleCatalogBlob> Catalog;
    }
}
