using Space4X.Physics;
using PureDOTS.Runtime;
using PureDOTS.Runtime.Physics;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component for Space4X rocks (throwable, mineable physics objects).
    /// Creates a rock entity with physics, throwability, and optional resource deposits.
    /// </summary>
    /// <remarks>
    /// Rocks are throwable test dummies that can also serve as resource deposits.
    /// They integrate with the hand interaction system and mining systems.
    /// </remarks>
    public class Space4XRockAuthoring : MonoBehaviour
    {
        [Header("Collider Settings")]
        [Tooltip("Type of collider to use")]
        public ColliderType colliderType = ColliderType.Sphere;

        [Tooltip("Radius for sphere/capsule colliders")]
        public float radius = 1f;

        [Tooltip("Size for box colliders (x, y, z)")]
        public Vector3 size = Vector3.one;

        [Tooltip("Height for capsule colliders")]
        public float height = 2f;

        [Tooltip("Center offset from transform")]
        public Vector3 centerOffset = Vector3.zero;

        [Header("Physics Layer")]
        [Tooltip("Physics layer for collision filtering")]
        public Space4XPhysicsLayer layer = Space4XPhysicsLayer.Asteroid;

        [Header("Destructible")]
        [Tooltip("Is this rock destructible?")]
        public bool isDestructible = true;

        [Tooltip("Hit points (if destructible)")]
        public float hitPoints = 100f;

        [Header("Impact Damage")]
        [Tooltip("Does this rock deal impact damage?")]
        public bool dealsImpactDamage = true;

        [Tooltip("Damage per unit of collision impulse")]
        public float damagePerImpulse = 10f;

        [Tooltip("Minimum impulse to count as a hit")]
        public float minImpulse = 1f;

        [Header("Resource Deposit")]
        [Tooltip("Is this rock a resource deposit?")]
        public bool isResourceDeposit = false;

        [Tooltip("Resource type ID (index into ResourceTypeIndex catalog)")]
        public int resourceTypeId = 0;

        [Tooltip("Current amount of resource")]
        public float resourceAmount = 1000f;

        [Tooltip("Maximum amount (for UI/regen)")]
        public float maxResourceAmount = 1000f;

        [Tooltip("Regeneration per second (0 for non-regenerating)")]
        public float regenPerSecond = 0f;

        [Header("Behavior Flags")]
        [Tooltip("Entity generates collision events")]
        public bool raisesCollisionEvents = true;

        [Tooltip("Entity is a trigger (no physical response)")]
        public bool isTrigger = false;

        [Tooltip("Use continuous collision detection")]
        public bool continuousCollision = false;

        [Header("Material Properties")]
        [Tooltip("Material hardness (resistance to deformation). Rock: 2.0, Ship: 1.5, Soft: 0.5")]
        public float hardness = 2f;

        [Tooltip("Material fragility (how easily it shatters). Brittle rock: 1.5, Durable: 0.5")]
        public float fragility = 0.5f;

        [Tooltip("Material density (for mass calculations). Rock: 3.0, Ship: 2.0, Soft: 0.8")]
        public float density = 3f;

        [Header("Impact Physics")]
        [Tooltip("Authored impact mass used by collision damage math when body is kinematic.")]
        [Min(0.05f)] public float impactMass = 3f;

        [Header("Priority")]
        [Tooltip("Physics processing priority (0-255)")]
        [Range(0, 255)]
        public int priority = 100;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.5f, 0.3f, 0.1f, 0.5f); // Brown for rocks
            var center = transform.position + centerOffset;

            switch (colliderType)
            {
                case ColliderType.Sphere:
                    Gizmos.DrawWireSphere(center, radius);
                    break;
                case ColliderType.Box:
                    Gizmos.DrawWireCube(center, size);
                    break;
                case ColliderType.Capsule:
                    // Draw capsule approximation
                    Gizmos.DrawWireSphere(center + Vector3.up * (height * 0.5f - radius), radius);
                    Gizmos.DrawWireSphere(center - Vector3.up * (height * 0.5f - radius), radius);
                    break;
            }
        }
    }

    /// <summary>
    /// Baker for Space4X rock authoring.
    /// </summary>
    public class Space4XRockBaker : Baker<Space4XRockAuthoring>
    {
        public override void Bake(Space4XRockAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);

            // Add rock tags
            AddComponent<RockTag>(entity);
            AddComponent<ThrowableTag>(entity);

            // Add resource node tag if it's a resource deposit
            if (authoring.isResourceDeposit)
            {
                AddComponent<ResourceNodeTag>(entity);
                AddComponent(entity, new ResourceDeposit
                {
                    ResourceTypeId = authoring.resourceTypeId,
                    CurrentAmount = authoring.resourceAmount,
                    MaxAmount = authoring.maxResourceAmount,
                    RegenPerSecond = authoring.regenPerSecond
                });
            }

            // Add destructible if enabled
            if (authoring.isDestructible)
            {
                AddComponent(entity, new Destructible
                {
                    HitPoints = authoring.hitPoints,
                    MaxHitPoints = authoring.hitPoints
                });
            }

            // Add impact damage if enabled
            if (authoring.dealsImpactDamage)
            {
                AddComponent(entity, new ImpactDamage
                {
                    DamagePerImpulse = authoring.damagePerImpulse,
                    MinImpulse = authoring.minImpulse
                });
            }

            // Add material stats (always added for material-aware damage calculation)
            AddComponent(entity, new MaterialStats
            {
                Hardness = authoring.hardness,
                Fragility = authoring.fragility,
                Density = authoring.density
            });

            // Build physics flags
            var flags = SpacePhysicsFlags.IsActive;
            if (authoring.raisesCollisionEvents)
                flags |= SpacePhysicsFlags.RaisesCollisionEvents;
            if (authoring.isTrigger)
                flags |= SpacePhysicsFlags.IsTrigger;
            if (authoring.continuousCollision)
                flags |= SpacePhysicsFlags.ContinuousCollision;

            // Add SpacePhysicsBody marker
            AddComponent(entity, new SpacePhysicsBody
            {
                Layer = authoring.layer,
                Priority = (byte)authoring.priority,
                Flags = flags
            });

            // Add SpaceColliderData
            AddComponent(entity, new SpaceColliderData
            {
                Type = authoring.colliderType,
                Radius = authoring.radius,
                Size = new float3(authoring.size.x, authoring.size.y, authoring.size.z),
                Height = authoring.height,
                CenterOffset = new float3(authoring.centerOffset.x, authoring.centerOffset.y, authoring.centerOffset.z)
            });

            // Add SpaceVelocity for velocity tracking
            AddComponent(entity, new SpaceVelocity
            {
                Linear = float3.zero,
                Angular = float3.zero
            });

            // Add RequiresPhysics from PureDOTS
            AddComponent(entity, new RequiresPhysics
            {
                Priority = (byte)authoring.priority,
                Flags = authoring.raisesCollisionEvents 
                    ? PhysicsInteractionFlags.Collidable 
                    : PhysicsInteractionFlags.None
            });

            // Add PhysicsInteractionConfig
            AddComponent(entity, new PhysicsInteractionConfig
            {
                Mass = math.max(0.05f, authoring.impactMass),
                CollisionRadius = authoring.radius,
                Restitution = 0f,
                Friction = 0f,
                LinearDamping = 0f,
                AngularDamping = 0f
            });

            // Add collision event buffer if events are enabled
            if (authoring.raisesCollisionEvents)
            {
                AddBuffer<SpaceCollisionEvent>(entity);
                AddBuffer<PhysicsCollisionEventElement>(entity);
            }

            // Add NeedsPhysicsSetup tag for bootstrap system
            AddComponent(entity, new NeedsPhysicsSetup());
        }
    }
}
