using PureDOTS.Runtime.Modules;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    public struct EngineModuleSpec
    {
        public FixedString64Bytes ModuleId;
        public EngineClass EngineClass;
        public EngineFuelType FuelType;
        public EngineIntakeType IntakeType;
        public EngineVectoringMode VectoringMode;
        public float TechLevel; // 0-1 (0 = auto)
        public float Quality; // 0-1 (0 = auto)
        public float ThrustScalar; // <= 0 = auto
        public float TurnScalar; // <= 0 = auto
        public float ResponseRating; // 0-1 (0 = auto)
        public float EfficiencyRating; // 0-1 (0 = auto)
        public float BoostRating; // 0-1 (0 = auto)
        public float VectoringRating; // 0-1 (0 = auto)
    }

    public struct ShieldModuleSpec
    {
        public FixedString64Bytes ModuleId;
        public float Capacity;
        public float RechargePerSecond;
        public float RegenDelaySeconds;
        public float ArcDegrees;
        public float KineticResist;
        public float EnergyResist;
        public float ThermalResist;
        public float EMResist;
        public float RadiationResist;
        public float ExplosiveResist;
    }

    public struct SensorModuleSpec
    {
        public FixedString64Bytes ModuleId;
        public float Range;
        public float RefreshSeconds;
        public float Resolution;
        public float JamResistance;
        public float PassiveSignature;
    }

    public struct ArmorModuleSpec
    {
        public FixedString64Bytes ModuleId;
        public float HullBonus;
        public float DamageReduction;
        public float KineticResist;
        public float EnergyResist;
        public float ThermalResist;
        public float EMResist;
        public float RadiationResist;
        public float ExplosiveResist;
        public float RepairRateMultiplier;
    }

    public struct WeaponModuleSpec
    {
        public FixedString64Bytes ModuleId;
        public FixedString64Bytes WeaponId;
        public float FireArcDegrees;
        public float FireArcOffsetDeg;
        public float AccuracyBonus;
        public float TrackingBonus;
    }

    public struct BridgeModuleSpec
    {
        public FixedString64Bytes ModuleId;
        public float TechLevel; // 0-1 (0 = auto)
    }

    public struct CockpitModuleSpec
    {
        public FixedString64Bytes ModuleId;
        public float NavigationCohesion; // 0-1 (0 = auto)
    }

    public struct AmmoModuleSpec
    {
        public FixedString64Bytes ModuleId;
        public float AmmoCapacity;
    }

    public struct EngineModuleCatalogBlob
    {
        public BlobArray<EngineModuleSpec> Modules;
    }

    public struct ShieldModuleCatalogBlob
    {
        public BlobArray<ShieldModuleSpec> Modules;
    }

    public struct SensorModuleCatalogBlob
    {
        public BlobArray<SensorModuleSpec> Modules;
    }

    public struct ArmorModuleCatalogBlob
    {
        public BlobArray<ArmorModuleSpec> Modules;
    }

    public struct WeaponModuleCatalogBlob
    {
        public BlobArray<WeaponModuleSpec> Modules;
    }

    public struct BridgeModuleCatalogBlob
    {
        public BlobArray<BridgeModuleSpec> Modules;
    }

    public struct CockpitModuleCatalogBlob
    {
        public BlobArray<CockpitModuleSpec> Modules;
    }

    public struct AmmoModuleCatalogBlob
    {
        public BlobArray<AmmoModuleSpec> Modules;
    }

    public struct EngineModuleCatalogSingleton : IComponentData
    {
        public BlobAssetReference<EngineModuleCatalogBlob> Catalog;
    }

    public struct ShieldModuleCatalogSingleton : IComponentData
    {
        public BlobAssetReference<ShieldModuleCatalogBlob> Catalog;
    }

    public struct SensorModuleCatalogSingleton : IComponentData
    {
        public BlobAssetReference<SensorModuleCatalogBlob> Catalog;
    }

    public struct ArmorModuleCatalogSingleton : IComponentData
    {
        public BlobAssetReference<ArmorModuleCatalogBlob> Catalog;
    }

    public struct WeaponModuleCatalogSingleton : IComponentData
    {
        public BlobAssetReference<WeaponModuleCatalogBlob> Catalog;
    }

    public struct BridgeModuleCatalogSingleton : IComponentData
    {
        public BlobAssetReference<BridgeModuleCatalogBlob> Catalog;
    }

    public struct CockpitModuleCatalogSingleton : IComponentData
    {
        public BlobAssetReference<CockpitModuleCatalogBlob> Catalog;
    }

    public struct AmmoModuleCatalogSingleton : IComponentData
    {
        public BlobAssetReference<AmmoModuleCatalogBlob> Catalog;
    }
}
