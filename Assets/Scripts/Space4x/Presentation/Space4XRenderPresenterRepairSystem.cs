using PureDOTS.Rendering;
using Unity.Collections;
using Unity.Entities;

namespace Space4X.Presentation
{
    /// <summary>
    /// Ensures any entity with a semantic render key also has presenter components before validation runs.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XPresentationLifecycleSystem))]
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [UpdateBefore(typeof(RenderPresentationValidationSystem))]
#endif
    public partial struct Space4XRenderPresenterRepairSystem : ISystem
    {
        private EntityQuery _missingPresenterQuery;

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
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_missingPresenterQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var entityManager = state.EntityManager;
            using var entities = _missingPresenterQuery.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                if (!entityManager.Exists(entity))
                {
                    continue;
                }

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
            }
        }
    }
}
