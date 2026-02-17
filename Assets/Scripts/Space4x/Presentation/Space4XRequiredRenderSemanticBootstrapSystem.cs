#if UNITY_EDITOR || DEVELOPMENT_BUILD
using PureDOTS.Rendering;
using Unity.Entities;

namespace Space4X.Presentation
{
    /// <summary>
    /// Provides the required semantic universe so render catalog validation and diagnostics can run deterministically.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateBefore(typeof(RenderPresentationCatalogValidationSystem))]
    public partial struct Space4XRequiredRenderSemanticBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RenderPresentationCatalog>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var entityManager = state.EntityManager;
            using var existing = entityManager.CreateEntityQuery(ComponentType.ReadOnly<RenderPresentationCatalogValidation.RequiredRenderSemanticKey>());
            if (!existing.IsEmptyIgnoreFilter)
            {
                state.Enabled = false;
                return;
            }

            var entity = entityManager.CreateEntity();
            entityManager.SetName(entity, "Space4XRequiredRenderSemanticKeys");
            var required = entityManager.AddBuffer<RenderPresentationCatalogValidation.RequiredRenderSemanticKey>(entity);
            required.Add(new RenderPresentationCatalogValidation.RequiredRenderSemanticKey { Value = Space4XRenderKeys.Carrier });
            required.Add(new RenderPresentationCatalogValidation.RequiredRenderSemanticKey { Value = Space4XRenderKeys.Miner });
            required.Add(new RenderPresentationCatalogValidation.RequiredRenderSemanticKey { Value = Space4XRenderKeys.Asteroid });
            required.Add(new RenderPresentationCatalogValidation.RequiredRenderSemanticKey { Value = Space4XRenderKeys.Projectile });
            required.Add(new RenderPresentationCatalogValidation.RequiredRenderSemanticKey { Value = Space4XRenderKeys.FleetImpostor });
            required.Add(new RenderPresentationCatalogValidation.RequiredRenderSemanticKey { Value = Space4XRenderKeys.Individual });
            required.Add(new RenderPresentationCatalogValidation.RequiredRenderSemanticKey { Value = Space4XRenderKeys.StrikeCraft });
            required.Add(new RenderPresentationCatalogValidation.RequiredRenderSemanticKey { Value = Space4XRenderKeys.ResourcePickup });
            required.Add(new RenderPresentationCatalogValidation.RequiredRenderSemanticKey { Value = Space4XRenderKeys.GhostTether });

            state.Enabled = false;
        }
    }
}
#endif
