using Unity.Entities;
using Unity.Mathematics;
using PureDOTS.Runtime.Physics;

namespace Space4X.Physics
{
    /// <summary>
    /// Marker component for Space4X entities that participate in physics simulation.
    /// Entities with this component will have their ECS transforms synced to Havok bodies.
    /// </summary>
    /// <remarks>
    /// Philosophy:
    /// - ECS is authoritative for positions, velocities, and gameplay state
    /// - Havok is used for collision detection and queries only
    /// - All physics bodies are kinematic (driven by ECS movement systems)
    /// </remarks>
    public struct SpacePhysicsBody : IComponentData
    {
        /// <summary>
        /// Physics layer for collision filtering.
        /// </summary>
        public Space4XPhysicsLayer Layer;

        /// <summary>
        /// Priority for physics processing (0-255, higher = more important).
        /// When physics budget is exceeded, lower priority entities may be skipped.
        /// </summary>
        public byte Priority;

        /// <summary>
        /// Flags for physics behavior.
        /// </summary>
        public SpacePhysicsFlags Flags;
    }

    /// <summary>
    /// Flags for Space4X physics behavior.
    /// </summary>
    [System.Flags]
    public enum SpacePhysicsFlags : byte
    {
        None = 0,

        /// <summary>
        /// Entity generates collision events.
        /// </summary>
        RaisesCollisionEvents = 1 << 0,

        /// <summary>
        /// Entity is a trigger (no physical response, just events).
        /// </summary>
        IsTrigger = 1 << 1,

        /// <summary>
        /// Entity uses continuous collision detection (for fast-moving objects like projectiles).
        /// </summary>
        ContinuousCollision = 1 << 2,

        /// <summary>
        /// Entity is currently active in physics simulation.
        /// </summary>
        IsActive = 1 << 3,

        /// <summary>
        /// Entity was recently involved in a collision.
        /// </summary>
        HasRecentCollision = 1 << 4
    }

    /// <summary>
    /// Collider configuration for Space4X entities.
    /// </summary>
    public struct SpaceColliderData : IComponentData
    {
        /// <summary>
        /// Radius for sphere/capsule colliders.
        /// </summary>
        public float Radius;

        /// <summary>
        /// Size for box colliders (x, y, z dimensions).
        /// </summary>
        public float3 Size;

        /// <summary>
        /// Height for capsule colliders.
        /// </summary>
        public float Height;

        /// <summary>
        /// Collider type.
        /// </summary>
        public ColliderType Type;

        /// <summary>
        /// Center offset from entity transform.
        /// </summary>
        public float3 CenterOffset;

        /// <summary>
        /// Creates a sphere collider configuration.
        /// </summary>
        public static SpaceColliderData CreateSphere(float radius, float3 centerOffset = default)
        {
            return new SpaceColliderData
            {
                Type = ColliderType.Sphere,
                Radius = radius,
                CenterOffset = centerOffset
            };
        }

        /// <summary>
        /// Creates a box collider configuration.
        /// </summary>
        public static SpaceColliderData CreateBox(float3 size, float3 centerOffset = default)
        {
            return new SpaceColliderData
            {
                Type = ColliderType.Box,
                Size = size,
                CenterOffset = centerOffset
            };
        }

        /// <summary>
        /// Creates a capsule collider configuration.
        /// </summary>
        public static SpaceColliderData CreateCapsule(float radius, float height, float3 centerOffset = default)
        {
            return new SpaceColliderData
            {
                Type = ColliderType.Capsule,
                Radius = radius,
                Height = height,
                CenterOffset = centerOffset
            };
        }
    }

    /// <summary>
    /// Velocity component for Space4X entities.
    /// Synced to Havok PhysicsVelocity for kinematic bodies.
    /// </summary>
    public struct SpaceVelocity : IComponentData
    {
        /// <summary>
        /// Linear velocity in world space.
        /// </summary>
        public float3 Linear;

        /// <summary>
        /// Angular velocity (rotation speed) in world space.
        /// </summary>
        public float3 Angular;
    }

    /// <summary>
    /// Collision event buffer element for Space4X entities.
    /// Populated by PhysicsEventSystem after physics step.
    /// </summary>
    public struct SpaceCollisionEvent : IBufferElementData
    {
        /// <summary>
        /// The other entity involved in the collision.
        /// </summary>
        public Entity OtherEntity;

        /// <summary>
        /// Contact point in world space.
        /// </summary>
        public float3 ContactPoint;

        /// <summary>
        /// Contact normal (pointing away from this entity).
        /// </summary>
        public float3 ContactNormal;

        /// <summary>
        /// Relative velocity at contact point.
        /// </summary>
        public float3 RelativeVelocity;

        /// <summary>
        /// Collision impulse magnitude.
        /// </summary>
        public float ImpulseMagnitude;

        /// <summary>
        /// Tick when collision occurred.
        /// </summary>
        public uint Tick;

        /// <summary>
        /// Type of collision event.
        /// </summary>
        public CollisionEventType EventType;
    }

    /// <summary>
    /// Type of collision event.
    /// </summary>
    public enum CollisionEventType : byte
    {
        /// <summary>
        /// Initial contact (enter).
        /// </summary>
        Enter = 0,

        /// <summary>
        /// Ongoing contact (stay).
        /// </summary>
        Stay = 1,

        /// <summary>
        /// Contact ended (exit).
        /// </summary>
        Exit = 2,

        /// <summary>
        /// Trigger enter (no physical response).
        /// </summary>
        TriggerEnter = 3,

        /// <summary>
        /// Trigger exit.
        /// </summary>
        TriggerExit = 4
    }

    /// <summary>
    /// Tag component for entities that need physics initialization.
    /// Removed after PhysicsBodyBootstrapSystem processes the entity.
    /// </summary>
    public struct NeedsPhysicsSetup : IComponentData { }

    /// <summary>
    /// Tag component for entities with active physics colliders.
    /// Added by PhysicsBodyBootstrapSystem after setup.
    /// </summary>
    public struct HasPhysicsCollider : IComponentData { }
}

