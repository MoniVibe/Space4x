using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Scope for status-like effect application.
    /// </summary>
    public enum Space4XStatusEffectScope : byte
    {
        Module = 0,
        Ship = 1,
        Area = 2,
        Hazard = 3,
        Weapon = 4,
        BeneficialModule = 5
    }

    [System.Flags]
    public enum Space4XAreaEffectTargetMask : byte
    {
        None = 0,
        Ships = 1 << 0,
        Individuals = 1 << 1,
        All = Ships | Individuals
    }

    [System.Flags]
    public enum Space4XAreaEffectImpactMask : byte
    {
        None = 0,
        HullDamage = 1 << 0,
        Hazard = 1 << 1,
        DisableWeapons = 1 << 2,
        DisableEngines = 1 << 3,
        ModuleLimbDamage = 1 << 4
    }

    [System.Flags]
    public enum Space4XAreaOcclusionChannel : byte
    {
        None = 0,
        Blast = 1 << 0,
        Hazard = 1 << 1,
        EMP = 1 << 2,
        Thermal = 1 << 3
    }

    public enum Space4XAreaOcclusionMode : byte
    {
        None = 0,
        BinaryBlock = 1,
        StrengthAttenuation = 2,
        PhysicsRaycast = 3
    }

    /// <summary>
    /// Config/runtime state for an area effect pulse emitter.
    /// Attach to any entity with LocalTransform, or use CenterWorld directly.
    /// </summary>
    public struct Space4XAreaEffectEmitter : IComponentData
    {
        public Space4XStatusEffectScope Scope;
        public Space4XAreaEffectTargetMask TargetMask;
        public Space4XAreaEffectImpactMask ImpactMask;
        public Space4XAreaOcclusionChannel OcclusionChannel;
        public Space4XAreaOcclusionMode OcclusionMode;
        public HazardTypeId HazardType;
        public Space4XDamageType DamageType;
        public float3 CenterWorld;
        public float3 LocalOffset;
        public float Radius;
        public float InnerRadius;
        public float Magnitude;
        public float FalloffExponent;
        public float OcclusionRadiusBias;
        public uint DisableDurationTicks;
        public float ModuleDamageScale;
        public uint PulseIntervalTicks;
        public uint NextPulseTick;
        public uint RemainingPulses; // 0 = unlimited
        public byte Active;
        public byte AffectsSource;
        public Entity ExcludedEntity;
    }

    /// <summary>
    /// Spherical occluder for line-of-effect checks.
    /// Use on asteroids, shields, and large hulls.
    /// </summary>
    public struct Space4XAreaOccluder : IComponentData
    {
        public float Radius;
        public float Strength01;
        public Space4XAreaOcclusionChannel BlocksChannels;
    }

    /// <summary>
    /// Optional per-target event stream for status/buff/debuff consumers.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct Space4XStatusEffectEvent : IBufferElementData
    {
        public Entity SourceEntity;
        public Space4XStatusEffectScope Scope;
        public Space4XAreaEffectImpactMask ImpactMask;
        public HazardTypeId HazardType;
        public Space4XDamageType DamageType;
        public float Magnitude;
        public uint Tick;
    }
}
