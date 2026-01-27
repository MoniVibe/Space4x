using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Combat
{
    /// <summary>
    /// Stable identifier for entities that need deterministic behavior across rewinds.
    /// Assigned at spawn and preserved during rewind playback.
    /// </summary>
    public struct PersistentId : IComponentData
    {
        public uint Value;
    }

    /// <summary>
    /// Component attached to entities that have weapons installed.
    /// </summary>
    public struct WeaponMount : IComponentData
    {
        public FixedString64Bytes WeaponId; // Reference to WeaponSpec
        public FixedString32Bytes TurretId; // Reference to TurretSpec (empty = fixed mount)
        public Entity TargetEntity; // Current target (Entity.Null if none)
        public float3 TargetPosition; // World-space target position
        public float LastFireTime; // Time of last shot
        public float HeatLevel; // Current heat (0-1)
        public float EnergyReserve; // Current energy available
        public bool IsFiring; // Whether weapon is currently firing
        public int ShotSequence; // Incremental counter for deterministic RNG seeding
    }

    /// <summary>
    /// Component attached to active projectile entities.
    /// </summary>
    public struct ProjectileEntity : IComponentData
    {
        public FixedString64Bytes ProjectileId; // Reference to ProjectileSpec
        public Entity SourceEntity; // Entity that fired this projectile
        public Entity TargetEntity; // Target entity (for homing, Entity.Null for ballistic)
        public float3 Velocity; // Current velocity vector
        public float3 PrevPos; // Previous position for continuous collision detection
        public float SpawnTime; // Time when projectile was created
        public float DistanceTraveled; // Total distance traveled
        public float HitsLeft; // Remaining pierce count (changed from byte to float)
        public float Age; // Seconds since spawn
        public uint Seed; // Deterministic shot seed for damage/crit rolls
        public int ShotSequence; // Sequence number of the shot that spawned this projectile
        public int PelletIndex; // Index within spread pattern (0 for single shots)
    }

    /// <summary>
    /// Component for turret traversal state.
    /// </summary>
    public struct TurretState : IComponentData
    {
        public FixedString32Bytes TurretId; // Reference to TurretSpec
        public quaternion CurrentRotation; // Current turret rotation
        public quaternion TargetRotation; // Desired rotation
        public float3 MuzzlePosition; // World-space muzzle position (updated by system)
        public float3 MuzzleForward; // World-space forward direction (updated by system)
    }

    /// <summary>
    /// Component marking entities that can be damaged.
    /// </summary>
    public struct Damageable : IComponentData
    {
        public float ShieldPoints;
        public float MaxShieldPoints;
        public float ArmorPoints;
        public float MaxArmorPoints;
        public float HullPoints;
        public float MaxHullPoints;
    }

    /// <summary>
    /// Component for impact effects queued by projectile systems.
    /// </summary>
    public struct ImpactEffectRequest : IComponentData
    {
        public float3 ImpactPosition;
        public float3 ImpactNormal;
        public FixedString64Bytes EffectId; // Presentation binding ID
        public float Magnitude; // Effect magnitude (for scaling)
    }

    /// <summary>
    /// Buffer element for queued projectile spawns.
    /// </summary>
    public struct ProjectileSpawnRequest : IBufferElementData
    {
        public FixedString64Bytes ProjectileId;
        public float3 SpawnPosition;
        public float3 SpawnDirection;
        public Entity SourceEntity;
        public Entity TargetEntity;
        public uint ShotSeed; // Deterministic seed for damage/crit rolls
        public int ShotSequence; // Sequence number for this shot
        public int PelletIndex; // Index within spread pattern (0 for single shots)
    }

    /// <summary>
    /// Buffer element for projectile hit results from collision detection.
    /// </summary>
    public struct ProjectileHitResult : IBufferElementData
    {
        public float3 HitPosition;
        public float3 HitNormal;
        public Entity HitEntity;
        public float TimeOfImpact; // 0-1 along the segment from PrevPos to Pos
    }
}

