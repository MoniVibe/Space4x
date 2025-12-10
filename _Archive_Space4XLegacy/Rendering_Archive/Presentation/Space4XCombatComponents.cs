using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Presentation
{
    // ============================================================================
    // Combat Components
    // ============================================================================

    /// <summary>
    /// Marker component for projectile entities in the presentation layer.
    /// </summary>
    public struct ProjectilePresentationTag : IComponentData { }

    /// <summary>
    /// Visual state for projectiles.
    /// </summary>
    public enum ProjectileVisualStateType : byte
    {
        Laser = 0,
        Kinetic = 1,
        Missile = 2,
        Beam = 3
    }

    /// <summary>
    /// Per-projectile visual state component.
    /// </summary>
    public struct ProjectileVisualState : IComponentData
    {
        public ProjectileVisualStateType Type;
        public float Lifetime;
        public float MaxLifetime;
    }

    /// <summary>
    /// Trail renderer data for projectiles.
    /// </summary>
    public struct ProjectileTrail : IComponentData
    {
        public float Length;
        public float4 Color;
        public float Width;
    }

    /// <summary>
    /// Impact effect data for projectiles.
    /// </summary>
    public struct ProjectileImpact : IComponentData
    {
        public float3 Position;
        public float Radius;
        public ProjectileImpactType EffectType;
        public float Duration;
        public float Timer;
    }

    /// <summary>
    /// Impact effect type enumeration.
    /// </summary>
    public enum ProjectileImpactType : byte
    {
        Explosion = 0,
        BeamHit = 1,
        ShieldHit = 2,
        MissileHit = 3
    }

    /// <summary>
    /// Combat-specific state for entities.
    /// Must be provided by PureDOTS sim systems for combat visualization.
    /// See COMBAT_STATE_CONTRACT.md for integration requirements.
    /// </summary>
    public struct CombatState : IComponentData
    {
        /// <summary>True if entity is currently in combat</summary>
        public bool IsInCombat;
        /// <summary>Target entity being engaged (Entity.Null if none)</summary>
        public Entity TargetEntity;
        /// <summary>Health ratio (0-1), current health / max health</summary>
        public float HealthRatio;
        /// <summary>Shield ratio (0-1), current shields / max shields</summary>
        public float ShieldRatio;
        /// <summary>Last tick when damage was taken</summary>
        public uint LastDamageTick;
        /// <summary>Current engagement phase</summary>
        public CombatEngagementPhase Phase;
    }

    /// <summary>
    /// Combat engagement phase enumeration.
    /// </summary>
    public enum CombatEngagementPhase : byte
    {
        None = 0,
        Approach = 1,
        Exchange = 2,
        Disengage = 3
    }

    /// <summary>
    /// Damage flash effect data.
    /// </summary>
    public struct DamageFlash : IComponentData
    {
        /// <summary>Flash intensity (0-1)</summary>
        public float FlashIntensity;
        /// <summary>Flash color</summary>
        public float4 FlashColor;
        /// <summary>Flash duration in seconds</summary>
        public float FlashDuration;
        /// <summary>Current flash timer</summary>
        public float FlashTimer;
    }

    // ============================================================================
    // Extended Visual State Enums
    // ============================================================================

    /// <summary>
    /// Extended carrier visual state with combat states.
    /// </summary>
    public enum CarrierCombatVisualStateType : byte
    {
        Idle = 0,
        Patrolling = 1,
        Mining = 2,
        InCombat = 3,
        TakingDamage = 4,
        Shielded = 5,
        Destroyed = 6,
        Retreating = 7
    }

    /// <summary>
    /// Extended craft visual state with combat states.
    /// </summary>
    public enum CraftCombatVisualStateType : byte
    {
        Idle = 0,
        Mining = 1,
        Returning = 2,
        Docked = 3,
        Engaging = 4,
        Retreating = 5,
        Destroyed = 6
    }
}

