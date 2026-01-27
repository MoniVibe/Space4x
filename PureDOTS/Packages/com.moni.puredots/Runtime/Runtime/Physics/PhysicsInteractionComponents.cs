using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Physics
{
    /// <summary>
    /// Marks entity as participating in physics simulation.
    /// Only add PhysicsBody/PhysicsVelocity if this component exists.
    /// Use sparingly - physics is 10x slower than spatial grid queries.
    /// </summary>
    /// <remarks>
    /// Guidelines:
    /// - Default to spatial grid / distance checks (fast, deterministic, scalable)
    /// - Only add RequiresPhysics when:
    ///   - Player-visible spectacle requires realistic motion
    ///   - Gameplay mechanics depend on physics (pushing, momentum, rotation)
    ///   - Visual feedback needs realistic collision response
    /// - Rule: Use physics for less than 1% of entities (spectacle only)
    /// </remarks>
    public struct RequiresPhysics : IComponentData
    {
        /// <summary>
        /// Priority for physics simulation (0-255, higher = more important).
        /// When physics budget is exceeded, lower priority entities may be skipped.
        /// </summary>
        public byte Priority;

        /// <summary>
        /// Physics interaction flags.
        /// </summary>
        public PhysicsInteractionFlags Flags;
    }

    /// <summary>
    /// Flags for physics interaction behavior.
    /// </summary>
    [System.Flags]
    public enum PhysicsInteractionFlags : byte
    {
        None = 0,
        
        /// <summary>
        /// Entity can collide with other physics bodies.
        /// </summary>
        Collidable = 1 << 0,
        
        /// <summary>
        /// Entity responds to physics forces (not kinematic).
        /// </summary>
        Dynamic = 1 << 1,
        
        /// <summary>
        /// Entity triggers events but doesn't block movement.
        /// </summary>
        Trigger = 1 << 2,
        
        /// <summary>
        /// Entity uses continuous collision detection (for fast-moving objects).
        /// </summary>
        ContinuousCollision = 1 << 3,
        
        /// <summary>
        /// Entity should be pooled rather than destroyed.
        /// </summary>
        Pooled = 1 << 4
    }

    /// <summary>
    /// Marks entity as using spatial grid for interactions (default behavior).
    /// Entities without this or RequiresPhysics are assumed to use spatial grid.
    /// </summary>
    /// <remarks>
    /// Benefits of spatial grid over physics:
    /// - ~0.01ms per query (vs ~0.1ms for physics body update)
    /// - Fully deterministic
    /// - Scales to millions of entities
    /// - No physics body overhead
    /// </remarks>
    public struct UsesSpatialGrid : IComponentData
    {
        /// <summary>
        /// Query radius for spatial interactions.
        /// </summary>
        public float QueryRadius;

        /// <summary>
        /// Spatial query flags.
        /// </summary>
        public SpatialQueryFlags Flags;
    }

    /// <summary>
    /// Flags for spatial query behavior.
    /// </summary>
    [System.Flags]
    public enum SpatialQueryFlags : byte
    {
        None = 0,
        
        /// <summary>
        /// Include this entity in spatial grid queries.
        /// </summary>
        Queryable = 1 << 0,
        
        /// <summary>
        /// Entity can query the spatial grid.
        /// </summary>
        CanQuery = 1 << 1,
        
        /// <summary>
        /// Use distance-based checks (default).
        /// </summary>
        DistanceCheck = 1 << 2,
        
        /// <summary>
        /// Use raycast for line-of-sight checks.
        /// </summary>
        RaycastCheck = 1 << 3,
        
        /// <summary>
        /// Use overlap for area checks.
        /// </summary>
        OverlapCheck = 1 << 4
    }

    /// <summary>
    /// Configuration for physics interaction behavior per entity type.
    /// </summary>
    public struct PhysicsInteractionConfig : IComponentData
    {
        /// <summary>
        /// Mass for physics calculations.
        /// </summary>
        public float Mass;

        /// <summary>
        /// Collision radius.
        /// </summary>
        public float CollisionRadius;

        /// <summary>
        /// Restitution (bounciness) coefficient.
        /// </summary>
        public float Restitution;

        /// <summary>
        /// Friction coefficient.
        /// </summary>
        public float Friction;

        /// <summary>
        /// Linear damping (velocity decay).
        /// </summary>
        public float LinearDamping;

        /// <summary>
        /// Angular damping (rotation decay).
        /// </summary>
        public float AngularDamping;
    }

    /// <summary>
    /// Simplified ballistic motion for thrown objects (no full physics).
    /// Uses pre-computed trajectory instead of physics simulation.
    /// </summary>
    public struct BallisticMotion : IComponentData
    {
        /// <summary>
        /// Current velocity vector.
        /// </summary>
        public float3 Velocity;

        /// <summary>
        /// Gravity acceleration (typically negative Y).
        /// </summary>
        public float3 Gravity;

        /// <summary>
        /// Time in flight (seconds).
        /// </summary>
        public float FlightTime;

        /// <summary>
        /// Maximum flight time before auto-land.
        /// </summary>
        public float MaxFlightTime;

        /// <summary>
        /// Target position (for guided projectiles).
        /// </summary>
        public float3 TargetPosition;

        /// <summary>
        /// Motion flags.
        /// </summary>
        public BallisticMotionFlags Flags;
    }

    /// <summary>
    /// Flags for ballistic motion behavior.
    /// </summary>
    [System.Flags]
    public enum BallisticMotionFlags : byte
    {
        None = 0,
        
        /// <summary>
        /// Motion is active (entity is in flight).
        /// </summary>
        Active = 1 << 0,
        
        /// <summary>
        /// Use gravity in motion calculation.
        /// </summary>
        UseGravity = 1 << 1,
        
        /// <summary>
        /// Home toward target position.
        /// </summary>
        Homing = 1 << 2,
        
        /// <summary>
        /// Check for ground collision.
        /// </summary>
        GroundCollision = 1 << 3,
        
        /// <summary>
        /// Break on impact.
        /// </summary>
        BreakOnImpact = 1 << 4
    }

    /// <summary>
    /// Ground collision detection for ballistic objects.
    /// Uses terrain heightmap instead of full physics.
    /// </summary>
    public struct GroundCollisionCheck : IComponentData
    {
        /// <summary>
        /// Height offset from terrain.
        /// </summary>
        public float HeightOffset;

        /// <summary>
        /// Velocity threshold for breaking on impact.
        /// </summary>
        public float BreakVelocityThreshold;

        /// <summary>
        /// Last ground height check result.
        /// </summary>
        public float LastGroundHeight;

        /// <summary>
        /// Collision flags.
        /// </summary>
        public byte Flags;

        public const byte FlagHasCollided = 1 << 0;
        public const byte FlagShouldBreak = 1 << 1;
    }

    /// <summary>
    /// Helper methods for physics vs spatial grid decisions.
    /// </summary>
    public static class PhysicsInteractionHelpers
    {
        /// <summary>
        /// Calculates ballistic arc velocity for a throw from start to target.
        /// </summary>
        public static float3 CalculateBallisticArc(
            float3 startPosition, 
            float3 targetPosition, 
            float gravity,
            float flightTime)
        {
            if (flightTime <= 0f) return float3.zero;

            var displacement = targetPosition - startPosition;
            
            // Horizontal velocity (constant)
            var horizontalVelocity = new float3(
                displacement.x / flightTime,
                0f,
                displacement.z / flightTime);

            // Vertical velocity (accounting for gravity)
            // y = v0*t + 0.5*g*t^2 => v0 = (y - 0.5*g*t^2) / t
            var verticalVelocity = (displacement.y - 0.5f * gravity * flightTime * flightTime) / flightTime;

            return new float3(
                horizontalVelocity.x,
                verticalVelocity,
                horizontalVelocity.z);
        }

        /// <summary>
        /// Updates ballistic position using simple integration.
        /// </summary>
        public static void UpdateBallisticPosition(
            ref float3 position,
            ref float3 velocity,
            float3 gravity,
            float deltaTime)
        {
            // Update position
            position += velocity * deltaTime;
            
            // Update velocity (apply gravity)
            velocity += gravity * deltaTime;
        }

        /// <summary>
        /// Checks if entity should use physics or spatial grid.
        /// </summary>
        public static bool ShouldUsePhysics(
            bool hasRequiresPhysics,
            bool isSpectacle,
            bool needsCollisionResponse,
            float entityCount)
        {
            // Always use physics if explicitly marked
            if (hasRequiresPhysics) return true;

            // Use physics for spectacle moments
            if (isSpectacle) return true;

            // Use physics if collision response is needed
            if (needsCollisionResponse) return true;

            // Default to spatial grid for scale
            return false;
        }
    }
}

