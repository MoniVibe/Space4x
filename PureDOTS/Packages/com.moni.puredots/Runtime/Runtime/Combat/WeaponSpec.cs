using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Combat
{
    /// <summary>
    /// Weapon specification data - defines weapon behavior and stats.
    /// Data-only, no GameObject references.
    /// </summary>
    public struct WeaponSpec
    {
        public FixedString64Bytes Id;
        public byte Class; // Weapon class enum (Beam, MassDriver, Missile, etc.)
        public float FireRate; // Shots per second
        public byte Burst; // Shots per burst (1 = single shot)
        public float SpreadDeg; // Spread angle in degrees
        public float EnergyCost; // Energy per shot
        public float HeatCost; // Heat generated per shot
        public FixedString32Bytes ProjectileId; // Reference to projectile spec
    }

    /// <summary>
    /// Projectile specification data - defines projectile behavior.
    /// </summary>
    public struct ProjectileSpec
    {
        public FixedString64Bytes Id;
        public byte Kind; // ProjectileKind enum (Ballistic, Homing, Beam)
        public float Speed; // Units per second
        public float Lifetime; // Seconds before expiry
        public float TurnRateDeg; // Degrees per second (for homing)
        public float SeekRadius; // Detection radius for homing
        public float AoERadius; // Area of effect radius (0 = no AoE)
        public float Pierce; // Pierce count (0 = no pierce)
        public float ChainRange; // Chain effect range (0 = none)
        public uint HitFilter; // Physics collision mask
        public DamageModel Damage; // Damage calculation model
        public BlobArray<EffectOp> OnHit; // Effect operations on hit
    }

    /// <summary>
    /// Turret specification data - defines turret traversal capabilities.
    /// </summary>
    public struct TurretSpec
    {
        public FixedString32Bytes Id;
        public float TraverseDegPerS; // Yaw rotation speed (degrees/second)
        public float ElevDegPerS; // Pitch rotation speed (degrees/second)
        public float ArcYawDeg; // Yaw arc limit (0 = 360, otherwise ±arc/2)
        public float ArcPitchDeg; // Pitch arc limit (0 = 180, otherwise ±arc/2)
        public FixedString32Bytes MuzzleSocket; // Socket name for muzzle position
    }

    /// <summary>
    /// Damage model for projectiles.
    /// </summary>
    public struct DamageModel
    {
        public float BaseDamage;
        public float ShieldMultiplier; // Damage vs shields (1.0 = normal)
        public float ArmorMultiplier; // Damage vs armor (1.0 = normal)
        public float HullMultiplier; // Damage vs hull (1.0 = normal)
    }

    /// <summary>
    /// Projectile kind enumeration.
    /// </summary>
    public enum ProjectileKind : byte
    {
        Ballistic = 0,
        Homing = 1,
        Beam = 2
    }

    /// <summary>
    /// Effect operation kind enumeration.
    /// </summary>
    public enum EffectOpKind : byte
    {
        Damage = 0,
        AoE = 1,
        Chain = 2,
        Pierce = 3,
        Status = 4,
        Knockback = 5,
        SpawnSub = 6
    }

    /// <summary>
    /// Effect operation data - defines what happens when a projectile hits.
    /// </summary>
    public struct EffectOp
    {
        public EffectOpKind Kind; // Type of effect
        public float Magnitude; // Effect magnitude (damage, force, etc.)
        public float Duration; // Effect duration in seconds (for status effects)
        public float Aux; // Auxiliary value (radius for AoE, count for chain, etc.)
        public uint StatusId; // Status effect ID (for Status kind)
    }

    /// <summary>
    /// Weapon class enumeration.
    /// </summary>
    public enum WeaponClass : byte
    {
        BeamCannon = 0,
        MassDriver = 1,
        Missile = 2,
        PointDefense = 3,
        Torpedo = 4
    }

    /// <summary>
    /// Blob catalog for weapon specifications.
    /// </summary>
    public struct WeaponCatalogBlob
    {
        public BlobArray<WeaponSpec> Weapons;
    }

    /// <summary>
    /// Blob catalog for projectile specifications.
    /// </summary>
    public struct ProjectileCatalogBlob
    {
        public BlobArray<ProjectileSpec> Projectiles;
    }

    /// <summary>
    /// Blob catalog for turret specifications.
    /// </summary>
    public struct TurretCatalogBlob
    {
        public BlobArray<TurretSpec> Turrets;
    }

    /// <summary>
    /// Singleton component holding weapon catalog reference.
    /// </summary>
    public struct WeaponCatalog : IComponentData
    {
        public BlobAssetReference<WeaponCatalogBlob> Catalog;
    }

    /// <summary>
    /// Singleton component holding projectile catalog reference.
    /// </summary>
    public struct ProjectileCatalog : IComponentData
    {
        public BlobAssetReference<ProjectileCatalogBlob> Catalog;
    }

    /// <summary>
    /// Singleton component holding turret catalog reference.
    /// </summary>
    public struct TurretCatalog : IComponentData
    {
        public BlobAssetReference<TurretCatalogBlob> Catalog;
    }
}

