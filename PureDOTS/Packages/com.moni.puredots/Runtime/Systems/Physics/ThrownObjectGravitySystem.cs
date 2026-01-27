using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Interaction;
using PureDOTS.Runtime.Time;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace PureDOTS.Systems.Physics
{
    /// <summary>
    /// Applies gravity to thrown objects (entities with BeingThrown component).
    /// Updates PhysicsVelocity.Linear each frame to simulate ballistic arcs.
    /// </summary>
    /// <remarks>
    /// Philosophy:
    /// - ECS is authoritative; we update velocity directly from ECS
    /// - Gravity is simulated in ECS, not by Unity Physics (Unity Physics gravity should be disabled)
    /// - Applies to all entities with BeingThrown (including kinematic bodies created by bootstrap)
    /// - Uses FixedDeltaTime for determinism
    /// - Respects rewind state (only runs in Record mode)
    /// 
    /// Integration Method:
    /// - Uses semi-implicit Euler: v += g*dt (this system), then x += v*dt (ThrownObjectTransformIntegratorSystem)
    /// - This is stable, deterministic, and game-feel-friendly
    /// - Note: This does NOT match analytic projectile math (x = v0*t + 0.5*g*t^2)
    /// - If you need "solve initial velocity to hit target", use the same integrator model
    /// </remarks>
    [BurstCompile]
    [UpdateInGroup(typeof(ThrownObjectPrePhysicsSystemGroup))]
    [UpdateBefore(typeof(Unity.Physics.Systems.BuildPhysicsWorld))]
    public partial struct ThrownObjectGravitySystem : ISystem
    {
        /// <summary>
        /// Gravity vector in m/s² (default: Earth gravity downward).
        /// Can be overridden via PhysicsConfig if needed.
        /// </summary>
        private static readonly float3 DefaultGravity = new float3(0f, -9.81f, 0f);

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<BeingThrown>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();

            // Only process in Record mode (not during playback/rewind)
            // This prevents re-simulating gravity during replay, maintaining determinism
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            if (timeState.IsPaused)
            {
                return;
            }

            // Use FixedDeltaTime for determinism (not frame-time dependent)
            float deltaTime = timeState.FixedDeltaTime;

            // Get gravity vector (could be from PhysicsConfig in future, for now use default)
            float3 gravity = DefaultGravity;

            // IMPORTANT: Unity Physics gravity must be disabled (PhysicsStep.Gravity = float3.zero)
            // to prevent double-gravity. This system is the single authority for thrown object gravity.
            // If Unity Physics gravity is enabled, thrown objects will experience double acceleration.
            // Note: We cannot assert this here because PhysicsStep is managed by Unity Physics systems.
            // Ensure PhysicsStep.Gravity is set to zero in physics configuration/bootstrap.

            // Process entities with PhysicsGravityFactor (most common case - bootstrap adds it)
            foreach (var (velocityRef, thrownRef, gravFactorRef) in SystemAPI
                         .Query<RefRW<PhysicsVelocity>, RefRW<BeingThrown>, RefRO<PhysicsGravityFactor>>())
            {
                // Read PhysicsGravityFactor but don't mutate it (avoid double-gravity if Unity Physics applies it)
                // Bootstrap sets it to 0, but we apply custom gravity regardless
                // Use local factor: if PhysicsGravityFactor is 0, use 1 for custom gravity (don't write back)
                float factor = gravFactorRef.ValueRO.Value;
                float factorForCustomGravity = factor > 0f ? factor : 1f; // Default to 1 if bootstrap set it to 0

                var velocity = velocityRef.ValueRO;

                // Apply gravity as a vector (scaled by custom factor and deltaTime)
                // Note: We apply gravity even to kinematic bodies (infinite mass) because BeingThrown
                // indicates this is a thrown object that should follow ballistic arcs
                // The velocity is updated directly in ECS, maintaining ECS authority
                var newVelocity = velocity;
                newVelocity.Linear += gravity * (factorForCustomGravity * deltaTime);
                velocityRef.ValueRW = newVelocity;

                // Update BeingThrown.TimeSinceThrow for tracking
                var thrown = thrownRef.ValueRO;
                thrown.TimeSinceThrow += deltaTime;
                thrownRef.ValueRW = thrown;
            }

            // Process entities without PhysicsGravityFactor (fallback for entities spawned differently)
            // Default to factor=1 if component is missing
            foreach (var (velocityRef, thrownRef) in SystemAPI
                         .Query<RefRW<PhysicsVelocity>, RefRW<BeingThrown>>()
                         .WithNone<PhysicsGravityFactor>())
            {
                var velocity = velocityRef.ValueRO;

                // Apply gravity with default factor of 1
                var newVelocity = velocity;
                newVelocity.Linear += gravity * deltaTime; // factor = 1.0
                velocityRef.ValueRW = newVelocity;

                // Update BeingThrown.TimeSinceThrow for tracking
                var thrown = thrownRef.ValueRO;
                thrown.TimeSinceThrow += deltaTime;
                thrownRef.ValueRW = thrown;
            }
        }
    }
}

