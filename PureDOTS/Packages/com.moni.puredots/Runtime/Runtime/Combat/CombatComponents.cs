using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Combat
{
    /// <summary>
    /// Weapon data for entities (simpler than WeaponMount, works alongside it).
    /// Phase 1: Basic weapon stats.
    /// Phase 2: Extended with complex behaviors, modifiers, etc.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct WeaponComponent : IBufferElementData
    {
        /// <summary>
        /// Maximum range for this weapon.
        /// </summary>
        public float Range;

        /// <summary>
        /// Fire rate (shots per second).
        /// </summary>
        public float FireRate;

        /// <summary>
        /// Base damage per shot.
        /// </summary>
        public float BaseDamage;

        /// <summary>
        /// Damage type (affects resistances).
        /// </summary>
        public DamageType DamageType;

        /// <summary>
        /// Projectile type identifier (for spawning projectiles).
        /// Empty = hitscan/instant.
        /// </summary>
        public FixedString32Bytes ProjectileType;

        /// <summary>
        /// Fire arc constraints (degrees, 0 = no constraint).
        /// </summary>
        public float FireArcDegrees;

        /// <summary>
        /// Last fire time (seconds).
        /// </summary>
        public float LastFireTime;

        /// <summary>
        /// Current cooldown remaining (seconds).
        /// </summary>
        public float CooldownRemaining;

        /// <summary>
        /// Weapon index (for referencing this weapon).
        /// </summary>
        public byte WeaponIndex;
    }

    /// <summary>
    /// Fire event - emitted when weapon fires.
    /// Consumed by projectile spawn systems or hitscan systems.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct FireEvent : IBufferElementData
    {
        /// <summary>
        /// Entity that fired.
        /// </summary>
        public Entity EmitterEntity;

        /// <summary>
        /// Target entity (if any).
        /// </summary>
        public Entity TargetEntity;

        /// <summary>
        /// Target position (world space).
        /// </summary>
        public float3 TargetPosition;

        /// <summary>
        /// Weapon index that fired.
        /// </summary>
        public byte WeaponIndex;

        /// <summary>
        /// Fire direction (normalized).
        /// </summary>
        public float3 FireDirection;

        /// <summary>
        /// Tick when fired.
        /// </summary>
        public uint FireTick;

        /// <summary>
        /// Damage amount (from weapon).
        /// </summary>
        public float DamageAmount;

        /// <summary>
        /// Damage type.
        /// </summary>
        public DamageType DamageType;
    }

    /// <summary>
    /// Hit event - emitted when projectile/hitscan hits target.
    /// Consumed by DamageResolutionSystem to apply damage.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct HitEvent : IBufferElementData
    {
        /// <summary>
        /// Entity that was hit.
        /// </summary>
        public Entity HitEntity;

        /// <summary>
        /// Entity that caused the hit (attacker).
        /// </summary>
        public Entity AttackerEntity;

        /// <summary>
        /// Hit position (world space).
        /// </summary>
        public float3 HitPosition;

        /// <summary>
        /// Hit normal (for impact effects).
        /// </summary>
        public float3 HitNormal;

        /// <summary>
        /// Damage amount.
        /// </summary>
        public float DamageAmount;

        /// <summary>
        /// Damage type.
        /// </summary>
        public DamageType DamageType;

        /// <summary>
        /// Weapon index that caused hit (if applicable).
        /// </summary>
        public byte WeaponIndex;

        /// <summary>
        /// Tick when hit occurred.
        /// </summary>
        public uint HitTick;
    }

    /// <summary>
    /// Simple damage profile (Phase 1: data-only).
    /// Phase 2: Extended with multipliers vs tags, area effects, etc.
    /// </summary>
    public struct DamageProfile : IComponentData
    {
        /// <summary>
        /// Base damage amount.
        /// </summary>
        public float BaseDamage;

        /// <summary>
        /// Damage type.
        /// </summary>
        public DamageType Type;

        /// <summary>
        /// Multiplier vs shield (1.0 = normal, >1.0 = effective vs shields).
        /// </summary>
        public float ShieldMultiplier;

        /// <summary>
        /// Multiplier vs armor (1.0 = normal, >1.0 = effective vs armor).
        /// </summary>
        public float ArmorMultiplier;

        /// <summary>
        /// Multiplier vs hull/organic (1.0 = normal).
        /// </summary>
        public float HullMultiplier;

        /// <summary>
        /// Creates default damage profile.
        /// </summary>
        public static DamageProfile Default => new DamageProfile
        {
            BaseDamage = 10f,
            Type = DamageType.Physical,
            ShieldMultiplier = 1f,
            ArmorMultiplier = 1f,
            HullMultiplier = 1f
        };
    }

    /// <summary>
    /// Faction relationship data (extends FactionId with relationships).
    /// FactionId stays on entities; this component describes relationships.
    /// Can be on a singleton or faction definition entity.
    /// Phase 1: Simple explicit fields for common factions.
    /// Phase 2: Use blob asset or hash map for scalability.
    /// </summary>
    public struct FactionRelationships : IComponentData
    {
        /// <summary>
        /// Faction ID this relationship data applies to.
        /// </summary>
        public int FactionId;

        // Phase 1: Explicit relationship fields for common factions (0-15)
        // Phase 2: Replace with blob asset or hash map
        public sbyte Relationship0;
        public sbyte Relationship1;
        public sbyte Relationship2;
        public sbyte Relationship3;
        public sbyte Relationship4;
        public sbyte Relationship5;
        public sbyte Relationship6;
        public sbyte Relationship7;
        public sbyte Relationship8;
        public sbyte Relationship9;
        public sbyte Relationship10;
        public sbyte Relationship11;
        public sbyte Relationship12;
        public sbyte Relationship13;
        public sbyte Relationship14;
        public sbyte Relationship15;

        /// <summary>
        /// Gets relationship to another faction (Phase 1: simple lookup for IDs 0-15).
        /// </summary>
        public sbyte GetRelationship(int otherFactionId)
        {
            return otherFactionId switch
            {
                0 => Relationship0,
                1 => Relationship1,
                2 => Relationship2,
                3 => Relationship3,
                4 => Relationship4,
                5 => Relationship5,
                6 => Relationship6,
                7 => Relationship7,
                8 => Relationship8,
                9 => Relationship9,
                10 => Relationship10,
                11 => Relationship11,
                12 => Relationship12,
                13 => Relationship13,
                14 => Relationship14,
                15 => Relationship15,
                _ => 0 // Neutral for invalid/unknown IDs
            };
        }

        /// <summary>
        /// Sets relationship to another faction (Phase 1: simple setter for IDs 0-15).
        /// </summary>
        public void SetRelationship(int otherFactionId, sbyte relationship)
        {
            switch (otherFactionId)
            {
                case 0: Relationship0 = relationship; break;
                case 1: Relationship1 = relationship; break;
                case 2: Relationship2 = relationship; break;
                case 3: Relationship3 = relationship; break;
                case 4: Relationship4 = relationship; break;
                case 5: Relationship5 = relationship; break;
                case 6: Relationship6 = relationship; break;
                case 7: Relationship7 = relationship; break;
                case 8: Relationship8 = relationship; break;
                case 9: Relationship9 = relationship; break;
                case 10: Relationship10 = relationship; break;
                case 11: Relationship11 = relationship; break;
                case 12: Relationship12 = relationship; break;
                case 13: Relationship13 = relationship; break;
                case 14: Relationship14 = relationship; break;
                case 15: Relationship15 = relationship; break;
            }
        }
    }

    /// <summary>
    /// Simple attack stats component for baseline combat (Milestone 6).
    /// </summary>
    public struct AttackStats : IComponentData
    {
        public float Damage;
        public float Range;
        public float AttackCooldown;
        public float LastAttackTime;
    }

    /// <summary>
    /// Simple defense stats component for baseline combat (Milestone 6).
    /// </summary>
    public struct DefenseStats : IComponentData
    {
        public float Armor;
        public float Evasion;
    }

    /// <summary>
    /// Combat intent component for individual behavior in groups (Milestone 6).
    /// </summary>
    public struct CombatIntent : IComponentData
    {
        public byte State;     // FollowGroup, Flank, Flee, Berserk…
        public Entity Target;  // optional override
    }

    /// <summary>
    /// Combat intent state enum.
    /// </summary>
    public enum CombatIntentState : byte
    {
        FollowGroup = 0,
        Flank = 1,
        Flee = 2,
        Berserk = 3,
        HoldPosition = 4
    }
}
