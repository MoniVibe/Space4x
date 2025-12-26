using PureDOTS.Environment;
using PureDOTS.Rendering;
using PureDOTS.Runtime.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace Space4X.Presentation
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct Space4XDebrisSpallSystem : ISystem
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private Entity _statsEntity;
#endif

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TerrainModificationQueue>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (RuntimeMode.IsHeadless)
            {
                return;
            }

            if (!SystemAPI.TryGetSingletonEntity<TerrainModificationQueue>(out var queueEntity))
            {
                return;
            }

            var events = SystemAPI.GetBuffer<TerrainModificationEvent>(queueEntity);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var statsEntity = EnsureStatsEntity(ref state);
            var stats = new Space4XDebrisSpallFrameStats();
#endif
            if (events.Length == 0)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (statsEntity != Entity.Null)
                {
                    state.EntityManager.SetComponentData(statsEntity, stats);
                }
#endif
                return;
            }

            var config = Space4XDebrisSpallConfig.Default;
            if (SystemAPI.TryGetSingleton<Space4XDebrisSpallConfig>(out var configSingleton))
            {
                config = configSingleton;
            }

            var remainingBudget = math.max(0, config.MaxPiecesPerFrame);
            if (remainingBudget <= 0)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                stats.DebrisSuppressedByBudget = events.Length;
                if (statsEntity != Entity.Null)
                {
                    state.EntityManager.SetComponentData(statsEntity, stats);
                }
#endif
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var spawnEvents = 0;
            var spawnedCount = 0;
            var suppressed = 0;

            foreach (var evt in events)
            {
                if (remainingBudget <= 0)
                {
                    break;
                }

                if (evt.ClearedVoxels <= 0)
                {
                    continue;
                }

                var desired = (int)math.ceil(evt.ClearedVoxels * math.max(0f, config.PiecesPerVoxel));
                desired = math.clamp(desired, 1, math.max(1, config.MaxPiecesPerEvent));
                var desiredClamped = math.min(desired, remainingBudget);
                suppressed += math.max(0, desired - desiredClamped);
                desired = desiredClamped;
                if (desired <= 0)
                {
                    continue;
                }

                spawnEvents++;

                var baseSeed = (uint)(evt.Tick * 397u + (uint)evt.VolumeEntity.Index * 17u + 11u);
                var impulseMultiplier = ResolveImpulseMultiplier(evt.ToolKind, config);
                var tint = ResolveDebrisTint(evt.ToolKind);
                for (int i = 0; i < desired; i++)
                {
                    var rng = Random.CreateFromIndex(baseSeed + (uint)(i * 73 + 1));
                    var direction = ResolveDebrisDirection(evt, rng);
                    var speed = math.lerp(config.ImpulseMin, config.ImpulseMax, rng.NextFloat()) * impulseMultiplier;
                    var velocity = direction * speed;
                    var jitter = rng.NextFloat3Direction() * math.max(0.05f, evt.Radius * 0.35f);
                    var position = evt.WorldPosition + jitter;
                    var lifetime = math.lerp(config.LifetimeMin, config.LifetimeMax, rng.NextFloat());
                    var scale = math.lerp(config.ScaleMin, config.ScaleMax, rng.NextFloat());

                    var entity = ecb.CreateEntity();
                    ecb.AddComponent(entity, new Space4XDebrisTag());
                    ecb.AddComponent(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, scale));
                    ecb.AddComponent(entity, new Space4XDebrisMotion
                    {
                        Velocity = velocity,
                        Lifetime = lifetime,
                        Drag = config.Drag
                    });
                    ecb.AddComponent(entity, new RenderTint { Value = tint });
                }

                spawnedCount += desired;
                remainingBudget -= desired;
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            stats.DebrisSpawnedThisFrame = spawnedCount;
            stats.DebrisSpawnEventsThisFrame = spawnEvents;
            stats.DebrisSuppressedByBudget = suppressed;
            if (statsEntity != Entity.Null)
            {
                state.EntityManager.SetComponentData(statsEntity, stats);
            }
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private Entity EnsureStatsEntity(ref SystemState state)
        {
            if (_statsEntity != Entity.Null && state.EntityManager.Exists(_statsEntity))
            {
                return _statsEntity;
            }

            _statsEntity = state.EntityManager.CreateEntity(typeof(Space4XDebrisSpallFrameStats));
            return _statsEntity;
        }
#endif

        private static float ResolveImpulseMultiplier(TerrainModificationToolKind toolKind, in Space4XDebrisSpallConfig config)
        {
            return toolKind switch
            {
                TerrainModificationToolKind.Laser => config.LaserImpulseMultiplier,
                TerrainModificationToolKind.Microwave => config.MicrowaveImpulseMultiplier,
                _ => config.DrillImpulseMultiplier
            };
        }

        private static float3 ResolveDebrisDirection(in TerrainModificationEvent evt, Random rng)
        {
            var direction = evt.WorldDirection;
            if (math.lengthsq(direction) < 1e-4f)
            {
                return rng.NextFloat3Direction();
            }

            var spread = rng.NextFloat3Direction() * 0.35f;
            direction = math.normalizesafe(direction + spread, rng.NextFloat3Direction());
            return direction;
        }

        private static float4 ResolveDebrisTint(TerrainModificationToolKind toolKind)
        {
            return toolKind switch
            {
                TerrainModificationToolKind.Laser => new float4(0.62f, 0.46f, 0.36f, 1f),
                TerrainModificationToolKind.Microwave => new float4(0.45f, 0.5f, 0.6f, 1f),
                _ => new float4(0.48f, 0.45f, 0.42f, 1f)
            };
        }
    }

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XDebrisSpallSystem))]
    public partial struct Space4XDebrisMotionSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            if (RuntimeMode.IsHeadless)
            {
                return;
            }

            var deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
            {
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (motion, transform, entity) in SystemAPI.Query<RefRW<Space4XDebrisMotion>, RefRW<LocalTransform>>().WithEntityAccess())
            {
                var data = motion.ValueRO;
                data.Lifetime -= deltaTime;
                if (data.Lifetime <= 0f)
                {
                    ecb.DestroyEntity(entity);
                    continue;
                }

                var dragFactor = math.saturate(1f - data.Drag * deltaTime);
                data.Velocity *= dragFactor;
                var updated = transform.ValueRO;
                updated.Position += data.Velocity * deltaTime;

                motion.ValueRW = data;
                transform.ValueRW = updated;
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
