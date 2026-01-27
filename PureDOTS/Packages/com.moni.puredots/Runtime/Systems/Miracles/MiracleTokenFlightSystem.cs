using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Interaction;
using PureDOTS.Runtime.Miracles;
using PureDOTS.Runtime.Time;
using ProjectileToken = PureDOTS.Runtime.Miracles.MiracleToken;
using LegacyToken = PureDOTS.Runtime.Components.MiracleTokenLegacy;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Miracles
{
    /// <summary>
    /// Tracks flight time for miracle tokens and handles timeout explosions.
    /// Destroys tokens that exceed MaxFlightTime or whose owners are destroyed.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(MiracleEffectSystemGroup))]
    [UpdateAfter(typeof(MiracleTokenVelocitySystem))]
    public partial struct MiracleTokenFlightSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<MiracleOnImpact>();
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            float deltaTime = SystemAPI.Time.DeltaTime;
            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            _transformLookup.Update(ref state);

            foreach (var (impactRef, tokenRef, thrownRef, entity) in SystemAPI
                         .Query<RefRW<MiracleOnImpact>, RefRO<ProjectileToken>, RefRW<BeingThrown>>()
                         .WithEntityAccess())
            {
                var impact = impactRef.ValueRO;
                var token = tokenRef.ValueRO;

                // Skip if already impacted
                if (impact.HasImpacted != 0)
                {
                    continue;
                }

                // Validate owner exists - if not, cleanup orphaned token
                if (!SystemAPI.Exists(token.Owner))
                {
                    // Owner destroyed - destroy orphaned token
                    ecb.DestroyEntity(entity);
                    continue;
                }

                // Update flight time
                impact.FlightTime += deltaTime;
                impactRef.ValueRW = impact;

                // Update BeingThrown.TimeSinceThrow for consistency
                var thrown = thrownRef.ValueRO;
                thrown.TimeSinceThrow = impact.FlightTime;
                thrownRef.ValueRW = thrown;

                // Check for timeout
                if (impact.FlightTime >= impact.MaxFlightTime)
                {
                    // Timeout reached - trigger explosion in air
                    // Set HasImpacted to prevent further processing
                    impact.HasImpacted = 1;
                    impactRef.ValueRW = impact;

                    // Spawn impact effect at current position
                    if (_transformLookup.HasComponent(entity))
                    {
                        var transform = _transformLookup[entity];
                        // Create impact effect entity (similar to instant mode)
                        var impactEntity = ecb.CreateEntity();
                        ecb.AddComponent(impactEntity, LocalTransform.FromPosition(transform.Position));
                        ecb.AddComponent(impactEntity, new MiracleEffectNew
                        {
                            Id = token.Id,
                            RemainingSeconds = 0f, // Instant explosion
                            Intensity = token.Intensity,
                            Origin = transform.Position,
                            Radius = impact.ExplosionRadius
                        });

                        // Destroy token after spawning impact effect
                        ecb.DestroyEntity(entity);
                    }
                    else
                    {
                        // No transform - just destroy token
                        ecb.DestroyEntity(entity);
                    }
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}

