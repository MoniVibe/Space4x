using System;
using System.Collections.Generic;
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
        private static readonly HashSet<int> s_fallbackWarningKeys = new();
        private static readonly HashSet<int> s_missingKeyLogIds = new();
        private static readonly HashSet<int> s_outOfRangeKeyLogIds = new();

        private static readonly ushort[] s_requiredKeys =
        {
            (ushort)Space4XRenderKeys.Carrier,
            (ushort)Space4XRenderKeys.Miner,
            (ushort)Space4XRenderKeys.Asteroid,
            (ushort)Space4XRenderKeys.Projectile,
            (ushort)Space4XRenderKeys.FleetImpostor
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
                var keyInt = (int)key;
                if (map.TryGetValue(keyInt, out _))
                    continue;

                var placeholder = fallback;
                placeholder.ArchetypeId = (ushort)key;
                placeholder.MaterialIndex = ClampIndex(placeholder.MaterialIndex, materialCount);
                placeholder.MeshIndex = ClampIndex(placeholder.MeshIndex, meshCount);
                placeholder.SubMesh = (ushort)math.max((int)placeholder.SubMesh, 0);
                map.TryAdd(keyInt, placeholder);
                if (ShouldLogCatalogWarnings() && s_fallbackWarningKeys.Add(keyInt))
                {
                    Debug.LogWarning(
                        $"[Space4X RenderCatalog] Missing catalog row for ArchetypeId={key}; using fallback mesh/material. " +
                        "Update Space4XRenderCatalogDefinition to provide art.");
                }
            }

            var rmaQuery = GetEntityQuery(ComponentType.ReadOnly<RenderKey>());
            em.AddSharedComponentManaged(rmaQuery, rma);

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var defaultFilterSettings = RenderFilterSettings.Default;

            int assignedCount = 0;
            int missingCount = 0;
            int outOfRangeCount = 0;
            int removedCount = 0;

            foreach (var (key, entity) in SystemAPI.Query<RefRO<RenderKey>>().WithEntityAccess())
            {
                var keyValue = key.ValueRO.ArchetypeId;

                if (!map.TryGetValue(keyValue, out var entry))
                {
                    missingCount++;
                    if (ShouldLogCatalogWarnings() && s_missingKeyLogIds.Add(keyValue))
                    {
                        LogMissingKey(keyValue, entity.Index);
                    }
                    if (em.HasComponent<MaterialMeshInfo>(entity))
                    {
                        ecb.RemoveComponent<MaterialMeshInfo>(entity);
                        removedCount++;
                    }
                    continue;
                }

                if (entry.MaterialIndex < 0 || entry.MaterialIndex >= materialCount ||
                    entry.MeshIndex < 0 || entry.MeshIndex >= meshCount)
                {
                    outOfRangeCount++;
                    if (ShouldLogCatalogWarnings() && s_outOfRangeKeyLogIds.Add(keyValue))
                    {
                        LogOutOfRangeKey(keyValue, entry.MaterialIndex, entry.MeshIndex);
                    }
                    if (em.HasComponent<MaterialMeshInfo>(entity))
                    {
                        ecb.RemoveComponent<MaterialMeshInfo>(entity);
                        removedCount++;
                    }
                    continue;
                }

                var mmi = MaterialMeshInfo.FromRenderMeshArrayIndices(
                    (ushort)entry.MaterialIndex,
                    (ushort)entry.MeshIndex,
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

                assignedCount++;
            }

            ecb.Playback(em);
            ecb.Dispose();
            map.Dispose();

            if (ShouldLogCatalogWarnings() &&
                (!s_loggedFirstPass || missingCount > 0 || outOfRangeCount > 0 || removedCount > 0))
            {
                Debug.Log(
                    $"[ApplyRenderCatalogSystem] Assigned {assignedCount} MaterialMeshInfo components. " +
                    $"Removed={removedCount} MissingKeys={missingCount} OutOfRange={outOfRangeCount} Entries={entries.Length}");
                s_loggedFirstPass = true;
            }
        }

        static void LogMissingKey(int key, int entityIndex)
        {
            Debug.LogWarning($"[ApplyRenderCatalogSystem] Missing key {key} for entity {entityIndex}");
        }

        static void LogOutOfRangeKey(int key, int matIndex, int meshIndex)
        {
            Debug.LogError($"[ApplyRenderCatalogSystem] Key {key} has bad indices mat:{matIndex} mesh:{meshIndex}");
        }

        static bool ShouldLogCatalogWarnings()
        {
#if UNITY_EDITOR
            return true;
#else
            return Debug.isDebugBuild && !Application.isBatchMode;
#endif
        }

        static int ClampIndex(int index, int count)
        {
            if (count <= 0)
                return 0;
            return math.clamp(index, 0, count - 1);
        }
    }
}
