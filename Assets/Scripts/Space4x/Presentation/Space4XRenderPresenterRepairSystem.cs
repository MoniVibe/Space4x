using PureDOTS.Rendering;
using Unity.Collections;
using Unity.Entities;

namespace Space4X.Presentation
{
    /// <summary>
    /// Ensures any entity with a semantic render key has presenter components and at least one presenter enabled
    /// before validation runs.
    /// </summary>
    [UpdateInGroup(typeof(Unity.Entities.PresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XPresentationModeSystem))]
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [UpdateBefore(typeof(RenderPresentationValidationSystem))]
#endif
    public partial struct Space4XRenderPresenterRepairSystem : ISystem
    {
        private EntityQuery _semanticQuery;

        public void OnCreate(ref SystemState state)
        {
            _semanticQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<RenderSemanticKey>()
                },
                Options = EntityQueryOptions.IgnoreComponentEnabledState
            });
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!PureDOTS.Runtime.Core.RuntimeMode.IsRenderingEnabled || _semanticQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var entityManager = state.EntityManager;
            using var entities = _semanticQuery.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                if (!entityManager.Exists(entity))
                {
                    continue;
                }

                var hasMesh = entityManager.HasComponent<MeshPresenter>(entity);
                var hasSprite = entityManager.HasComponent<SpritePresenter>(entity);
                var hasDebug = entityManager.HasComponent<DebugPresenter>(entity);
                var hasTracer = entityManager.HasComponent<TracerPresenter>(entity);

                if (!hasMesh)
                {
                    entityManager.AddComponentData(entity, new MeshPresenter
                    {
                        DefIndex = RenderPresentationConstants.UnassignedPresenterDefIndex
                    });
                    hasMesh = true;
                }

                if (!hasSprite)
                {
                    entityManager.AddComponentData(entity, new SpritePresenter
                    {
                        DefIndex = RenderPresentationConstants.UnassignedPresenterDefIndex
                    });
                    hasSprite = true;
                }

                if (!hasDebug)
                {
                    entityManager.AddComponentData(entity, new DebugPresenter
                    {
                        DefIndex = RenderPresentationConstants.UnassignedPresenterDefIndex
                    });
                    hasDebug = true;
                }

                if (!hasTracer)
                {
                    entityManager.AddComponentData(entity, new TracerPresenter
                    {
                        DefIndex = RenderPresentationConstants.UnassignedPresenterDefIndex
                    });
                    hasTracer = true;
                }

                var meshEnabled = hasMesh && entityManager.IsComponentEnabled<MeshPresenter>(entity);
                var spriteEnabled = hasSprite && entityManager.IsComponentEnabled<SpritePresenter>(entity);
                var debugEnabled = hasDebug && entityManager.IsComponentEnabled<DebugPresenter>(entity);
                var tracerEnabled = hasTracer && entityManager.IsComponentEnabled<TracerPresenter>(entity);
                var hasEnabledPresenter = meshEnabled || spriteEnabled || debugEnabled || tracerEnabled;

                if (!hasEnabledPresenter)
                {
                    entityManager.SetComponentEnabled<MeshPresenter>(entity, true);
                }
            }
        }
    }
}
