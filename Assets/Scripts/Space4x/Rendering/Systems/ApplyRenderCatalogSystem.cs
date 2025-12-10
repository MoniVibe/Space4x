using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using Space4X.Rendering;

namespace Space4X.Rendering.Systems
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class ApplyRenderCatalogSystem : SystemBase
    {
        private EntityQuery _catalogQuery;
        private EntityQuery _renderKeyQuery;

        protected override void OnCreate()
        {
            RequireForUpdate<Space4XRenderCatalogSingleton>();

            _catalogQuery = GetEntityQuery(
                ComponentType.ReadOnly<Space4XRenderCatalogSingleton>(),
                ComponentType.ReadOnly<RenderMeshArray>());

            _renderKeyQuery = GetEntityQuery(
                ComponentType.ReadOnly<RenderKey>(),
                ComponentType.ReadWrite<MaterialMeshInfo>());
        }

        protected override void OnUpdate()
        {
            if (_catalogQuery.IsEmptyIgnoreFilter || _renderKeyQuery.IsEmptyIgnoreFilter)
                return;

            var em = EntityManager;

            using var catalogEntities = _catalogQuery.ToEntityArray(Allocator.Temp);
            var catalogEntity = catalogEntities[0];

            var catalogSingleton = em.GetComponentData<Space4XRenderCatalogSingleton>(catalogEntity);
            if (!catalogSingleton.Catalog.IsCreated)
                return;

            ref var catalog = ref catalogSingleton.Catalog.Value;
            ref var entries = ref catalog.Entries;

            var renderMeshArray = em.GetSharedComponentManaged<RenderMeshArray>(catalogEntity);
            var materialCount = renderMeshArray.Materials.Length;
            var meshCount = renderMeshArray.Meshes.Length;

            using var entities = _renderKeyQuery.ToEntityArray(Allocator.Temp);
            using var renderKeys = _renderKeyQuery.ToComponentDataArray<RenderKey>(Allocator.Temp);
            using var mmiArray = _renderKeyQuery.ToComponentDataArray<MaterialMeshInfo>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                var e = entities[i];
                var rk = renderKeys[i];
                var mmi = mmiArray[i];
                var archetypeId = rk.ArchetypeId;

                Space4XRenderMeshCatalogEntry? match = null;
                for (int j = 0; j < entries.Length; j++)
                {
                    if (entries[j].ArchetypeId == archetypeId)
                    {
                        match = entries[j];
                        break;
                    }
                }

                if (match is not Space4XRenderMeshCatalogEntry entry)
                    continue;

                if ((uint)entry.MaterialIndex >= (uint)materialCount ||
                    (uint)entry.MeshIndex >= (uint)meshCount)
                    continue;

                mmi = MaterialMeshInfo.FromRenderMeshArrayIndices(
                    entry.MaterialIndex,
                    entry.MeshIndex,
                    entry.SubMesh);

                em.SetComponentData(e, mmi);

                if (!em.HasComponent<RenderMeshArray>(e))
                {
                    em.AddSharedComponentManaged(e, renderMeshArray);
                }
            }
        }
    }
}
