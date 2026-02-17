using PureDOTS.Rendering;
using PureDOTS.Systems;
using Unity.Collections;
using Unity.Entities;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityDebug = UnityEngine.Debug;
#endif

namespace Space4X.Presentation
{
    /// <summary>
    /// Ensures any entity with a semantic render key also has presenter components before validation runs.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(PresentationContentResolveSystem))]
    public partial struct Space4XRenderPresenterRepairSystem : ISystem
    {
        private EntityQuery _missingPresenterQuery;
        private byte _loggedCreate;
        private byte _loggedRepair;

        public void OnCreate(ref SystemState state)
        {
            _missingPresenterQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<RenderSemanticKey>()
                },
                None = new[]
                {
                    ComponentType.ReadOnly<MeshPresenter>(),
                    ComponentType.ReadOnly<SpritePresenter>(),
                    ComponentType.ReadOnly<DebugPresenter>(),
                    ComponentType.ReadOnly<TracerPresenter>()
                },
                Options = EntityQueryOptions.IgnoreComponentEnabledState
            });

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_loggedCreate == 0)
            {
                _loggedCreate = 1;
                UnityDebug.Log($"[Space4XRenderPresenterRepairSystem] OnCreate World='{state.WorldUnmanaged.Name}'");
            }
#endif
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_missingPresenterQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var entityManager = state.EntityManager;
            using var entities = _missingPresenterQuery.ToEntityArray(Allocator.Temp);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_loggedRepair == 0)
            {
                _loggedRepair = 1;
                UnityDebug.Log($"[Space4XRenderPresenterRepairSystem] Repairing missing presenters World='{state.WorldUnmanaged.Name}' Count={entities.Length}");
            }
#endif
            for (var i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                if (!entityManager.Exists(entity))
                {
                    continue;
                }

                var hadMesh = entityManager.HasComponent<MeshPresenter>(entity);
                var hadSprite = entityManager.HasComponent<SpritePresenter>(entity);
                var hadDebug = entityManager.HasComponent<DebugPresenter>(entity);
                var hadTracer = entityManager.HasComponent<TracerPresenter>(entity);

                if (!entityManager.HasComponent<MeshPresenter>(entity))
                {
                    entityManager.AddComponentData(entity, new MeshPresenter
                    {
                        DefIndex = RenderPresentationConstants.UnassignedPresenterDefIndex
                    });
                }
                entityManager.SetComponentEnabled<MeshPresenter>(entity, true);

                if (!entityManager.HasComponent<SpritePresenter>(entity))
                {
                    entityManager.AddComponentData(entity, new SpritePresenter
                    {
                        DefIndex = RenderPresentationConstants.UnassignedPresenterDefIndex
                    });
                }
                entityManager.SetComponentEnabled<SpritePresenter>(entity, false);

                if (!entityManager.HasComponent<DebugPresenter>(entity))
                {
                    entityManager.AddComponentData(entity, new DebugPresenter
                    {
                        DefIndex = RenderPresentationConstants.UnassignedPresenterDefIndex
                    });
                }
                entityManager.SetComponentEnabled<DebugPresenter>(entity, false);

                if (!entityManager.HasComponent<TracerPresenter>(entity))
                {
                    entityManager.AddComponentData(entity, new TracerPresenter
                    {
                        DefIndex = RenderPresentationConstants.UnassignedPresenterDefIndex
                    });
                }
                entityManager.SetComponentEnabled<TracerPresenter>(entity, false);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (i < 3)
                {
                    var hasMesh = entityManager.HasComponent<MeshPresenter>(entity);
                    var hasSprite = entityManager.HasComponent<SpritePresenter>(entity);
                    var hasDebug = entityManager.HasComponent<DebugPresenter>(entity);
                    var hasTracer = entityManager.HasComponent<TracerPresenter>(entity);
                    UnityDebug.Log(
                        $"[Space4XRenderPresenterRepairSystem] Entity={entity} " +
                        $"before(mesh={hadMesh} sprite={hadSprite} debug={hadDebug} tracer={hadTracer}) " +
                        $"after(mesh={hasMesh} sprite={hasSprite} debug={hasDebug} tracer={hasTracer})");
                }
#endif
            }
        }
    }
}
