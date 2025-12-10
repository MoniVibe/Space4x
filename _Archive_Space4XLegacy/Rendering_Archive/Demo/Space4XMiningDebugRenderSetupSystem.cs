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
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(Space4XMiningDemoBootstrapSystem))]
    [UpdateAfter(typeof(PureDOTS.Systems.Bootstrap.DemoScenarioRunnerSystem))]
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

            var em = state.EntityManager;
            var renderQuery = em.CreateEntityQuery(ComponentType.ReadOnly<RenderMeshArraySingleton>());
            if (renderQuery.IsEmptyIgnoreFilter)
            {
                // UnityEngine.Debug.LogWarning("[Space4XMiningDebugRenderSetupSystem] RenderMeshArraySingleton not found.");
                return;
            }

            var renderArray = em.GetSharedComponentManaged<RenderMeshArraySingleton>(renderQuery.GetSingletonEntity()).Value;
            if (renderArray == null)
            {
                // UnityEngine.Debug.LogWarning("[Space4XMiningDebugRenderSetupSystem] RenderMeshArray is null.");
                return;
            }

            var desc = new RenderMeshDescription();
            AssignIfMissing(em, desc, renderArray,
                em.CreateEntityQuery(ComponentType.ReadOnly<PlatformTag>(), ComponentType.ReadOnly<LocalTransform>(), ComponentType.Exclude<MaterialMeshInfo>()),
                DemoMeshIndices.VillageHomeMeshIndex, DemoMeshIndices.DemoMaterialIndex); // big cube for carriers

#if SPACE4X_AVAILABLE
            AssignIfMissing(em, desc, renderArray,
                em.CreateEntityQuery(ComponentType.ReadOnly<MiningVesselTag>(), ComponentType.ReadOnly<LocalTransform>(), ComponentType.Exclude<MaterialMeshInfo>()),
                DemoMeshIndices.VillageVillagerMeshIndex, DemoMeshIndices.DemoMaterialIndex); // smaller cube for miners
#endif

            AssignIfMissing(em, desc, renderArray,
                em.CreateEntityQuery(ComponentType.ReadOnly<ResourceNodeTag>(), ComponentType.ReadOnly<LocalTransform>(), ComponentType.Exclude<MaterialMeshInfo>()),
                DemoMeshIndices.VillageWorkMeshIndex, DemoMeshIndices.DemoMaterialIndex); // cube for asteroids

            _initialized = true;
            state.Enabled = false;
        }

        private static void AssignIfMissing(EntityManager em, in RenderMeshDescription desc, RenderMeshArray array, EntityQuery query, int meshIndex, int materialIndex)
        {
            using var entities = query.ToEntityArray(Allocator.Temp);
            if (entities.Length > 0)
            {
                UnityEngine.Debug.Log($"[Space4XMiningDebugRenderSetupSystem] Adding render components to {entities.Length} entities (Mesh: {meshIndex}, Mat: {materialIndex}).");
                for (int i = 0; i < entities.Length; i++)
                {
                    var entity = entities[i];

                    RenderMeshUtility.AddComponents(
                        entity,
                        em,
                        desc,
                        array,
                        MaterialMeshInfo.FromRenderMeshArrayIndices(meshIndex, materialIndex));
                }
            }
        }
    }
}
