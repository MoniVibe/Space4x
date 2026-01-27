using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.AI
{
    /// <summary>
    /// Steering behavior types.
    /// </summary>
    public enum SteeringBehavior : byte
    {
        /// <summary>No steering active.</summary>
        None = 0,
        /// <summary>Move toward target.</summary>
        Seek = 1,
        /// <summary>Move away from target.</summary>
        Flee = 2,
        /// <summary>Seek with deceleration near target.</summary>
        Arrive = 3,
        /// <summary>Random movement.</summary>
        Wander = 4,
        /// <summary>Intercept moving target.</summary>
        Pursue = 5,
        /// <summary>Flee from predicted position.</summary>
        Evade = 6,
        /// <summary>Move with group.</summary>
        Flocking = 7,
        /// <summary>Follow a path.</summary>
        PathFollow = 8,
        /// <summary>Avoid obstacles.</summary>
        ObstacleAvoidance = 9,
        /// <summary>Circle around target.</summary>
        Orbit = 10,
        /// <summary>Maintain formation position.</summary>
        Formation = 11
    }

    /// <summary>
    /// Output from steering calculations.
    /// </summary>
    public struct SteeringOutput : IComponentData
    {
        /// <summary>
        /// Desired velocity vector.
        /// </summary>
        public float3 DesiredVelocity;

        /// <summary>
        /// Desired rotation (euler angles or quaternion components).
        /// </summary>
        public float3 DesiredRotation;

        /// <summary>
        /// Currently active steering behavior.
        /// </summary>
        public SteeringBehavior ActiveBehavior;

        /// <summary>
        /// Blend weight for this steering output (0-1).
        /// </summary>
        public float Weight;

        /// <summary>
        /// Has valid steering data.
        /// </summary>
        public bool IsValid;
    }

    /// <summary>
    /// Configuration for steering behaviors.
    /// </summary>
    public struct SteeringConfig : IComponentData
    {
        /// <summary>
        /// Maximum speed.
        /// </summary>
        public float MaxSpeed;

        /// <summary>
        /// Maximum acceleration.
        /// </summary>
        public float MaxAcceleration;

        /// <summary>
        /// Maximum rotation speed (degrees/second).
        /// </summary>
        public float MaxRotationSpeed;

        /// <summary>
        /// Distance at which to start slowing for Arrive.
        /// </summary>
        public float ArriveSlowingRadius;

        /// <summary>
        /// Distance considered "arrived".
        /// </summary>
        public float ArriveStopRadius;

        /// <summary>
        /// Wander circle radius.
        /// </summary>
        public float WanderRadius;

        /// <summary>
        /// Wander circle distance ahead.
        /// </summary>
        public float WanderDistance;

        /// <summary>
        /// Maximum wander angle change per update.
        /// </summary>
        public float WanderJitter;

        /// <summary>
        /// Distance to look ahead for obstacles.
        /// </summary>
        public float ObstacleAvoidanceDistance;

        /// <summary>
        /// Creates default steering config.
        /// </summary>
        public static SteeringConfig Default => new SteeringConfig
        {
            MaxSpeed = 5f,
            MaxAcceleration = 10f,
            MaxRotationSpeed = 180f,
            ArriveSlowingRadius = 5f,
            ArriveStopRadius = 0.5f,
            WanderRadius = 2f,
            WanderDistance = 4f,
            WanderJitter = 40f,
            ObstacleAvoidanceDistance = 10f
        };
    }

    /// <summary>
    /// Steering target for movement behaviors.
    /// </summary>
    public struct SteeringTarget : IComponentData
    {
        /// <summary>
        /// Target entity to steer toward/away from.
        /// </summary>
        public Entity TargetEntity;

        /// <summary>
        /// Target position (if no entity).
        /// </summary>
        public float3 TargetPosition;

        /// <summary>
        /// Target's velocity (for pursuit/evade).
        /// </summary>
        public float3 TargetVelocity;

        /// <summary>
        /// Use entity or position?
        /// </summary>
        public bool UseEntity;

        /// <summary>
        /// Desired behavior for this target.
        /// </summary>
        public SteeringBehavior Behavior;

        /// <summary>
        /// Priority weight for blending multiple behaviors.
        /// </summary>
        public float Priority;
    }

    /// <summary>
    /// State for wander behavior (needs persistence).
    /// </summary>
    public struct WanderState : IComponentData
    {
        /// <summary>
        /// Current wander angle.
        /// </summary>
        public float WanderAngle;

        /// <summary>
        /// Random seed for deterministic wander.
        /// </summary>
        public uint RandomSeed;

        /// <summary>
        /// Last update tick.
        /// </summary>
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Flocking behavior configuration.
    /// </summary>
    public struct FlockingConfig : IComponentData
    {
        /// <summary>
        /// Radius for separation behavior.
        /// </summary>
        public float SeparationRadius;

        /// <summary>
        /// Radius for alignment behavior.
        /// </summary>
        public float AlignmentRadius;

        /// <summary>
        /// Radius for cohesion behavior.
        /// </summary>
        public float CohesionRadius;

        /// <summary>
        /// Weight for separation.
        /// </summary>
        public float SeparationWeight;

        /// <summary>
        /// Weight for alignment.
        /// </summary>
        public float AlignmentWeight;

        /// <summary>
        /// Weight for cohesion.
        /// </summary>
        public float CohesionWeight;

        /// <summary>
        /// Group ID for flocking (only flock with same group).
        /// </summary>
        public int GroupId;

        /// <summary>
        /// Creates default flocking config.
        /// </summary>
        public static FlockingConfig Default => new FlockingConfig
        {
            SeparationRadius = 2f,
            AlignmentRadius = 5f,
            CohesionRadius = 10f,
            SeparationWeight = 1.5f,
            AlignmentWeight = 1f,
            CohesionWeight = 1f,
            GroupId = 0
        };
    }

    /// <summary>
    /// Formation position assignment.
    /// </summary>
    public struct FormationSlot : IComponentData
    {
        /// <summary>
        /// Entity this agent is following in formation.
        /// </summary>
        public Entity LeaderEntity;

        /// <summary>
        /// Offset from leader position.
        /// </summary>
        public float3 OffsetFromLeader;

        /// <summary>
        /// Slot index in formation.
        /// </summary>
        public byte SlotIndex;

        /// <summary>
        /// Formation type ID.
        /// </summary>
        public byte FormationType;
    }

    /// <summary>
    /// Burst-compatible steering calculations.
    /// </summary>
    public static class SteeringCalculations
    {
        /// <summary>
        /// Calculate seek steering toward a target.
        /// </summary>
        public static float3 Seek(float3 position, float3 target, float3 currentVelocity, float maxSpeed)
        {
            var desired = math.normalize(target - position) * maxSpeed;
            return desired - currentVelocity;
        }

        /// <summary>
        /// Calculate flee steering away from a target.
        /// </summary>
        public static float3 Flee(float3 position, float3 target, float3 currentVelocity, float maxSpeed)
        {
            var desired = math.normalize(position - target) * maxSpeed;
            return desired - currentVelocity;
        }

        /// <summary>
        /// Calculate arrive steering with deceleration.
        /// </summary>
        public static float3 Arrive(
            float3 position,
            float3 target,
            float3 currentVelocity,
            float maxSpeed,
            float slowingRadius,
            float stopRadius)
        {
            var toTarget = target - position;
            var distance = math.length(toTarget);

            if (distance < stopRadius)
            {
                return -currentVelocity; // Stop
            }

            var speed = distance < slowingRadius
                ? maxSpeed * (distance / slowingRadius)
                : maxSpeed;

            var desired = (toTarget / distance) * speed;
            return desired - currentVelocity;
        }

        /// <summary>
        /// Calculate wander steering.
        /// </summary>
        public static float3 Wander(
            float3 position,
            float3 forward,
            ref float wanderAngle,
            float wanderRadius,
            float wanderDistance,
            float wanderJitter,
            uint seed)
        {
            // Add jitter to wander angle
            var random = new Unity.Mathematics.Random(seed);
            wanderAngle += random.NextFloat(-wanderJitter, wanderJitter) * math.TORADIANS;

            // Calculate wander circle center
            var circleCenter = position + forward * wanderDistance;

            // Calculate target on circle
            var displacement = new float3(
                math.cos(wanderAngle) * wanderRadius,
                0f,
                math.sin(wanderAngle) * wanderRadius
            );

            return math.normalize(circleCenter + displacement - position);
        }

        /// <summary>
        /// Calculate pursuit steering (intercept moving target).
        /// </summary>
        public static float3 Pursue(
            float3 position,
            float3 targetPosition,
            float3 targetVelocity,
            float3 currentVelocity,
            float maxSpeed)
        {
            var toTarget = targetPosition - position;
            var distance = math.length(toTarget);
            var speed = math.length(currentVelocity);
            
            // Estimate time to intercept
            var lookAheadTime = speed > 0.0001f ? distance / speed : 1f;
            lookAheadTime = math.min(lookAheadTime, 2f); // Cap prediction
            
            // Predict target position
            var predictedPosition = targetPosition + targetVelocity * lookAheadTime;
            
            return Seek(position, predictedPosition, currentVelocity, maxSpeed);
        }

        /// <summary>
        /// Calculate evade steering (flee from predicted position).
        /// </summary>
        public static float3 Evade(
            float3 position,
            float3 targetPosition,
            float3 targetVelocity,
            float3 currentVelocity,
            float maxSpeed)
        {
            var toTarget = targetPosition - position;
            var distance = math.length(toTarget);
            var speed = math.length(currentVelocity);
            
            var lookAheadTime = speed > 0.0001f ? distance / speed : 1f;
            lookAheadTime = math.min(lookAheadTime, 2f);
            
            var predictedPosition = targetPosition + targetVelocity * lookAheadTime;
            
            return Flee(position, predictedPosition, currentVelocity, maxSpeed);
        }
    }
}

