using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Interaction;
using PureDOTS.Runtime.Miracles;
using PureDOTS.Runtime.Physics;
using PureDOTS.Runtime.Time;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace PureDOTS.Systems.Physics
{
    /// <summary>
    /// Prevents tunneling for fast-moving thrown objects by performing deterministic sweep tests.
    /// Runs after BuildPhysicsWorld to detect collisions that discrete position updates might miss.
    /// </summary>
    /// <remarks>
    /// Philosophy:
    /// - Fast throws can tunnel through thin colliders (discrete x += v*dt updates)
    /// - This system performs sweep tests using the entity's actual PhysicsCollider from previous position to current position
    /// - If a hit is detected, clamps position to hit point and stops the throw (clears BeingThrown)
    /// - This keeps kinematic flight while preventing tunneling without making bodies dynamic
    /// 
    /// Integration:
    /// - ThrownObjectTransformIntegratorSystem stores previous pose (position + rotation) before moving
    /// - This system runs after BuildPhysicsWorld (when the collision world is available)
    /// - Performs sweep tests using each entity's PhysicsCollider and handles impact if detected
    /// </remarks>
    [BurstCompile]
    [UpdateInGroup(typeof(Unity.Physics.Systems.PhysicsSystemGroup))]
    [UpdateAfter(typeof(Unity.Physics.Systems.BuildPhysicsWorld))]
    public partial struct ThrownObjectTunnelingPreventionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<BeingThrown>();
            state.RequireForUpdate<PhysicsWorldSingleton>();
        }

        [BurstCompile]
        public unsafe void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();

            // Only process in Record mode (not during playback/rewind)
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            if (timeState.IsPaused)
            {
                return;
            }

            var physicsWorldSingleton = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            var physicsWorld = physicsWorldSingleton.PhysicsWorld;
            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            // Process kinematic thrown objects (only these need sweep tests - dynamic bodies use Unity's CCD)
            // Handle miracle tokens (with MiracleToken and MiracleOnImpact) and generic thrown objects
            // Must have PhysicsCollider to perform sweep test
            foreach (var (transformRef, velocityRef, thrownRef, massRef, colliderRef, entity) in SystemAPI
                         .Query<RefRW<LocalTransform>, RefRO<PhysicsVelocity>, RefRW<BeingThrown>, RefRO<PhysicsMass>, RefRO<PhysicsCollider>>()
                         .WithEntityAccess())
            {
                var mass = massRef.ValueRO;
                var transform = transformRef.ValueRO;
                var velocity = velocityRef.ValueRO;
                var thrown = thrownRef.ValueRO;
                var collider = colliderRef.ValueRO;

                // Only process kinematic bodies (dynamic bodies use Unity's CCD)
                if (mass.InverseMass > 0f)
                {
                    continue;
                }

                // Skip if already impacted (for miracle tokens)
                bool hasImpact = SystemAPI.HasComponent<MiracleOnImpact>(entity);
                bool hasMiracleToken = SystemAPI.HasComponent<PureDOTS.Runtime.Miracles.MiracleToken>(entity);
                if (hasImpact)
                {
                    var impact = SystemAPI.GetComponent<MiracleOnImpact>(entity);
                    if (impact.HasImpacted != 0)
                    {
                        continue;
                    }
                }

                // Skip if velocity is too low (no tunneling risk)
                float speed = math.length(velocity.Linear);
                if (speed < 0.1f)
                {
                    continue;
                }

                // Use stored previous pose (set by ThrownObjectTransformIntegratorSystem)
                float3 currentPos = transform.Position;
                float3 prevPos = thrown.PrevPosition;

                // Skip sweep on the very first frame (before integrator stores prev pose)
                if (thrown.TimeSinceThrow <= 0f)
                {
                    thrown.PrevPosition = currentPos;
                    thrown.PrevRotation = transform.Rotation;
                    thrownRef.ValueRW = thrown;
                    continue;
                }

                // Use entity's actual PhysicsCollider for sweep test (preserves shape and filter)
                var sweepInput = new ColliderCastInput
                {
                    Collider = (Unity.Physics.Collider*)collider.Value.GetUnsafePtr(),
                    Start = prevPos,
                    End = currentPos,
                    Orientation = thrown.PrevRotation
                };

                if (physicsWorld.CastCollider(sweepInput, out var hit))
                {
                    // Ignore self hits (can happen because the collider already exists at currentPos)
                    if (hit.Entity == entity)
                    {
                        continue;
                    }

                    // Hit detected - clamp position to correct sphere center at impact
                    float3 travelDir = math.normalizesafe(currentPos - prevPos, float3.zero);
                    float3 impactCenter = math.lerp(prevPos, currentPos, hit.Fraction);
                    impactCenter -= travelDir * 0.01f; // pull back slightly along travel direction

                    var newTransform = transform;
                    newTransform.Position = impactCenter;
                    transformRef.ValueRW = newTransform;

                    // Mark as impacted (for miracle tokens)
                    if (hasImpact && hasMiracleToken)
                    {
                        var impactRef = SystemAPI.GetComponentRW<MiracleOnImpact>(entity);
                        var impact = impactRef.ValueRO;
                        impact.HasImpacted = 1;
                        impactRef.ValueRW = impact;
                    }

                    // Clear BeingThrown to stop further integration
                    ecb.SetComponentEnabled<BeingThrown>(entity, false);

                    // Zero velocity to prevent further movement
                    if (SystemAPI.HasComponent<PhysicsVelocity>(entity))
                    {
                        var velRef = SystemAPI.GetComponentRW<PhysicsVelocity>(entity);
                        var vel = velRef.ValueRO;
                        vel.Linear = float3.zero;
                        vel.Angular = float3.zero;
                        velRef.ValueRW = vel;
                    }

                    // Spawn impact effect (for miracle tokens only)
                    if (hasMiracleToken && hasImpact)
                    {
                        var token = SystemAPI.GetComponent<PureDOTS.Runtime.Miracles.MiracleToken>(entity);
                        var impact = SystemAPI.GetComponent<MiracleOnImpact>(entity);
                        var impactEntity = ecb.CreateEntity();
                        ecb.AddComponent(impactEntity, LocalTransform.FromPosition(impactCenter));
                        ecb.AddComponent(impactEntity, new MiracleEffectNew
                        {
                            Id = token.Id,
                            RemainingSeconds = 0f, // Instant explosion
                            Intensity = token.Intensity,
                            Origin = impactCenter,
                            Radius = impact.ExplosionRadius
                        });

                        // Destroy token after spawning impact effect
                        ecb.DestroyEntity(entity);
                    }
                    // Generic thrown objects: BeingThrown already removed above, just stop movement
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}

