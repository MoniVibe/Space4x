using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Entities.Graphics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using Space4X.Rendering;

namespace Space4X.Rendering.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(Unity.Rendering.EntitiesGraphicsSystem))]
    [UpdateBefore(typeof(StripInvalidMaterialMeshInfoSystem))]
    [UpdateBefore(typeof(DebugVerifyVisualsSystem))]
    public partial class ApplyRenderCatalogSystem : SystemBase
    {
        private static bool s_loggedFirstPass;

        private static readonly int[] s_requiredKeys =
        {
            Space4XRenderKeys.Carrier,
            Space4XRenderKeys.Miner,
            Space4XRenderKeys.Asteroid,
            Space4XRenderKeys.Projectile,
            Space4XRenderKeys.FleetImpostor
        };

        protected override void OnCreate()
        {
            RequireForUpdate<Space4XRenderCatalogSingleton>();
            RequireForUpdate<RenderKey>();
        }

        protected override void OnUpdate()
        {
            var em = EntityManager;
            var catalogEntity = SystemAPI.GetSingletonEntity<Space4XRenderCatalogSingleton>();
            var catalog = SystemAPI.GetComponent<Space4XRenderCatalogSingleton>(catalogEntity);
            var catalogBlob = catalog.Catalog;
            if (!catalogBlob.IsCreated)
                return;

            ref var entries = ref catalogBlob.Value.Entries;
            if (entries.Length == 0)
                return;

            var rma = em.GetSharedComponentManaged<RenderMeshArray>(catalogEntity);
            var materialRefs = rma.MaterialReferences;
            var meshRefs = rma.MeshReferences;
            var materialCount = materialRefs?.Length ?? 0;
            var meshCount = meshRefs?.Length ?? 0;

            var map = new NativeParallelHashMap<int, Space4XRenderMeshCatalogEntry>(entries.Length + s_requiredKeys.Length, Allocator.Temp);
            for (int i = 0; i < entries.Length; i++)
            {
                map.TryAdd(entries[i].ArchetypeId, entries[i]);
            }

            var fallback = entries[0];
            foreach (var key in s_requiredKeys)
            {
                if (map.TryGetValue(key, out _))
                    continue;

                var placeholder = fallback;
                placeholder.ArchetypeId = key;
                map.TryAdd(key, placeholder);
#if UNITY_EDITOR
                Debug.LogWarning(
                    $"[Space4X RenderCatalog] Missing catalog row for ArchetypeId={key}; using fallback mesh/material. " +
                    "Update Space4XRenderCatalogDefinition to provide art.");
#endif
            }

            var rmaQuery = GetEntityQuery(ComponentType.ReadOnly<RenderKey>());
            em.AddSharedComponentManaged(rmaQuery, rma);

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var defaultFilterSettings = RenderFilterSettings.Default;

#if UNITY_EDITOR
            int assignedCount = 0;
            int missingCount = 0;
            int outOfRangeCount = 0;
            int removedCount = 0;
#endif

            foreach (var (key, entity) in SystemAPI.Query<RefRO<RenderKey>>().WithEntityAccess())
            {
                var keyValue = key.ValueRO.ArchetypeId;

                if (!map.TryGetValue(keyValue, out var entry))
                {
#if UNITY_EDITOR
                    missingCount++;
                    LogMissingKey(keyValue, entity.Index);
#endif
                    if (em.HasComponent<MaterialMeshInfo>(entity))
                    {
                        ecb.RemoveComponent<MaterialMeshInfo>(entity);
#if UNITY_EDITOR
                        removedCount++;
#endif
                    }
                    continue;
                }

                if (entry.MaterialIndex < 0 || entry.MaterialIndex >= materialCount ||
                    entry.MeshIndex < 0 || entry.MeshIndex >= meshCount)
                {
#if UNITY_EDITOR
                    outOfRangeCount++;
                    LogOutOfRangeKey(keyValue, entry.MaterialIndex, entry.MeshIndex);
#endif
                    if (em.HasComponent<MaterialMeshInfo>(entity))
                    {
                        ecb.RemoveComponent<MaterialMeshInfo>(entity);
#if UNITY_EDITOR
                        removedCount++;
#endif
                    }
                    continue;
                }

                var mmi = MaterialMeshInfo.FromRenderMeshArrayIndices(
                    entry.MaterialIndex,
                    entry.MeshIndex,
                    (ushort)entry.SubMesh);

                if (em.HasComponent<MaterialMeshInfo>(entity))
                    ecb.SetComponent(entity, mmi);
                else
                    ecb.AddComponent(entity, mmi);

                if (!em.HasComponent<RenderBounds>(entity))
                {
                    var bounds = new RenderBounds
                    {
                        Value = new Unity.Mathematics.AABB
                        {
                            Center = entry.BoundsCenter,
                            Extents = entry.BoundsExtents
                        }
                    };
                    ecb.AddComponent(entity, bounds);
                }

                if (!em.HasComponent<RenderFilterSettings>(entity))
                {
                    ecb.AddSharedComponent(entity, defaultFilterSettings);
                }

#if UNITY_EDITOR
                assignedCount++;
#endif
            }

            ecb.Playback(em);
            ecb.Dispose();
            map.Dispose();

#if UNITY_EDITOR
            if (!s_loggedFirstPass || missingCount > 0 || outOfRangeCount > 0 || removedCount > 0)
            {
                Debug.Log(
                    $"[ApplyRenderCatalogSystem] Assigned {assignedCount} MaterialMeshInfo components. " +
                    $"Removed={removedCount} MissingKeys={missingCount} OutOfRange={outOfRangeCount} Entries={entries.Length}");
                s_loggedFirstPass = true;
            }
#endif
        }

#if UNITY_EDITOR
        static void LogMissingKey(int key, int entityIndex)
        {
            Debug.LogError($"[ApplyRenderCatalogSystem] Missing key {key} for entity {entityIndex}");
        }

        static void LogOutOfRangeKey(int key, int matIndex, int meshIndex)
        {
            Debug.LogError($"[ApplyRenderCatalogSystem] Key {key} has bad indices mat:{matIndex} mesh:{meshIndex}");
        }
#endif
    }
}




