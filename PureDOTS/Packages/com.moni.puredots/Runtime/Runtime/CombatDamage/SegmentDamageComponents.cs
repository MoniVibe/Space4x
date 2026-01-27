using System;
using PureDOTS.Runtime.Identity;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.CombatDamage
{
    public static class SegmentDamageConstants
    {
        public const ushort UnassignedSegmentId = ushort.MaxValue;
        public const ushort UnroutableSegmentId = ushort.MaxValue - 1;
    }

    [InternalBufferCapacity(4)]
    public struct SegmentDamageEvent : IBufferElementData
    {
        public uint EventId;
        public uint Tick;
        public Entity Source;
        public Entity Target;
        public ushort SegmentId;
        public ushort SegmentHint;
        public float3 ImpactPositionLocal;
        public float3 ImpactNormalLocal;
        public float3 IncomingDirectionLocal;
        public float Impulse;
        public float Heat;
        public float SpreadRadius;
        public SegmentDamageEventFlags Flags;
        public ushort PayloadStart;
        public ushort PayloadCount;
    }

    [Flags]
    public enum SegmentDamageEventFlags : byte
    {
        None = 0,
        IsExplosion = 1 << 0,
        IsBeam = 1 << 1,
        IsPiercing = 1 << 2,
        IgnoresFriendlyFire = 1 << 3
    }

    [InternalBufferCapacity(8)]
    public struct SegmentDamagePayloadElement : IBufferElementData
    {
        public ushort DamageTypeIndex;
        public float Amount;
        public float Penetration;
        public float Bypass;
    }

    [Flags]
    public enum SegmentDamageTypeFlags : byte
    {
        None = 0,
        Thermal = 1 << 0,
        EM = 1 << 1,
        Explosive = 1 << 2,
        Kinetic = 1 << 3,
        Corrosive = 1 << 4,
        Radiation = 1 << 5
    }

    public struct SegmentDamageTypeDef
    {
        public FixedString64Bytes Id;
        public SegmentDamageTypeFlags Flags;
    }

    public struct SegmentDamageTypeCatalogBlob
    {
        public BlobArray<SegmentDamageTypeDef> Types;
    }

    public struct SegmentDamageTypeIndex : IComponentData
    {
        public BlobAssetReference<SegmentDamageTypeCatalogBlob> Catalog;
    }

    public enum ShieldCoverageMode : byte
    {
        Bubble = 0,
        Arc = 1,
        Wrap = 2,
        Directional = 3
    }

    public struct ShieldState : IComponentData
    {
        public float MaxStrength;
        public float RegenPerSecond;
        public float CoverageAngleDeg;
        public ShieldCoverageMode CoverageMode;
        public float PowerRatio;
    }

    public struct DamageResistanceElement
    {
        public ushort DamageTypeIndex;
        public float Resistance;
        public float Hardness;
        public float SeepThrough;
    }

    public struct ShieldProfileBlob
    {
        public float MaxStrength;
        public float RegenPerSecond;
        public float CoverageAngleDeg;
        public byte CoverageMode;
        public BlobArray<DamageResistanceElement> Resistances;
    }

    public struct ArmorProfileBlob
    {
        public float MaxIntegrity;
        public float AblationPerDamage;
        public BlobArray<DamageResistanceElement> Resistances;
    }

    public struct HullProfileBlob
    {
        public float MaxIntegrity;
        public float BreachThreshold;
        public BlobArray<DamageResistanceElement> Resistances;
    }

    public struct DamageProfileCatalogBlob
    {
        public BlobArray<ShieldProfileBlob> Shields;
        public BlobArray<ArmorProfileBlob> Armors;
        public BlobArray<HullProfileBlob> Hulls;
    }

    public struct DamageProfileIndex : IComponentData
    {
        public BlobAssetReference<DamageProfileCatalogBlob> Catalog;
    }

    [InternalBufferCapacity(4)]
    public struct DamageSegmentDefinition : IBufferElementData
    {
        public ushort SegmentId;
        public float3 LocalCenter;
        public float3 LocalExtents;
        public ushort ShieldProfileId;
        public ushort ArmorProfileId;
        public ushort HullProfileId;
        public byte Flags;
    }

    [InternalBufferCapacity(4)]
    public struct DamageSegmentState : IBufferElementData
    {
        public ushort SegmentId;
        public IntegrityState ShieldIntegrity;
        public IntegrityState ArmorIntegrity;
        public IntegrityState HullIntegrity;
        public byte Flags;
        public uint LastDamageTick;
    }

    [Flags]
    public enum DamageSegmentFlags : byte
    {
        None = 0,
        Breached = 1 << 0,
        Vented = 1 << 1,
        OnFire = 1 << 2
    }

    public struct ModuleSegmentLink : IComponentData
    {
        public ushort SegmentId;
        public float3 LocalOffset;
    }

    [InternalBufferCapacity(2)]
    public struct ShieldHoleState : IBufferElementData
    {
        public ushort SegmentId;
        public float3 LocalDirection;
        public float AngleDeg;
        public float RemainingSeconds;
    }

    public struct ModuleIntegrity : IComponentData
    {
        public float FaultThreshold;
        public float CriticalThreshold;
    }

    [InternalBufferCapacity(2)]
    public struct ModuleDamageSensitivity : IBufferElementData
    {
        public ushort DamageTypeIndex;
        public float Multiplier;
    }

    public struct ModuleFaultState : IComponentData
    {
        public byte IsFaulted;
        public byte IsDestroyed;
        public uint LastFaultTick;
    }

    public enum CombatPosture : byte
    {
        Hold = 0,
        Cautious = 1,
        Aggressive = 2,
        Desperate = 3,
        Retreat = 4
    }

    public struct CombatPostureState : IComponentData
    {
        public CombatPosture Value;
        public float RiskTolerance;
        public float RetreatThreshold;
    }

    public struct DamageControlPolicy : IComponentData
    {
        public float ShieldPriority;
        public float RepairPriority;
        public float WeaponsPriority;
        public float VentingThreshold;
    }

    public struct SegmentDamageMetrics : IComponentData
    {
        public uint Tick;
        public uint NormalizedHits;
        public uint RoutedHits;
        public uint UnroutableHits;
    }

    [InternalBufferCapacity(16)]
    public struct SegmentDamageHitBucket : IBufferElementData
    {
        public ushort SegmentId;
        public uint Count;
    }
}
