using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using PureDOTS.Runtime.Core;
using RenderKey = PureDOTS.Rendering.RenderKey;
using RenderFlags = PureDOTS.Rendering.RenderFlags;

namespace Godgame.Rendering.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(Unity.Rendering.EntitiesGraphicsSystem))]
    public partial class GodgameApplyRenderCatalogSystem : SystemBase
    {
        private EntityQuery _pendingQuery;
        private EntityQuery _renderKeyQuery;
        private NativeParallelHashMap<int, GodgameRenderMeshCatalogEntry> _catalogMap;
        private BlobAssetReference<GodgameRenderMeshCatalog> _cachedCatalog;

        protected override void OnCreate()
        {
            RequireForUpdate<GodgameRenderCatalogSingleton>();
            RequireForUpdate<RenderKey>();
            RequireForUpdate<RenderFlags>();
            _pendingQuery = GetEntityQuery(
                ComponentType.ReadOnly<RenderKey>(),
                ComponentType.ReadOnly<RenderFlags>(),
                ComponentType.Exclude<RenderCatalogAppliedTag>());
            _renderKeyQuery = GetEntityQuery(ComponentType.ReadOnly<RenderKey>());
            _catalogMap = new NativeParallelHashMap<int, GodgameRenderMeshCatalogEntry>(1, Allocator.Persistent);
            _cachedCatalog = default;
        }

        protected override void OnUpdate()
        {
            if (RuntimeMode.IsHeadless)
                return;
            var catalogEntity = SystemAPI.GetSingletonEntity<GodgameRenderCatalogSingleton>();
            var catalog = SystemAPI.GetComponent<GodgameRenderCatalogSingleton>(catalogEntity);
            if (!catalog.Catalog.IsCreated)
                return;

            ref var entries = ref catalog.Catalog.Value.Entries;
            if (entries.Length == 0)
                return;

            if (_pendingQuery.IsEmptyIgnoreFilter)
                return;

            var em = EntityManager;
            var rma = em.GetSharedComponentManaged<RenderMeshArray>(catalogEntity);
            var materialRefs = rma.MaterialReferences;
            var meshRefs = rma.MeshReferences;
            var materialCount = materialRefs?.Length ?? 0;
            var meshCount = meshRefs?.Length ?? 0;
            if (materialCount == 0 || meshCount == 0)
                return;

            EnsureCatalogMap(ref catalog.Catalog, materialCount, meshCount);
            var fallbackEntry = entries[0];

            // Ensure all render key entities share the RenderMeshArray once for batching
            EntityManager.AddSharedComponentManaged(_renderKeyQuery, rma);

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var defaultFilter = RenderFilterSettings.Default;

#if UNITY_EDITOR
            int assigned = 0;
            int missing = 0;
            int outOfRange = 0;
            int fallbackAssignments = 0;
#endif

            foreach (var (key, flags, entity) in SystemAPI.Query<RefRO<RenderKey>, RefRO<RenderFlags>>()
                         .WithNone<RenderCatalogAppliedTag>()
                         .WithEntityAccess())
            {
                if (flags.ValueRO.Visible == 0)
                    continue;

                var entry = fallbackEntry;
                var usingFallback = true;

                if (_catalogMap.TryGetValue(key.ValueRO.ArchetypeId, out var candidate))
                {
                    if (IsEntryInRange(candidate, materialCount, meshCount))
                    {
                        entry = candidate;
                        usingFallback = false;
                    }
                    else
                    {
#if UNITY_EDITOR
                        outOfRange++;
#endif
                    }
                }
                else
                {
#if UNITY_EDITOR
                    missing++;
#endif
                }

#if UNITY_EDITOR
                if (usingFallback)
                {
                    fallbackAssignments++;
                }
#endif

                entry = usingFallback ? EnsurePlaceholderBounds(entry) : entry;

                var mmi = BuildMaterialMeshInfo(entry, materialCount, meshCount);
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
                    ecb.AddSharedComponent(entity, defaultFilter);
                }

                ecb.AddComponent<RenderCatalogAppliedTag>(entity);

#if UNITY_EDITOR
                assigned++;
#endif
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();

#if UNITY_EDITOR
            if (missing > 0)
            {
                Debug.LogWarning($"[GodgameApplyRenderCatalogSystem] Missing {missing} catalog entry mappings. Fallback applied.");
            }
            if (outOfRange > 0)
            {
                Debug.LogWarning($"[GodgameApplyRenderCatalogSystem] {outOfRange} catalog entries referenced invalid indices. Fallback applied.");
            }
            if (assigned > 0)
            {
                Debug.Log($"[GodgameApplyRenderCatalogSystem] Assigned {assigned} MaterialMeshInfo components (fallback used {fallbackAssignments} times).");
            }
#endif
        }

        static bool IsEntryInRange(in GodgameRenderMeshCatalogEntry entry, int materialCount, int meshCount)
        {
            return entry.MaterialIndex >= 0 && entry.MaterialIndex < materialCount &&
                   entry.MeshIndex >= 0 && entry.MeshIndex < meshCount;
        }

        static MaterialMeshInfo BuildMaterialMeshInfo(in GodgameRenderMeshCatalogEntry entry, int materialCount, int meshCount)
        {
            return MaterialMeshInfo.FromRenderMeshArrayIndices(
                (ushort)ClampIndex(entry.MaterialIndex, materialCount),
                (ushort)ClampIndex(entry.MeshIndex, meshCount),
                entry.SubMesh);
        }

        static RenderBounds BuildBounds(in GodgameRenderMeshCatalogEntry entry)
        {
            return new RenderBounds
            {
                Value = new AABB
                {
                    Center = entry.BoundsCenter,
                    Extents = entry.BoundsExtents
                }
            };
        }

        static int ClampIndex(int index, int count)
        {
            if (count <= 0)
                return 0;
            return math.clamp(index, 0, count - 1);
        }

        void EnsureCatalogMap(ref BlobAssetReference<GodgameRenderMeshCatalog> catalog, int materialCount, int meshCount)
        {
            var catalogMatches = _catalogMap.IsCreated &&
                                 _cachedCatalog.IsCreated &&
                                 _cachedCatalog.Equals(catalog);

            if (catalogMatches)
                return;

            if (_catalogMap.IsCreated)
            {
                _catalogMap.Dispose();
            }

            ref var entries = ref catalog.Value.Entries;
            var capacity = math.max(entries.Length, 1);
            _catalogMap = new NativeParallelHashMap<int, GodgameRenderMeshCatalogEntry>(capacity, Allocator.Persistent);

            for (int i = 0; i < entries.Length; i++)
            {
                _catalogMap.TryAdd(entries[i].ArchetypeId, entries[i]);
            }

            _cachedCatalog = catalog;
        }

        static GodgameRenderMeshCatalogEntry EnsurePlaceholderBounds(GodgameRenderMeshCatalogEntry entry)
        {
            entry.BoundsCenter = float3.zero;
            entry.BoundsExtents = math.max(entry.BoundsExtents, new float3(32f));
            return entry;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (_catalogMap.IsCreated)
            {
                _catalogMap.Dispose();
            }
            _cachedCatalog = default;
        }
    }
}
