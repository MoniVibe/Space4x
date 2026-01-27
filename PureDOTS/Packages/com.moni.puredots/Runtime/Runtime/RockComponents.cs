using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime
{
    /// <summary>
    /// Tag component identifying rock objects.
    /// Used for collision handling, visual identification, and game-specific rock behaviors.
    /// </summary>
    public struct RockTag : IComponentData { }

    /// <summary>
    /// Tag component marking entities that can be picked up by the hand.
    /// Hand interaction systems should check for this tag when raycasting.
    /// </summary>
    public struct ThrowableTag : IComponentData { }

    /// <summary>
    /// Tag component marking entities that can be mined for resources.
    /// Mining systems should query for this tag along with ResourceDeposit.
    /// </summary>
    public struct ResourceNodeTag : IComponentData { }

    /// <summary>
    /// Tag component identifying tree resource nodes (wood).
    /// </summary>
    public struct TreeTag : IComponentData { }

    /// <summary>
    /// Tag component identifying stone resource nodes.
    /// </summary>
    public struct StoneNodeTag : IComponentData { }

    /// <summary>
    /// Tag component identifying ore resource nodes.
    /// </summary>
    public struct OreNodeTag : IComponentData { }

    /// <summary>
    /// Component tracking health/destruction state for destructible entities.
    /// Used by collision systems to apply damage and handle destruction.
    /// </summary>
    public struct Destructible : IComponentData
    {
        /// <summary>
        /// Current hit points.
        /// </summary>
        public float HitPoints;

        /// <summary>
        /// Maximum hit points (for regeneration or UI display).
        /// </summary>
        public float MaxHitPoints;
    }

    /// <summary>
    /// Component defining how an entity deals impact damage on collision.
    /// Used by collision response systems to calculate damage based on collision impulse.
    /// </summary>
    public struct ImpactDamage : IComponentData
    {
        /// <summary>
        /// Damage dealt per unit of collision impulse (e.g., HP per unit impulse).
        /// </summary>
        public float DamagePerImpulse;

        /// <summary>
        /// Minimum impulse threshold to count as a "hit" (below this, no damage).
        /// </summary>
        public float MinImpulse;
    }

    /// <summary>
    /// Component defining a resource deposit that can be mined.
    /// Plugs into the ResourceRegistry system for spatial queries and mining integration.
    /// </summary>
    public struct ResourceDeposit : IComponentData
    {
        /// <summary>
        /// Resource type index into the ResourceTypeIndex catalog.
        /// </summary>
        public int ResourceTypeId;

        /// <summary>
        /// Current amount of resource remaining.
        /// </summary>
        public float CurrentAmount;

        /// <summary>
        /// Maximum amount (for UI display or regeneration calculations).
        /// </summary>
        public float MaxAmount;

        /// <summary>
        /// Regeneration rate per second (0 for non-regenerating deposits).
        /// </summary>
        public float RegenPerSecond;
    }

    /// <summary>
    /// Tag component marking a resource deposit as depleted.
    /// Added when CurrentAmount reaches 0. Systems can query for this to handle cleanup.
    /// </summary>
    public struct DepletedTag : IComponentData { }
}




