using PureDOTS.Environment;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Miracles;
using PureDOTS.Runtime.Physics;
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
    /// Detects collisions for miracle tokens and triggers impact effects.
    /// Sets HasImpacted flag and spawns MiracleEffectNew at impact point.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(MiracleEffectSystemGroup))]
    [UpdateAfter(typeof(MiracleTokenFlightSystem))]
    public partial struct MiracleTokenImpactSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<MiracleOnImpact>();
            state.RequireForUpdate<TerrainModificationQueue>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (timeState.IsPaused || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            var hasQueue = SystemAPI.TryGetSingletonEntity<TerrainModificationQueue>(out var queueEntity);
            DynamicBuffer<TerrainModificationRequest> modificationBuffer = default;
            if (hasQueue)
            {
                modificationBuffer = SystemAPI.GetBuffer<TerrainModificationRequest>(queueEntity);
            }

            foreach (var (impactRef, tokenRef, transformRef, entity) in SystemAPI
                         .Query<RefRW<MiracleOnImpact>, RefRO<ProjectileToken>, RefRO<LocalTransform>>()
                         .WithEntityAccess())
            {
                var impact = impactRef.ValueRO;
                var token = tokenRef.ValueRO;

                // Skip if already impacted
                if (impact.HasImpacted != 0)
                {
                    continue;
                }

                // Check for collision events
                if (!SystemAPI.HasBuffer<PhysicsCollisionEventElement>(entity))
                {
                    continue;
                }

                var collisionEvents = SystemAPI.GetBuffer<PhysicsCollisionEventElement>(entity);
                if (collisionEvents.Length == 0)
                {
                    continue;
                }

                // Process first collision event
                var collisionEvent = collisionEvents[0];
                
                // Set HasImpacted flag
                impact.HasImpacted = 1;
                impactRef.ValueRW = impact;

                // Get impact position (use contact point if available, otherwise token position)
                float3 impactPosition = collisionEvent.ContactPoint;
                if (math.lengthsq(impactPosition) < 0.0001f)
                {
                    impactPosition = transformRef.ValueRO.Position;
                }

                if (hasQueue)
                {
                    var radius = math.max(0f, impact.ExplosionRadius);
                    if (radius > 0f)
                    {
                        modificationBuffer.Add(new TerrainModificationRequest
                        {
                            Kind = TerrainModificationKind.Dig,
                            Shape = TerrainModificationShape.Brush,
                            ToolKind = TerrainModificationToolKind.Laser,
                            Start = impactPosition,
                            End = impactPosition,
                            Radius = radius,
                            Depth = radius,
                            MaterialId = 0,
                            DamageDelta = 0,
                            DamageThreshold = 0,
                            YieldMultiplier = 1f,
                            HeatDelta = 0f,
                            InstabilityDelta = 0f,
                            Flags = TerrainModificationFlags.AffectsSurface | TerrainModificationFlags.AffectsVolume,
                            RequestedTick = timeState.Tick,
                            Actor = entity,
                            VolumeEntity = Entity.Null,
                            Space = TerrainModificationSpace.World
                        });
                    }
                }

                // Spawn impact effect at collision point
                var impactEntity = ecb.CreateEntity();
                ecb.AddComponent(impactEntity, LocalTransform.FromPosition(impactPosition));
                ecb.AddComponent(impactEntity, new MiracleEffectNew
                {
                    Id = token.Id,
                    RemainingSeconds = 0f, // Instant explosion on impact
                    Intensity = token.Intensity,
                    Origin = impactPosition,
                    Radius = impact.ExplosionRadius
                });

                // Destroy token after spawning impact effect
                ecb.DestroyEntity(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
