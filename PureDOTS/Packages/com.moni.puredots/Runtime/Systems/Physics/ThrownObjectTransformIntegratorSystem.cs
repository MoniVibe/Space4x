using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Interaction;
using PureDOTS.Runtime.Time;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace PureDOTS.Systems.Physics
{
    /// <summary>
    /// Integrates PhysicsVelocity into LocalTransform for thrown objects.
    /// Required because thrown objects are kinematic (Unity Physics doesn't integrate their velocity).
    /// Runs after gravity is applied but before BuildPhysicsWorld.
    /// </summary>
    /// <remarks>
    /// Philosophy:
    /// - ECS is authoritative; we update transform directly from velocity
    /// - Kinematic bodies don't have their velocity integrated by Unity Physics
    /// - This system ensures thrown objects actually move when velocity changes
    /// - Uses FixedDeltaTime for determinism
    /// - Respects rewind state (only runs in Record mode)
    /// - ONLY processes kinematic bodies (InverseMass == 0) to avoid double-movement of dynamic bodies
    ///
    /// Integration Method:
    /// - Uses semi-implicit Euler: v += g*dt (gravity system), then x += v*dt (this system)
    /// - Angular velocity integrated via axis-angle (quaternion.AxisAngle) + normalization
    /// - Stores previous pose (position + rotation) for tunneling sweep tests
    /// - This is stable, deterministic, and game-feel-friendly
    /// - Note: This does NOT match analytic projectile math (x = v0*t + 0.5*g*t^2)
    /// - If you need "solve initial velocity to hit target", use the same integrator model
    /// 
    /// Collision Behavior:
    /// - Kinematic bodies won't bounce/deflect automatically via Unity Physics
    /// - Collision response is handled by MiracleTokenImpactSystem (stops on impact)
    /// - If bounce/deflection is needed, consider making thrown objects dynamic (finite mass)
    /// 
    /// Tunneling Risk:
    /// - Fast throws can tunnel through thin colliders (discrete position updates, no sweep/CCD)
    /// - Mitigations:
    ///   1. Make thrown objects dynamic during flight + use Unity's speculative/CCD (best for bounce/stop)
    ///   2. Add deterministic sweep test per tick (ray/sphere cast from oldPos→newPos), clamp to hit point
    /// - Current implementation relies on collision events; may miss impacts at high speeds
    /// </remarks>
    [BurstCompile]
    [UpdateInGroup(typeof(Unity.Physics.Systems.PhysicsSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(ThrownObjectGravitySystem))]
    [UpdateBefore(typeof(Unity.Physics.Systems.BuildPhysicsWorld))]
    public partial struct ThrownObjectTransformIntegratorSystem : ISystem
    {
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
            // This prevents re-integrating transforms during replay, maintaining determinism
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

            // Integrate velocity -> position/rotation for kinematic thrown objects only
            // Dynamic bodies (finite mass) are integrated by Unity Physics, so we skip them
            
            // Process entities with PhysicsMass (most common case - bootstrap adds it)
            foreach (var (transformRef, velocityRef, massRef, thrownRef) in SystemAPI
                         .Query<RefRW<LocalTransform>, RefRO<PhysicsVelocity>, RefRO<PhysicsMass>, RefRW<BeingThrown>>()
                         .WithAll<BeingThrown>())
            {
                var mass = massRef.ValueRO;

                // Only integrate kinematic bodies (InverseMass == 0 means infinite mass = kinematic)
                // Dynamic bodies (InverseMass > 0) are moved by Unity Physics, so we skip them
                if (mass.InverseMass > 0f)
                {
                    continue; // Dynamic body - Unity Physics will integrate it
                }

                var transform = transformRef.ValueRO;
                var velocity = velocityRef.ValueRO;
                var thrown = thrownRef.ValueRO;

                // Store previous pose before moving (for tunneling prevention sweep tests)
                thrown.PrevPosition = transform.Position;
                thrown.PrevRotation = transform.Rotation;
                thrownRef.ValueRW = thrown;

                // Semi-implicit Euler integration: x += v*dt
                // (Velocity was already updated by ThrownObjectGravitySystem: v += g*dt)
                var newTransform = transform;
                newTransform.Position += velocity.Linear * deltaTime;

                // Integrate angular velocity into rotation using axis-angle (more accurate than EulerXYZ)
                // Normalize rotation after integration to prevent numerical drift
                if (math.lengthsq(velocity.Angular) > 0.0001f)
                {
                    var angularVel = velocity.Angular;
                    var angle = math.length(angularVel) * deltaTime;
                    if (angle > 0.0001f)
                    {
                        var axis = math.normalize(angularVel);
                        var rotationDelta = quaternion.AxisAngle(axis, angle);
                        newTransform.Rotation = math.mul(transform.Rotation, rotationDelta);
                        // Normalize to prevent numerical drift over many ticks
                        newTransform.Rotation = math.normalize(newTransform.Rotation);
                    }
                }

                transformRef.ValueRW = newTransform;
            }

            // Process entities without PhysicsMass (fallback for entities spawned differently)
            // WARNING: Assumes kinematic if PhysicsMass is missing. If a dynamic body is accidentally
            // spawned without PhysicsMass, it will be integrated here AND potentially by Unity Physics → double-move.
            // Consider: enforce bootstrap always adds PhysicsMass, or add explicit ThrownKinematic tag.
            // Assume kinematic if PhysicsMass is missing (bootstrap should add it, but handle gracefully)
            foreach (var (transformRef, velocityRef, thrownRef) in SystemAPI
                         .Query<RefRW<LocalTransform>, RefRO<PhysicsVelocity>, RefRW<BeingThrown>>()
                         .WithAll<BeingThrown>()
                         .WithNone<PhysicsMass>())
            {
                var transform = transformRef.ValueRO;
                var velocity = velocityRef.ValueRO;
                var thrown = thrownRef.ValueRO;

                // Store previous pose before moving (for tunneling prevention sweep tests)
                thrown.PrevPosition = transform.Position;
                thrown.PrevRotation = transform.Rotation;
                thrownRef.ValueRW = thrown;

                // Integrate position (assume kinematic if no PhysicsMass)
                var newTransform = transform;
                newTransform.Position += velocity.Linear * deltaTime;

                // Integrate angular velocity into rotation using axis-angle (more accurate than EulerXYZ)
                // Normalize rotation after integration to prevent numerical drift
                if (math.lengthsq(velocity.Angular) > 0.0001f)
                {
                    var angularVel = velocity.Angular;
                    var angle = math.length(angularVel) * deltaTime;
                    if (angle > 0.0001f)
                    {
                        var axis = math.normalize(angularVel);
                        var rotationDelta = quaternion.AxisAngle(axis, angle);
                        newTransform.Rotation = math.mul(transform.Rotation, rotationDelta);
                        // Normalize to prevent numerical drift over many ticks
                        newTransform.Rotation = math.normalize(newTransform.Rotation);
                    }
                }

                transformRef.ValueRW = newTransform;
            }
        }
    }
}

