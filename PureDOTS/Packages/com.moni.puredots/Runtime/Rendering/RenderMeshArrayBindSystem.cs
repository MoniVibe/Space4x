using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;

namespace PureDOTS.Rendering
{
    /// <summary>
    /// Ensures every MaterialMeshInfo entity has the correct RenderMeshArray shared component before Entities Graphics runs.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(ApplyRenderVariantSystem))]
    [UpdateBefore(typeof(Unity.Rendering.EntitiesGraphicsSystem))]
    public partial struct RenderMeshArrayBindSystem : ISystem
    {
        private EntityQuery _missingArrayQuery;
        private EntityQuery _allMaterialMeshInfoQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RenderPresentationCatalog>();
            state.RequireForUpdate<RenderCatalogVersion>();
            _missingArrayQuery = state.GetEntityQuery(new EntityQueryBuilder(Allocator.Temp)
                .WithAll<MaterialMeshInfo>()
                .WithNone<RenderMeshArray>());
            _allMaterialMeshInfoQuery = state.GetEntityQuery(ComponentType.ReadOnly<MaterialMeshInfo>());
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out RenderPresentationCatalog catalog) || catalog.RenderMeshArrayEntity == Entity.Null)
            {
                return;
            }

            var renderMeshArray = state.EntityManager.GetSharedComponentManaged<RenderMeshArray>(catalog.RenderMeshArrayEntity);
            var targetMeshCount = renderMeshArray.MeshReferences != null ? renderMeshArray.MeshReferences.Length : 0;
            var targetMaterialCount = renderMeshArray.MaterialReferences != null ? renderMeshArray.MaterialReferences.Length : 0;

            if (_missingArrayQuery.IsEmptyIgnoreFilter && _allMaterialMeshInfoQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            // Bind the catalog's RenderMeshArray to every entity that participates in Entities Graphics.
            // This is intentionally defensive: if an entity ends up with a stale/foreign RenderMeshArray shared
            // component, Entities Graphics will spam out-of-bounds Mesh/Material errors every frame.
            using (var entities = _allMaterialMeshInfoQuery.ToEntityArray(Allocator.Temp))
            {
                foreach (var entity in entities)
                {
                    if (state.EntityManager.HasComponent<RenderMeshArray>(entity))
                    {
                        var current = state.EntityManager.GetSharedComponentManaged<RenderMeshArray>(entity);
                        var currentMeshCount = current.MeshReferences != null ? current.MeshReferences.Length : 0;
                        var currentMaterialCount = current.MaterialReferences != null ? current.MaterialReferences.Length : 0;
                        if (currentMeshCount != targetMeshCount || currentMaterialCount != targetMaterialCount)
                        {
                            state.EntityManager.SetSharedComponentManaged(entity, renderMeshArray);
                        }
                    }
                    else
                    {
                        state.EntityManager.AddSharedComponentManaged(entity, renderMeshArray);
                    }
                }
            }
        }

        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
