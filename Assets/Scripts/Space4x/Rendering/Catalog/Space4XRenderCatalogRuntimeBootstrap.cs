using PureDOTS.Rendering;
using UnityEngine;
using Unity.Entities;
using Unity.Rendering;
using UnityDebug = UnityEngine.Debug;

namespace Space4X.Rendering.Catalog
{
    using Debug = UnityEngine.Debug;

    
    /// <summary>
    /// Runtime sanity check for the Space4X render catalog singleton.
    /// </summary>
    public class Space4XRenderCatalogRuntimeBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null)
            {
                UnityDebug.LogWarning("[Space4XRenderCatalogRuntimeBootstrap] Default world is null, aborting.");
                return;
            }

            var em = world.EntityManager;

            var catalogQuery = em.CreateEntityQuery(ComponentType.ReadOnly<RenderPresentationCatalog>());

            if (!catalogQuery.TryGetSingleton(out RenderPresentationCatalog catalogSingleton))
            {
                UnityDebug.LogWarning("[Space4XRenderCatalogRuntimeBootstrap] RenderPresentationCatalog missing; ensure baker/authoring is present.");
                return;
            }

            ref var catalog = ref catalogSingleton.Blob.Value;
            var variantCount = catalog.Variants.Length;
            var themeCount = catalog.Themes.Length;

            if (variantCount == 0)
            {
                UnityDebug.LogWarning("[Space4XRenderCatalogRuntimeBootstrap] Catalog has zero entries.");
                return;
            }

            var hasRma = catalogSingleton.RenderMeshArrayEntity != Entity.Null &&
                         em.HasComponent<RenderMeshArray>(catalogSingleton.RenderMeshArrayEntity);

            UnityDebug.Log($"[Space4XRenderCatalogRuntimeBootstrap] Catalog present. Variants={variantCount} Themes={themeCount} Semantics={catalog.SemanticKeyCount}. RenderMeshArray: {(hasRma ? "Present" : "Missing")}.");
        }
    }
}
