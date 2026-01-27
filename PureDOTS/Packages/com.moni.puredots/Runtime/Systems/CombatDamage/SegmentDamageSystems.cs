using PureDOTS.Runtime;
using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.CombatDamage;
using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Combat
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct SegmentDamageCatalogBootstrapSystem : ISystem
    {
        private EntityQuery _typeIndexQuery;
        private EntityQuery _profileIndexQuery;

        public void OnCreate(ref SystemState state)
        {
            _typeIndexQuery = state.GetEntityQuery(ComponentType.ReadOnly<SegmentDamageTypeIndex>());
            _profileIndexQuery = state.GetEntityQuery(ComponentType.ReadOnly<DamageProfileIndex>());
        }

        public void OnUpdate(ref SystemState state)
        {
            EnsureTypeCatalog(ref state);
            EnsureProfileCatalog(ref state);
            state.Enabled = false;
        }

        private void EnsureTypeCatalog(ref SystemState state)
        {
            var entityManager = state.EntityManager;
            if (_typeIndexQuery.IsEmptyIgnoreFilter)
            {
                var entity = entityManager.CreateEntity(typeof(SegmentDamageTypeIndex));
                entityManager.SetComponentData(entity, new SegmentDamageTypeIndex
                {
                    Catalog = BuildEmptyTypeCatalog()
                });
                return;
            }

            var indexEntity = _typeIndexQuery.GetSingletonEntity();
            var data = entityManager.GetComponentData<SegmentDamageTypeIndex>(indexEntity);
            if (data.Catalog.IsCreated)
            {
                return;
            }

            data.Catalog = BuildEmptyTypeCatalog();
            entityManager.SetComponentData(indexEntity, data);
        }

        private void EnsureProfileCatalog(ref SystemState state)
        {
            var entityManager = state.EntityManager;
            if (_profileIndexQuery.IsEmptyIgnoreFilter)
            {
                var entity = entityManager.CreateEntity(typeof(DamageProfileIndex));
                entityManager.SetComponentData(entity, new DamageProfileIndex
                {
                    Catalog = BuildEmptyProfileCatalog()
                });
                return;
            }

            var indexEntity = _profileIndexQuery.GetSingletonEntity();
            var data = entityManager.GetComponentData<DamageProfileIndex>(indexEntity);
            if (data.Catalog.IsCreated)
            {
                return;
            }

            data.Catalog = BuildEmptyProfileCatalog();
            entityManager.SetComponentData(indexEntity, data);
        }

        private static BlobAssetReference<SegmentDamageTypeCatalogBlob> BuildEmptyTypeCatalog()
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<SegmentDamageTypeCatalogBlob>();
            builder.Allocate(ref root.Types, 0);
            return builder.CreateBlobAssetReference<SegmentDamageTypeCatalogBlob>(Allocator.Persistent);
        }

        private static BlobAssetReference<DamageProfileCatalogBlob> BuildEmptyProfileCatalog()
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<DamageProfileCatalogBlob>();
            builder.Allocate(ref root.Shields, 0);
            builder.Allocate(ref root.Armors, 0);
            builder.Allocate(ref root.Hulls, 0);
            return builder.CreateBlobAssetReference<DamageProfileCatalogBlob>(Allocator.Persistent);
        }
    }

    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateBefore(typeof(SegmentDamageRoutingSystem))]
    [UpdateBefore(typeof(DamageResolutionSystem))]
    public partial struct SegmentDamageNormalizeSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<HitEvent>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewind) || rewind.Mode != RewindMode.Record)
            {
                return;
            }

            _transformLookup.Update(ref state);
            var metricsEntity = EnsureMetricsEntity(ref state, timeState.Tick);
            if (!state.EntityManager.HasBuffer<SegmentDamageHitBucket>(metricsEntity))
            {
                state.EntityManager.AddBuffer<SegmentDamageHitBucket>(metricsEntity);
            }
            var metrics = SystemAPI.GetComponentRW<SegmentDamageMetrics>(metricsEntity);
            var buckets = SystemAPI.GetBuffer<SegmentDamageHitBucket>(metricsEntity);
            SegmentDamageMetricsUtility.ResetIfNeeded(ref metrics.ValueRW, ref buckets, timeState.Tick);

            foreach (var (hitEvents, entity) in SystemAPI.Query<DynamicBuffer<HitEvent>>().WithEntityAccess())
            {
                if (hitEvents.Length == 0)
                {
                    continue;
                }

                for (var i = 0; i < hitEvents.Length; i++)
                {
                    var hit = hitEvents[i];
                    if (hit.HitTick == 0 || hit.HitEntity == Entity.Null)
                    {
                        continue;
                    }

                    var target = hit.HitEntity;
                    if (!SystemAPI.Exists(target))
                    {
                        continue;
                    }

                    if (!SystemAPI.HasBuffer<SegmentDamageEvent>(target))
                    {
                        state.EntityManager.AddBuffer<SegmentDamageEvent>(target);
                    }

                    if (!SystemAPI.HasBuffer<SegmentDamagePayloadElement>(target))
                    {
                        state.EntityManager.AddBuffer<SegmentDamagePayloadElement>(target);
                    }

                    var payloadBuffer = SystemAPI.GetBuffer<SegmentDamagePayloadElement>(target);
                    var payloadStart = (ushort)math.min(payloadBuffer.Length, ushort.MaxValue);
                    payloadBuffer.Add(new SegmentDamagePayloadElement
                    {
                        DamageTypeIndex = 0,
                        Amount = math.max(0f, hit.DamageAmount),
                        Penetration = 0f,
                        Bypass = 0f
                    });

                    var localPosition = hit.HitPosition;
                    var localNormal = hit.HitNormal;
                    var incoming = math.normalizesafe(-hit.HitNormal, new float3(0f, 0f, -1f));

                    if (_transformLookup.HasComponent(target))
                    {
                        var transform = _transformLookup[target];
                        var inverseRotation = math.inverse(transform.Rotation);
                        var scale = math.max(0.0001f, transform.Scale);
                        localPosition = math.mul(inverseRotation, (hit.HitPosition - transform.Position) / scale);
                        localNormal = math.normalizesafe(math.mul(inverseRotation, hit.HitNormal));
                        incoming = math.normalizesafe(-localNormal, new float3(0f, 0f, -1f));
                    }

                    var eventBuffer = SystemAPI.GetBuffer<SegmentDamageEvent>(target);
                    eventBuffer.Add(new SegmentDamageEvent
                    {
                        EventId = hit.HitTick ^ (uint)i,
                        Tick = hit.HitTick,
                        Source = hit.AttackerEntity,
                        Target = target,
                        SegmentId = SegmentDamageConstants.UnassignedSegmentId,
                        SegmentHint = 0,
                        ImpactPositionLocal = localPosition,
                        ImpactNormalLocal = localNormal,
                        IncomingDirectionLocal = incoming,
                        Impulse = 0f,
                        Heat = 0f,
                        SpreadRadius = 0f,
                        Flags = SegmentDamageEventFlags.None,
                        PayloadStart = payloadStart,
                        PayloadCount = 1
                    });

                    metrics.ValueRW.NormalizedHits++;
                }
            }
        }

        private static Entity EnsureMetricsEntity(ref SystemState state, uint tick)
        {
            var entityManager = state.EntityManager;
            var query = entityManager.CreateEntityQuery(typeof(SegmentDamageMetrics));
            if (!query.IsEmptyIgnoreFilter)
            {
                return query.GetSingletonEntity();
            }

            var metricsEntity = entityManager.CreateEntity();
            entityManager.AddComponentData(metricsEntity, new SegmentDamageMetrics
            {
                Tick = tick,
                NormalizedHits = 0,
                RoutedHits = 0,
                UnroutableHits = 0
            });
            entityManager.AddBuffer<SegmentDamageHitBucket>(metricsEntity);
            return metricsEntity;
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(SegmentDamageNormalizeSystem))]
    public partial struct SegmentDamageRoutingSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SegmentDamageEvent>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewind) || rewind.Mode != RewindMode.Record)
            {
                return;
            }

            var hasMetrics = SystemAPI.TryGetSingletonRW<SegmentDamageMetrics>(out var metrics);
            DynamicBuffer<SegmentDamageHitBucket> buckets = default;
            if (hasMetrics)
            {
                buckets = SystemAPI.GetSingletonBuffer<SegmentDamageHitBucket>();
                SegmentDamageMetricsUtility.ResetIfNeeded(ref metrics.ValueRW, ref buckets, timeState.Tick);
            }

            foreach (var (events, entity) in SystemAPI.Query<DynamicBuffer<SegmentDamageEvent>>().WithEntityAccess())
            {
                var eventBuffer = events;
                var hasSegments = SystemAPI.HasBuffer<DamageSegmentDefinition>(entity);
                var segmentDefs = hasSegments ? SystemAPI.GetBuffer<DamageSegmentDefinition>(entity) : default;

                for (var i = 0; i < eventBuffer.Length; i++)
                {
                    var evt = eventBuffer[i];
                    if (evt.SegmentId != SegmentDamageConstants.UnassignedSegmentId)
                    {
                        continue;
                    }

                    var resolved = SegmentDamageConstants.UnroutableSegmentId;
                    if (hasSegments && segmentDefs.Length > 0)
                    {
                        var bestDistance = float.MaxValue;
                        for (var s = 0; s < segmentDefs.Length; s++)
                        {
                            var definition = segmentDefs[s];
                            var extents = math.abs(definition.LocalExtents);
                            var delta = evt.ImpactPositionLocal - definition.LocalCenter;
                            if (math.abs(delta.x) > extents.x ||
                                math.abs(delta.y) > extents.y ||
                                math.abs(delta.z) > extents.z)
                            {
                                continue;
                            }

                            var distance = math.lengthsq(delta);
                            if (distance < bestDistance)
                            {
                                bestDistance = distance;
                                resolved = definition.SegmentId;
                            }
                        }
                    }

                    evt.SegmentId = resolved;
                    eventBuffer[i] = evt;

                    if (!hasMetrics)
                    {
                        continue;
                    }

                    if (resolved == SegmentDamageConstants.UnroutableSegmentId)
                    {
                        metrics.ValueRW.UnroutableHits++;
                        continue;
                    }

                    metrics.ValueRW.RoutedHits++;
                    SegmentDamageMetricsUtility.IncrementBucket(ref buckets, resolved);
                }
            }
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(SegmentDamageRoutingSystem))]
    public partial struct SegmentShieldResolveSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SegmentDamageEvent>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewind) || rewind.Mode != RewindMode.Record)
            {
                return;
            }

            // Placeholder: shield mitigation, coverage checks, and hole creation.
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(SegmentShieldResolveSystem))]
    public partial struct SegmentArmorResolveSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SegmentDamageEvent>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewind) || rewind.Mode != RewindMode.Record)
            {
                return;
            }

            // Placeholder: armor mitigation, ablation, and seep-through.
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(SegmentArmorResolveSystem))]
    public partial struct SegmentHullResolveSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SegmentDamageEvent>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewind) || rewind.Mode != RewindMode.Record)
            {
                return;
            }

            // Placeholder: hull integrity and breach checks.
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(SegmentHullResolveSystem))]
    public partial struct SegmentModuleSpillSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SegmentDamageEvent>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewind) || rewind.Mode != RewindMode.Record)
            {
                return;
            }

            // Placeholder: spill-through to modules and fault emission.
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(SegmentModuleSpillSystem))]
    public partial struct SegmentDamageRegenSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SegmentDamageEvent>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewind) || rewind.Mode != RewindMode.Record)
            {
                return;
            }

            // Placeholder: shield regen and integrity recovery.
        }
    }

    internal static class SegmentDamageMetricsUtility
    {
        public static void ResetIfNeeded(ref SegmentDamageMetrics metrics, ref DynamicBuffer<SegmentDamageHitBucket> buckets, uint tick)
        {
            if (metrics.Tick == tick)
            {
                return;
            }

            metrics.Tick = tick;
            metrics.NormalizedHits = 0;
            metrics.RoutedHits = 0;
            metrics.UnroutableHits = 0;
            if (buckets.Length > 0)
            {
                buckets.Clear();
            }
        }

        public static void IncrementBucket(ref DynamicBuffer<SegmentDamageHitBucket> buckets, ushort segmentId)
        {
            for (var i = 0; i < buckets.Length; i++)
            {
                var entry = buckets[i];
                if (entry.SegmentId != segmentId)
                {
                    continue;
                }

                entry.Count++;
                buckets[i] = entry;
                return;
            }

            buckets.Add(new SegmentDamageHitBucket
            {
                SegmentId = segmentId,
                Count = 1
            });
        }
    }
}
