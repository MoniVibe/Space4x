using PureDOTS.Runtime.Config;
using PureDOTS.Runtime.Visuals;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Visuals
{
    [BurstCompile]
    [UpdateInGroup(typeof(Unity.Entities.PresentationSystemGroup))]
    public partial struct MiningLoopVisualPresentationSystem : ISystem
    {
        private EntityQuery _prefabQuery;
        private EntityQuery _visualQuery;
        private bool _hasVisualToggle;
        private bool _visualToggleEnabled;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MiningVisualManifest>();
            _prefabQuery = state.GetEntityQuery(ComponentType.ReadOnly<MiningVisualPrefab>());
            _visualQuery = state.GetEntityQuery(ComponentType.ReadOnly<MiningVisual>(), ComponentType.ReadWrite<LocalTransform>());
            RuntimeConfigRegistry.Initialize();
            if (RuntimeConfigRegistry.TryGetVar("visuals.mining.enabled", out var configVar))
            {
                _hasVisualToggle = true;
                _visualToggleEnabled = configVar.BoolValue;
            }
            else
            {
                _hasVisualToggle = false;
                _visualToggleEnabled = true;
            }
        }

        public void OnDestroy(ref SystemState state) { }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (_hasVisualToggle && !_visualToggleEnabled)
            {
                CleanupAllVisuals(ref state);
                return;
            }

            if (_prefabQuery.IsEmptyIgnoreFilter)
            {
                CleanupAllVisuals(ref state);
                return;
            }

            var manifestEntity = SystemAPI.GetSingletonEntity<MiningVisualManifest>();
            var requestBuffer = state.EntityManager.GetBuffer<MiningVisualRequest>(manifestEntity);
            var requests = requestBuffer.ToNativeArray(Allocator.Temp);

            var prefabMap = GatherPrefabs(ref state);
            var entityManager = state.EntityManager;

            var existingMap = new NativeParallelHashMap<Entity, Entity>(math.max(1, _visualQuery.CalculateEntityCount()), Allocator.Temp);
            var duplicateVisuals = new NativeList<Entity>(Allocator.Temp);

            foreach (var (visual, entity) in SystemAPI.Query<MiningVisual>().WithEntityAccess())
            {
                if (visual.SourceEntity == Entity.Null || !existingMap.TryAdd(visual.SourceEntity, entity))
                {
                    duplicateVisuals.Add(entity);
                }
            }

            for (var i = 0; i < duplicateVisuals.Length; i++)
            {
                var duplicate = duplicateVisuals[i];
                if (duplicate != Entity.Null && entityManager.Exists(duplicate))
                {
                    entityManager.DestroyEntity(duplicate);
                }
            }

            var usedSources = new NativeParallelHashSet<Entity>(math.max(1, requests.Length), Allocator.Temp);

            for (var i = 0; i < requests.Length; i++)
            {
                var request = requests[i];
                if (request.SourceEntity == Entity.Null)
                {
                    continue;
                }

                if (!prefabMap.TryGetValue((byte)request.VisualType, out var prefabInfo) || prefabInfo.PrefabEntity == Entity.Null)
                {
                    continue;
                }

                var targetScale = prefabInfo.BaseScale * math.max(0.1f, request.BaseScale);

                if (existingMap.TryGetValue(request.SourceEntity, out var visualEntity) && entityManager.Exists(visualEntity))
                {
                    usedSources.Add(request.SourceEntity);
                    SetTransform(ref entityManager, visualEntity, request.Position, targetScale);
                }
                else
                {
                    usedSources.Add(request.SourceEntity);
                    var instance = entityManager.Instantiate(prefabInfo.PrefabEntity);
                    SetTransform(ref entityManager, instance, request.Position, targetScale);

                    if (entityManager.HasComponent<MiningVisual>(instance))
                    {
                        entityManager.SetComponentData(instance, new MiningVisual
                        {
                            VisualType = request.VisualType,
                            SourceEntity = request.SourceEntity
                        });
                    }
                    else
                    {
                        entityManager.AddComponentData(instance, new MiningVisual
                        {
                            VisualType = request.VisualType,
                            SourceEntity = request.SourceEntity
                        });
                    }

                    existingMap[request.SourceEntity] = instance;
                }
            }

            foreach (var kvp in existingMap)
            {
                if (kvp.Key == Entity.Null)
                {
                    continue;
                }

                if (!usedSources.Contains(kvp.Key) && entityManager.Exists(kvp.Value))
                {
                    entityManager.DestroyEntity(kvp.Value);
                }
            }

            usedSources.Dispose();
            existingMap.Dispose();
            prefabMap.Dispose();
            duplicateVisuals.Dispose();
            requests.Dispose();
        }

        private void CleanupAllVisuals(ref SystemState state)
        {
            using var visuals = _visualQuery.ToEntityArray(Allocator.Temp);
            if (visuals.Length == 0)
            {
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            for (var i = 0; i < visuals.Length; i++)
            {
                ecb.DestroyEntity(visuals[i]);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private NativeParallelHashMap<byte, PrefabInfo> GatherPrefabs(ref SystemState state)
        {
            var capacity = math.max(1, _prefabQuery.CalculateEntityCount());
            var map = new NativeParallelHashMap<byte, PrefabInfo>(capacity, Allocator.Temp);

            foreach (var (prefab, entity) in SystemAPI.Query<MiningVisualPrefab>().WithEntityAccess())
            {
                map[(byte)prefab.VisualType] = new PrefabInfo
                {
                    PrefabEntity = prefab.Prefab != Entity.Null ? prefab.Prefab : entity,
                    BaseScale = prefab.BaseScale > 0f ? prefab.BaseScale : 1f
                };
            }

            return map;
        }

        private static void SetTransform(ref EntityManager entityManager, Entity target, float3 position, float scale)
        {
            var transform = LocalTransform.FromPositionRotationScale(position, quaternion.identity, scale);
            if (entityManager.HasComponent<LocalTransform>(target))
            {
                entityManager.SetComponentData(target, transform);
            }
            else
            {
                entityManager.AddComponentData(target, transform);
            }
        }

        private struct PrefabInfo
        {
            public Entity PrefabEntity;
            public float BaseScale;
        }
    }
}
