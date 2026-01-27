using Unity.Entities;

namespace PureDOTS.Runtime.Combat.Range
{
    /// <summary>
    /// Combat range configuration for an entity.
    /// Defines melee, ranged, and AOE distances.
    /// </summary>
    public struct CombatRange : IComponentData
    {
        /// <summary>
        /// Maximum melee attack range.
        /// </summary>
        public float MeleeRange;

        /// <summary>
        /// Minimum range for ranged attacks (can't shoot closer than this).
        /// </summary>
        public float RangedMinRange;

        /// <summary>
        /// Maximum range for ranged attacks.
        /// </summary>
        public float RangedMaxRange;

        /// <summary>
        /// Radius for area-of-effect attacks.
        /// </summary>
        public float AOERadius;

        /// <summary>
        /// Preferred engagement distance for AI.
        /// </summary>
        public float PreferredRange;

        /// <summary>
        /// Whether entity can attack in melee.
        /// </summary>
        public bool CanMelee;

        /// <summary>
        /// Whether entity can attack at range.
        /// </summary>
        public bool CanRanged;
    }

    /// <summary>
    /// Attack range type enumeration.
    /// </summary>
    public enum AttackRangeType : byte
    {
        /// <summary>
        /// Close combat attack.
        /// </summary>
        Melee = 0,

        /// <summary>
        /// Ranged attack (projectile, spell, etc.).
        /// </summary>
        Ranged = 1,

        /// <summary>
        /// Area of effect attack.
        /// </summary>
        AOE = 2
    }

    /// <summary>
    /// Range check result.
    /// </summary>
    public struct RangeCheckResult
    {
        /// <summary>
        /// Whether target is in range.
        /// </summary>
        public bool InRange;

        /// <summary>
        /// Distance to target.
        /// </summary>
        public float Distance;

        /// <summary>
        /// Best attack type for this range.
        /// </summary>
        public AttackRangeType BestAttackType;

        /// <summary>
        /// Whether target is too close (inside minimum range).
        /// </summary>
        public bool TooClose;

        /// <summary>
        /// Whether target is too far.
        /// </summary>
        public bool TooFar;
    }
}

