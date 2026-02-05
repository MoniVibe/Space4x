using Unity.Collections;
using Unity.Entities;

namespace Space4X.Registry
{
    public struct BridgeModuleProfile : IComponentData
    {
        public float TechLevel; // 0-1
    }

    public struct CockpitModuleProfile : IComponentData
    {
        public float NavigationCohesion; // 0-1
    }

    public struct AmmoModuleProfile : IComponentData
    {
        public float AmmoCapacity;
    }

    public struct ShieldModuleProfile : IComponentData
    {
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

    public struct ArmorModuleProfile : IComponentData
    {
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

    public struct SensorModuleProfile : IComponentData
    {
        public float Range;
        public float RefreshSeconds;
        public float Resolution;
        public float JamResistance;
        public float PassiveSignature;
    }

    public struct WeaponModuleProfile : IComponentData
    {
        public FixedString64Bytes WeaponId;
        public float FireArcDegrees;
        public float FireArcOffsetDeg;
        public float AccuracyBonus;
        public float TrackingBonus;
    }

    public struct BridgeTechLevelBase : IComponentData
    {
        public float Value;
    }

    public struct NavigationCohesionBase : IComponentData
    {
        public float Value;
    }

    public struct AmmoCapacityBase : IComponentData
    {
        public float Value;
    }
}
