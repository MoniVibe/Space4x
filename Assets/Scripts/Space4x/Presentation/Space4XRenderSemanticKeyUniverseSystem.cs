using PureDOTS.Rendering;
using Space4X.Presentation;
using Unity.Burst;
using Unity.Entities;

namespace Space4X.Presentation
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD || UNITY_INCLUDE_TESTS
    /// <summary>
    /// Publishes the "required semantic key universe" for Space4X so PureDOTS can enforce:
    /// - ThemeId=0 maps every required semantic key (for every baked LOD)
    /// - Missing mappings that fall back to Variant 0 are detectable and reportable (dev-only)
    ///
    /// Pure validation only (no presentation offsets/smoothing hacks).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4XRenderSemanticKeyUniverseSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // Run once; create singleton if missing.
        }

        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.HasSingleton<RenderPresentationCatalogValidation.RequiredRenderSemanticKey>())
                return;

            var e = state.EntityManager.CreateEntity();
            state.EntityManager.SetName(e, "Space4X.RequiredRenderSemanticKeys");
            var buf = state.EntityManager.AddBuffer<RenderPresentationCatalogValidation.RequiredRenderSemanticKey>(e);

            // Keep this list explicit and stable; add here whenever Space4XRenderKeys grows.
            buf.Add(new RenderPresentationCatalogValidation.RequiredRenderSemanticKey { Value = Space4XRenderKeys.Carrier });
            buf.Add(new RenderPresentationCatalogValidation.RequiredRenderSemanticKey { Value = Space4XRenderKeys.Miner });
            buf.Add(new RenderPresentationCatalogValidation.RequiredRenderSemanticKey { Value = Space4XRenderKeys.Asteroid });
            buf.Add(new RenderPresentationCatalogValidation.RequiredRenderSemanticKey { Value = Space4XRenderKeys.Projectile });
            buf.Add(new RenderPresentationCatalogValidation.RequiredRenderSemanticKey { Value = Space4XRenderKeys.FleetImpostor });
            buf.Add(new RenderPresentationCatalogValidation.RequiredRenderSemanticKey { Value = Space4XRenderKeys.Individual });
            buf.Add(new RenderPresentationCatalogValidation.RequiredRenderSemanticKey { Value = Space4XRenderKeys.StrikeCraft });
            buf.Add(new RenderPresentationCatalogValidation.RequiredRenderSemanticKey { Value = Space4XRenderKeys.ResourcePickup });
            buf.Add(new RenderPresentationCatalogValidation.RequiredRenderSemanticKey { Value = Space4XRenderKeys.GhostTether });
        }
    }
#endif
}

