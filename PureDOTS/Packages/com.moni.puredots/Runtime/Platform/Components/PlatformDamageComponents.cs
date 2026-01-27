using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Platform
{
    /// <summary>
    /// Damage type flags - affects how damage interacts with armor/resistances.
    /// </summary>
    [System.Flags]
    public enum DamageTypeFlags : uint
    {
        Kinetic = 1 << 0,
        Energy = 1 << 1,
        EMP = 1 << 2,
        Radiation = 1 << 3,
        Thermal = 1 << 4,
        Chemical = 1 << 5,
        Biological = 1 << 6,
        Psionic = 1 << 7,
        Gravitic = 1 << 8,
        Nanite = 1 << 9
    }

    /// <summary>
    /// Damage behavior flags - special properties affecting damage application.
    /// </summary>
    [System.Flags]
    public enum DamageBehaviorFlags : uint
    {
        BypassesShields = 1 << 0,
        PenetratesArmor = 1 << 1,
        CrewPreferred = 1 << 2,
        HullPreferred = 1 << 3,
        ModulePreferred = 1 << 4,
        AreaOfEffect = 1 << 5,
        InternalOrigin = 1 << 6,
        Phasing = 1 << 7
    }

    /// <summary>
    /// Weapon damage profile defining how a weapon's damage behaves.
    /// </summary>
    public struct WeaponDamageProfile
    {
        public int WeaponId;
        public float BaseDamage;
        public DamageTypeFlags TypeFlags;
        public DamageBehaviorFlags BehaviorFlags;
        public float ArmorPenetration;
        public float ShieldEfficiency;
        public float HullEfficiency;
        public float ModuleEfficiency;
        public float CrewEfficiency;
        public float Radius;
    }

    /// <summary>
    /// Hit event targeting a platform. Consumed by PlatformHitResolutionSystem.
    /// </summary>
    public struct PlatformHitEvent : IBufferElementData
    {
        public Entity TargetPlatform;
        public float3 WorldHitPosition;
        public float3 WorldDirection;
        public int WeaponId;
        public float DamageAmount;
    }

    /// <summary>
    /// Explosion event for reactor meltdowns or internal explosions.
    /// </summary>
    public struct PlatformExplosionEvent : IBufferElementData
    {
        public Entity SourcePlatform;
        public float3 WorldPosition;
        public float DamageAmount;
        public float Radius;
        public DamageTypeFlags TypeFlags;
    }
}

