using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;
using PureDOTS.Demo.Rendering;
using PureDOTS.Runtime;
using PureDOTS.Runtime.Platform;
#if SPACE4X_AVAILABLE
using Space4X.Mining;
#endif

namespace Space4X.Demo
{
    /// <summary>
    /// Assigns simple render components to Space4X mining demo entities (carriers, miners, asteroids)
    /// that are missing MaterialMeshInfo. Runs once per world to guarantee visibility.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4XMiningDebugRenderSetupSystem : ISystem
    {
        private bool _initialized;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RenderMeshArraySingleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_initialized)
                return;

            _initialized = true;

            var em = state.EntityManager;
            var renderQuery = em.CreateEntityQuery(ComponentType.ReadOnly<RenderMeshArraySingleton>());
            if (renderQuery.IsEmptyIgnoreFilter)
                return;

            var renderArray = em.GetSharedComponentManaged<RenderMeshArraySingleton>(renderQuery.GetSingletonEntity()).Value;
            if (renderArray == null)
                return;

            var desc = new RenderMeshDescription();
            AssignIfMissing(em, desc, renderArray,
                em.CreateEntityQuery(ComponentType.ReadOnly<PlatformTag>(), ComponentType.ReadOnly<LocalTransform>(), ComponentType.Exclude<MaterialMeshInfo>()),
                DemoMeshIndices.DemoMaterialIndex, DemoMeshIndices.VillageHomeMeshIndex); // big cube for carriers

#if SPACE4X_AVAILABLE
            AssignIfMissing(em, desc, renderArray,
                em.CreateEntityQuery(ComponentType.ReadOnly<MiningVesselTag>(), ComponentType.ReadOnly<LocalTransform>(), ComponentType.Exclude<MaterialMeshInfo>()),
                DemoMeshIndices.DemoMaterialIndex, DemoMeshIndices.VillageVillagerMeshIndex); // smaller cube for miners
#endif

            AssignIfMissing(em, desc, renderArray,
                em.CreateEntityQuery(ComponentType.ReadOnly<ResourceNodeTag>(), ComponentType.ReadOnly<LocalTransform>(), ComponentType.Exclude<MaterialMeshInfo>()),
                DemoMeshIndices.DemoMaterialIndex, DemoMeshIndices.VillageWorkMeshIndex); // cube for asteroids
        }

        private static void AssignIfMissing(EntityManager em, in RenderMeshDescription desc, RenderMeshArray array, EntityQuery query, int materialIndex, int meshIndex)
        {
            using var entities = query.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                RenderMeshUtility.AddComponents(
                    entities[i],
                    em,
                    desc,
                    array,
                    MaterialMeshInfo.FromRenderMeshArrayIndices(meshIndex, materialIndex));
            }
        }
    }
}
