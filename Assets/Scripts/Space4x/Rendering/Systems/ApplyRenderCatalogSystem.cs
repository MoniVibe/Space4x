using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using PureDOTS.Runtime.Core;
using Space4X.Rendering;

namespace Space4X.Rendering.Systems
{
    using Debug = UnityEngine.Debug;

    [UpdateInGroup(typeof(Space4XRenderSystemGroup))]
    [UpdateBefore(typeof(StripInvalidMaterialMeshInfoSystem))]
    [UpdateBefore(typeof(DebugVerifyVisualsSystem))]
    public struct RenderCatalogAppliedTag : IComponentData { }

    [UpdateInGroup(typeof(Space4XRenderSystemGroup))]
    public partial struct ApplyRenderCatalogSystem : ISystem
    {
        private NativeParallelHashMap<int, Space4XRenderMeshCatalogEntry> _catalogMap;
        private BlobAssetReference<Space4XRenderMeshCatalog> _cachedCatalog;
        private EntityQuery _unprocessedQuery;
        private EntityQuery _renderKeyQuery;
        private static readonly float3 s_placeholderBoundsExtents = new float3(32f);

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

        public void OnCreate(ref SystemState state)
        {
            if (state.WorldUnmanaged.Name != "Game World")
            {
                state.Enabled = false;
                return;
            }
            state.RequireForUpdate<Space4XRenderCatalogSingleton>();
            state.RequireForUpdate<RenderKey>();
            state.RequireForUpdate<RenderFlags>();
            _unprocessedQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<RenderKey>(),
                ComponentType.ReadOnly<RenderFlags>(),
                ComponentType.Exclude<RenderCatalogAppliedTag>());
            _renderKeyQuery = state.GetEntityQuery(ComponentType.ReadOnly<RenderKey>());
            _catalogMap = new NativeParallelHashMap<int, Space4XRenderMeshCatalogEntry>(1, Allocator.Persistent);
            _cachedCatalog = default;
        }

        public void OnUpdate(ref SystemState state)
        {
            if (RuntimeMode.IsHeadless)
                return;

            if (_unprocessedQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var em = state.EntityManager;
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

            EnsureCatalogMap(ref catalogBlob, materialCount, meshCount);
            var fallback = entries[0];

            // Only update shared component if there are unprocessed entities
            if (!_unprocessedQuery.IsEmptyIgnoreFilter)
            {
                em.AddSharedComponentManaged(_renderKeyQuery, rma);
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var defaultFilterSettings = RenderFilterSettings.Default;

            int assignedCount = 0;
            int missingCount = 0;
            int outOfRangeCount = 0;
            int fallbackAssignments = 0;

            foreach (var (key, flags, entity) in SystemAPI.Query<RefRO<RenderKey>, RefRO<RenderFlags>>()
                         .WithNone<RenderCatalogAppliedTag>()
                         .WithEntityAccess())
            {
                if (flags.ValueRO.Visible == 0)
                    continue;

                var keyValue = key.ValueRO.ArchetypeId;
                var entry = fallback;
                var usingFallback = true;

                if (_catalogMap.TryGetValue(keyValue, out var candidate))
                {
                    if (IsEntryInRange(candidate, materialCount, meshCount))
                    {
                        entry = candidate;
                        usingFallback = false;
                    }
                    else
                    {
                        outOfRangeCount++;
                        if (ShouldLogCatalogWarnings() && s_outOfRangeKeyLogIds.Add(keyValue))
                        {
                            LogOutOfRangeKey(keyValue, candidate.MaterialIndex, candidate.MeshIndex);
                        }
                    }
                }
                else
                {
                    missingCount++;
                    if (ShouldLogCatalogWarnings() && s_missingKeyLogIds.Add(keyValue))
                    {
                        LogMissingKey(keyValue, entity.Index);
                    }
                }

                if (usingFallback)
                {
                    fallbackAssignments++;
                    entry = EnsurePlaceholderBounds(entry);
                }

                var mmi = MaterialMeshInfo.FromRenderMeshArrayIndices(
                    (ushort)ClampIndex(entry.MaterialIndex, materialCount),
                    (ushort)ClampIndex(entry.MeshIndex, meshCount),
                    (ushort)math.max(entry.SubMesh, 0));

                if (em.HasComponent<MaterialMeshInfo>(entity))
                    ecb.SetComponent(entity, mmi);
                else
                    ecb.AddComponent(entity, mmi);

                if (!em.HasComponent<RenderBounds>(entity))
                {
                    ecb.AddComponent(entity, BuildBounds(entry));
                }

                if (!em.HasComponent<RenderFilterSettings>(entity))
                {
                    ecb.AddSharedComponent(entity, defaultFilterSettings);
                }

                ecb.AddComponent<RenderCatalogAppliedTag>(entity);
                assignedCount++;
            }

            ecb.Playback(em);
            ecb.Dispose();

            if (ShouldLogCatalogWarnings() &&
                (!s_loggedFirstPass || missingCount > 0 || outOfRangeCount > 0 || fallbackAssignments > 0))
            {
                if (assignedCount > 0)
                {
                    Debug.Log(
                        $"[ApplyRenderCatalogSystem] Assigned {assignedCount} MaterialMeshInfo components. " +
                        $"Fallback={fallbackAssignments} MissingKeys={missingCount} OutOfRange={outOfRangeCount} Entries={entries.Length}");
                    s_loggedFirstPass = true;
                }
            }
        }

        public void OnDestroy(ref SystemState state)
        {
            if (_catalogMap.IsCreated)
            {
                _catalogMap.Dispose();
            }
            _cachedCatalog = default;
        }

        private void EnsureCatalogMap(ref BlobAssetReference<Space4XRenderMeshCatalog> catalogBlob, int materialCount, int meshCount)
        {
            var catalogMatches = _catalogMap.IsCreated &&
                                 _cachedCatalog.IsCreated &&
                                 _cachedCatalog.Equals(catalogBlob);

            if (catalogMatches)
                return;

            if (_catalogMap.IsCreated)
            {
                _catalogMap.Dispose();
            }

            ref var entries = ref catalogBlob.Value.Entries;
            var capacity = math.max(entries.Length + s_requiredKeys.Length, 1);
            _catalogMap = new NativeParallelHashMap<int, Space4XRenderMeshCatalogEntry>(capacity, Allocator.Persistent);

            for (int i = 0; i < entries.Length; i++)
            {
                _catalogMap.TryAdd(entries[i].ArchetypeId, entries[i]);
            }

            var fallback = entries.Length > 0 ? entries[0] : default;
            foreach (var key in s_requiredKeys)
            {
                var keyInt = (int)key;
                if (_catalogMap.ContainsKey(keyInt))
                    continue;

                var placeholder = fallback;
                placeholder.ArchetypeId = (ushort)key;
                placeholder.MaterialIndex = ClampIndex(placeholder.MaterialIndex, materialCount);
                placeholder.MeshIndex = ClampIndex(placeholder.MeshIndex, meshCount);
                placeholder.SubMesh = (ushort)math.max((int)placeholder.SubMesh, 0);
                placeholder = EnsurePlaceholderBounds(placeholder);

                _catalogMap.TryAdd(keyInt, placeholder);
                if (ShouldLogCatalogWarnings() && s_fallbackWarningKeys.Add(keyInt))
                {
                    Debug.LogWarning(
                        $"[Space4X RenderCatalog] Missing catalog row for ArchetypeId={key}; using fallback mesh/material. " +
                        "Update Space4XRenderCatalogDefinition to provide art.");
                }
            }

            _cachedCatalog = catalogBlob;
        }

        private static Space4XRenderMeshCatalogEntry EnsurePlaceholderBounds(Space4XRenderMeshCatalogEntry entry)
        {
            entry.BoundsCenter = float3.zero;
            entry.BoundsExtents = math.max(entry.BoundsExtents, s_placeholderBoundsExtents);
            return entry;
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

        static bool IsEntryInRange(in Space4XRenderMeshCatalogEntry entry, int materialCount, int meshCount)
        {
            return entry.MaterialIndex >= 0 && entry.MaterialIndex < materialCount &&
                   entry.MeshIndex >= 0 && entry.MeshIndex < meshCount;
        }

        static RenderBounds BuildBounds(in Space4XRenderMeshCatalogEntry entry)
        {
            return new RenderBounds
            {
                Value = new Unity.Mathematics.AABB
                {
                    Center = entry.BoundsCenter,
                    Extents = entry.BoundsExtents
                }
            };
        }
    }
}
