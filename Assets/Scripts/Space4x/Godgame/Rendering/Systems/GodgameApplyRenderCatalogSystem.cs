using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using RenderKey = PureDOTS.Rendering.RenderKey;
using RenderFlags = PureDOTS.Rendering.RenderFlags;

namespace Godgame.Rendering.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(Unity.Rendering.EntitiesGraphicsSystem))]
    public partial class GodgameApplyRenderCatalogSystem : SystemBase
    {
        EntityQuery _renderKeyQuery;

        protected override void OnCreate()
        {
            RequireForUpdate<GodgameRenderCatalogSingleton>();
            RequireForUpdate<RenderKey>();
            _renderKeyQuery = GetEntityQuery(ComponentType.ReadOnly<RenderKey>());
        }

        protected override void OnUpdate()
        {
            var catalogEntity = SystemAPI.GetSingletonEntity<GodgameRenderCatalogSingleton>();
            var catalog = SystemAPI.GetComponent<GodgameRenderCatalogSingleton>(catalogEntity);
            if (!catalog.Catalog.IsCreated)
                return;

            ref var entries = ref catalog.Catalog.Value.Entries;
            if (entries.Length == 0)
                return;

            var map = new NativeParallelHashMap<int, GodgameRenderMeshCatalogEntry>(entries.Length, Allocator.Temp);
            for (int i = 0; i < entries.Length; i++)
            {
                map.TryAdd(entries[i].ArchetypeId, entries[i]);
            }

            var em = EntityManager;
            var rma = em.GetSharedComponentManaged<RenderMeshArray>(catalogEntity);
            em.AddSharedComponentManaged(_renderKeyQuery, rma);

            var defaultFilter = RenderFilterSettings.Default;

#if UNITY_EDITOR
            int assigned = 0;
            int missing = 0;
#endif

            foreach (var (key, flags, entity) in SystemAPI.Query<RefRO<RenderKey>, RefRO<RenderFlags>>().WithEntityAccess())
            {
                if (flags.ValueRO.Visible == 0)
                    continue;

                if (!map.TryGetValue(key.ValueRO.ArchetypeId, out var entry))
                {
#if UNITY_EDITOR
                    missing++;
#endif
                    if (em.HasComponent<MaterialMeshInfo>(entity))
                    {
                        em.RemoveComponent<MaterialMeshInfo>(entity);
                    }
                    continue;
                }

                var mmi = MaterialMeshInfo.FromRenderMeshArrayIndices(entry.MaterialIndex, entry.MeshIndex, entry.SubMesh);
                if (em.HasComponent<MaterialMeshInfo>(entity))
                    em.SetComponentData(entity, mmi);
                else
                    em.AddComponentData(entity, mmi);

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
                    em.AddComponentData(entity, bounds);
                }

                if (!em.HasComponent<RenderFilterSettings>(entity))
                {
                    em.AddSharedComponent(entity, defaultFilter);
                }

#if UNITY_EDITOR
                assigned++;
#endif
            }

            map.Dispose();

#if UNITY_EDITOR
            if (missing > 0)
            {
                Debug.LogWarning($"[GodgameApplyRenderCatalogSystem] Missing {missing} catalog entry mappings.");
            }
            if (assigned > 0)
            {
                Debug.Log($"[GodgameApplyRenderCatalogSystem] Assigned {assigned} MaterialMeshInfo components.");
            }
#endif
        }
    }
}
