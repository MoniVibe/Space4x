using Space4X.Physics;
using PureDOTS.Runtime;
using PureDOTS.Runtime.Physics;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component for Space4X vessels that need physics collision detection.
    /// Adds SpacePhysicsBody, SpaceColliderData, and related components.
    /// </summary>
    /// <remarks>
    /// Philosophy:
    /// - ECS is authoritative; physics bodies are kinematic
    /// - Havok is used for collision detection and queries only
    /// - Vessels are driven by ECS movement systems, not physics forces
    /// </remarks>
    public class Space4XVesselPhysicsAuthoring : MonoBehaviour
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
        public Space4XPhysicsLayer layer = Space4XPhysicsLayer.Ship;

        [Header("Behavior Flags")]
        [Tooltip("Entity generates collision events")]
        public bool raisesCollisionEvents = true;

        [Tooltip("Entity is a trigger (no physical response)")]
        public bool isTrigger = false;

        [Tooltip("Use continuous collision detection (for fast-moving objects)")]
        public bool continuousCollision = false;

        [Header("Material Properties")]
        [Tooltip("Material hardness (resistance to deformation). Rock: 2.0, Ship: 1.5, Soft: 0.5")]
        public float hardness = 1.5f;

        [Tooltip("Material fragility (how easily it shatters). Brittle rock: 1.5, Durable: 0.5, Ship: 0.1")]
        public float fragility = 0.1f;

        [Tooltip("Material density (for mass calculations). Rock: 3.0, Ship: 2.0, Soft: 0.8")]
        public float density = 2f;

        [Header("Impact Physics")]
        [Tooltip("Authored impact mass used by collision damage math when body is kinematic.")]
        [Min(0.05f)] public float impactMass = 8f;

        [Header("Priority")]
        [Tooltip("Physics processing priority (0-255, higher = more important)")]
        [Range(0, 255)]
        public int priority = 100;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.5f);
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
    /// Baker for Space4X vessel physics authoring.
    /// </summary>
    public class Space4XVesselPhysicsBaker : Baker<Space4XVesselPhysicsAuthoring>
    {
        public override void Bake(Space4XVesselPhysicsAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);

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
            var interactionFlags = PhysicsInteractionFlags.Collidable;
            if (authoring.isTrigger)
            {
                interactionFlags |= PhysicsInteractionFlags.Trigger;
            }
            if (authoring.continuousCollision)
            {
                interactionFlags |= PhysicsInteractionFlags.ContinuousCollision;
            }

            AddComponent(entity, new RequiresPhysics
            {
                Priority = (byte)authoring.priority,
                Flags = interactionFlags
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

            // Add PhysicsColliderSpec so bootstrap uses the intended shape + filter.
            var spec = new PhysicsColliderSpec
            {
                Shape = authoring.colliderType switch
                {
                    ColliderType.Capsule => PhysicsColliderShape.Capsule,
                    ColliderType.Box => PhysicsColliderShape.Box,
                    _ => PhysicsColliderShape.Sphere
                },
                Dimensions = authoring.colliderType switch
                {
                    ColliderType.Capsule => new float3(authoring.radius, authoring.height, 0f),
                    ColliderType.Box => new float3(authoring.size.x, authoring.size.y, authoring.size.z),
                    _ => new float3(authoring.radius, 0f, 0f)
                },
                Flags = interactionFlags,
                IsTrigger = (byte)(authoring.isTrigger ||
                    authoring.layer == Space4XPhysicsLayer.SensorOnly ||
                    authoring.layer == Space4XPhysicsLayer.DockingZone ? 1 : 0),
                UseCustomFilter = 1,
                CustomFilter = Space4XPhysicsLayers.CreateFilter(authoring.layer)
            };
            AddComponent(entity, spec);

            // Add collision event buffer if events are enabled
            if (authoring.raisesCollisionEvents)
            {
                AddBuffer<SpaceCollisionEvent>(entity);
                AddBuffer<PhysicsCollisionEventElement>(entity);
            }

            // Add NeedsPhysicsSetup tag for bootstrap system
            AddComponent(entity, new NeedsPhysicsSetup());

            // Add material stats for material-aware damage calculation
            AddComponent(entity, new MaterialStats
            {
                Hardness = authoring.hardness,
                Fragility = authoring.fragility,
                Density = authoring.density
            });
        }
    }
}
