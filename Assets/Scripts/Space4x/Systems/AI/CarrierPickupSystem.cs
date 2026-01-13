using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Spatial;
using PureDOTS.Systems;
using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Systems.AI
{
    /// <summary>
    /// Finds the nearest spawned resource for each carrier and loads it into the carrier hold.
    /// Also updates mining telemetry totals for presentation bindings.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PureDOTS.Systems.ResourceSystemGroup))]
    [UpdateAfter(typeof(MiningResourceSpawnSystem))]
    public partial struct CarrierPickupSystem : ISystem
    {
        private EntityQuery _spawnQuery;
        private EntityQuery _carrierQuery;
        private EntityQuery _miningTelemetryQuery;
        private ComponentLookup<CrewSkills> _skillsLookup;
        private ComponentLookup<SpawnResource> _spawnLookup;
        private ComponentLookup<LocalTransform> _transformLookup;

        private const float PickupRadius = 8f;
        private const float PickupRadiusSq = PickupRadius * PickupRadius;

        private struct SpawnResourceFilter : ISpatialQueryFilter
        {
            [ReadOnly] public ComponentLookup<SpawnResource> SpawnLookup;

            public bool Accept(int descriptorIndex, in SpatialQueryDescriptor descriptor, in SpatialGridEntry entry)
            {
                var entity = entry.Entity;
                if (entity == Entity.Null)
                {
                    return false;
                }

                if (!SpawnLookup.HasComponent(entity))
                {
                    return false;
                }

                return SpawnLookup[entity].Amount > 0f;
            }
        }

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _spawnQuery = SystemAPI.QueryBuilder()
                .WithAll<SpawnResource, LocalTransform>()
                .Build();

            _carrierQuery = SystemAPI.QueryBuilder()
                .WithAll<Carrier, ResourceStorage, LocalTransform>()
                .Build();

            _miningTelemetryQuery = SystemAPI.QueryBuilder()
                .WithAll<Space4XMiningTelemetry>()
                .Build();

            _skillsLookup = state.GetComponentLookup<CrewSkills>(true);
            _spawnLookup = state.GetComponentLookup<SpawnResource>(false);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
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

            var hasCommandLog = SystemAPI.TryGetSingletonBuffer<MiningCommandLogEntry>(out var commandLog);

            var telemetryEntity = EnsureMiningTelemetryEntity(ref state);

            if (_carrierQuery.IsEmptyIgnoreFilter)
            {
                state.EntityManager.SetComponentData(telemetryEntity, new Space4XMiningTelemetry
                {
                    OreInHold = 0f,
                    LastUpdateTick = timeState.Tick
                });
                return;
            }

            float totalHeld = 0f;
            _skillsLookup.Update(ref state);
            _spawnLookup.Update(ref state);
            _transformLookup.Update(ref state);

            var spatialConfig = default(SpatialGridConfig);
            var hasSpatial = SystemAPI.TryGetSingleton(out spatialConfig) &&
                             SystemAPI.TryGetSingleton(out SpatialGridState _);

            DynamicBuffer<SpatialGridCellRange> cellRanges = default;
            DynamicBuffer<SpatialGridEntry> gridEntries = default;
            NativeList<KNearestResult> nearestResults = default;
            if (hasSpatial)
            {
                var gridEntity = SystemAPI.GetSingletonEntity<SpatialGridConfig>();
                cellRanges = SystemAPI.GetBuffer<SpatialGridCellRange>(gridEntity);
                gridEntries = SystemAPI.GetBuffer<SpatialGridEntry>(gridEntity);
                nearestResults = new NativeList<KNearestResult>(1, Allocator.Temp);
            }

            NativeArray<Entity> spawnEntities = default;
            NativeArray<SpawnResource> spawnData = default;
            NativeArray<LocalTransform> spawnTransforms = default;
            if (!hasSpatial)
            {
                spawnEntities = _spawnQuery.ToEntityArray(Allocator.Temp);
                spawnData = _spawnQuery.ToComponentDataArray<SpawnResource>(Allocator.Temp);
                spawnTransforms = _spawnQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            }

            foreach (var (carrier, storage, transform, entity) in SystemAPI
                         .Query<RefRO<Carrier>, DynamicBuffer<ResourceStorage>, RefRO<LocalTransform>>()
                         .WithEntityAccess())
            {
                var carrierPosition = transform.ValueRO.Position;
                var radiusScale = 1f + 0.25f * GetHaulingSkill(entity);
                var radius = PickupRadius * radiusScale;
                var radiusSq = radius * radius;

                Entity bestSpawn = Entity.Null;
                int bestIndex = -1;

                if (hasSpatial)
                {
                    var filter = new SpawnResourceFilter
                    {
                        SpawnLookup = _spawnLookup
                    };
                    var queryPos = carrierPosition;
                    SpatialQueryHelper.FindKNearestInRadius(ref queryPos, radius, 1, spatialConfig, cellRanges, gridEntries,
                        ref nearestResults, filter);
                    if (nearestResults.Length > 0)
                    {
                        bestSpawn = nearestResults[0].Entity;
                    }
                }
                else
                {
                    var bestDistSq = float.PositiveInfinity;
                    for (var i = 0; i < spawnEntities.Length; i++)
                    {
                        var spawn = spawnData[i];
                        if (spawn.Amount <= 0f)
                        {
                            continue;
                        }

                        var distSq = math.lengthsq(spawnTransforms[i].Position - carrierPosition);
                        if (distSq > radiusSq || distSq >= bestDistSq)
                        {
                            continue;
                        }

                        bestIndex = i;
                        bestDistSq = distSq;
                    }

                    if (bestIndex >= 0)
                    {
                        bestSpawn = spawnEntities[bestIndex];
                    }
                }

                if (bestSpawn != Entity.Null)
                {
                    if (hasSpatial)
                    {
                        if (_spawnLookup.HasComponent(bestSpawn))
                        {
                            var spawn = _spawnLookup[bestSpawn];
                            var picked = TransferToStorage(storage, spawn.Type, spawn.Amount);
                            if (picked > 0f)
                            {
                                spawn.Amount -= picked;
                                _spawnLookup[bestSpawn] = spawn;

                                if (hasCommandLog)
                                {
                                    var position = _transformLookup.HasComponent(bestSpawn)
                                        ? _transformLookup[bestSpawn].Position
                                        : carrierPosition;
                                    commandLog.Add(new MiningCommandLogEntry
                                    {
                                        Tick = timeState.Tick,
                                        CommandType = MiningCommandType.Pickup,
                                        SourceEntity = bestSpawn,
                                        TargetEntity = entity,
                                        ResourceType = spawn.Type,
                                        Amount = picked,
                                        Position = position
                                    });
                                }
                            }
                        }
                    }
                    else if (bestIndex >= 0)
                    {
                        var picked = TransferToStorage(storage, spawnData[bestIndex].Type, spawnData[bestIndex].Amount);
                        if (picked > 0f)
                        {
                            var spawn = spawnData[bestIndex];
                            spawn.Amount -= picked;
                            spawnData[bestIndex] = spawn;

                            if (hasCommandLog)
                            {
                                commandLog.Add(new MiningCommandLogEntry
                                {
                                    Tick = timeState.Tick,
                                    CommandType = MiningCommandType.Pickup,
                                    SourceEntity = spawnEntities[bestIndex],
                                    TargetEntity = entity,
                                    ResourceType = spawn.Type,
                                    Amount = picked,
                                    Position = spawnTransforms[bestIndex].Position
                                });
                            }
                        }
                    }
                }

                totalHeld += SumStorage(storage);
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            if (hasSpatial)
            {
                foreach (var (spawn, entity) in SystemAPI.Query<RefRO<SpawnResource>>().WithEntityAccess())
                {
                    if (spawn.ValueRO.Amount <= 0.01f)
                    {
                        ecb.DestroyEntity(entity);
                    }
                }
            }
            else
            {
                for (var i = 0; i < spawnEntities.Length; i++)
                {
                    var spawn = spawnData[i];
                    if (spawn.Amount <= 0.01f)
                    {
                        ecb.DestroyEntity(spawnEntities[i]);
                    }
                    else
                    {
                        ecb.SetComponent(spawnEntities[i], spawn);
                    }
                }
            }

            state.EntityManager.SetComponentData(telemetryEntity, new Space4XMiningTelemetry
            {
                OreInHold = totalHeld,
                LastUpdateTick = timeState.Tick
            });

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            if (nearestResults.IsCreated)
            {
                nearestResults.Dispose();
            }
            if (spawnEntities.IsCreated)
            {
                spawnEntities.Dispose();
            }
            if (spawnData.IsCreated)
            {
                spawnData.Dispose();
            }
            if (spawnTransforms.IsCreated)
            {
                spawnTransforms.Dispose();
            }
        }

        private static float TransferToStorage(DynamicBuffer<ResourceStorage> storage, ResourceType type, float amount)
        {
            var remaining = amount;
            for (var i = 0; i < storage.Length && remaining > 1e-4f; i++)
            {
                var slot = storage[i];
                if (slot.Type != type)
                {
                    continue;
                }

                remaining = slot.AddAmount(remaining);
                storage[i] = slot;
            }

            if (remaining > 1e-4f && storage.Length < 4)
            {
                var slot = ResourceStorage.Create(type);
                remaining = slot.AddAmount(remaining);
                storage.Add(slot);
            }

            return amount - remaining;
        }

        private static float SumStorage(DynamicBuffer<ResourceStorage> storage)
        {
            var total = 0f;
            for (var i = 0; i < storage.Length; i++)
            {
                total += storage[i].Amount;
            }

            return total;
        }

        private float GetHaulingSkill(Entity entity)
        {
            if (!_skillsLookup.HasComponent(entity))
            {
                return 0f;
            }

            return math.saturate(_skillsLookup[entity].HaulingSkill);
        }

        private Entity EnsureMiningTelemetryEntity(ref SystemState state)
        {
            var em = state.EntityManager;

            if (!_miningTelemetryQuery.IsEmptyIgnoreFilter)
            {
                return _miningTelemetryQuery.GetSingletonEntity();
            }

            var entity = em.CreateEntity();
            em.AddComponent<Space4XMiningTelemetry>(entity);
            em.SetComponentData(entity, new Space4XMiningTelemetry
            {
                OreInHold = 0f,
                LastUpdateTick = 0
            });
            return entity;
        }
    }
}
