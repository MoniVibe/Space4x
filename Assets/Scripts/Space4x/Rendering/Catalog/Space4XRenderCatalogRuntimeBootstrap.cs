using UnityEngine;
using Unity.Entities;
using Unity.Rendering;
using Space4X.Rendering;

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
                Debug.LogWarning("[Space4XRenderCatalogRuntimeBootstrap] Default world is null, aborting.");
                return;
            }

            var em = world.EntityManager;

            var catalogQuery = em.CreateEntityQuery(ComponentType.ReadOnly<Space4XRenderCatalogSingleton>());
            var rmaQuery = em.CreateEntityQuery(ComponentType.ReadOnly<RenderMeshArray>());

            if (!catalogQuery.TryGetSingleton(out Space4XRenderCatalogSingleton catalogSingleton))
            {
                Debug.LogWarning("[Space4XRenderCatalogRuntimeBootstrap] Catalog singleton missing; ensure baker/authoring is present.");
                return;
            }

            ref var catalog = ref catalogSingleton.Catalog.Value;
            var entryCount = catalog.Entries.Length;

            if (entryCount == 0)
            {
                Debug.LogWarning("[Space4XRenderCatalogRuntimeBootstrap] Catalog has zero entries.");
                return;
            }

            var hasRma = !rmaQuery.IsEmpty;
            Debug.Log($"[Space4XRenderCatalogRuntimeBootstrap] Catalog present with {entryCount} entries. RenderMeshArray: {(hasRma ? "Present" : "Missing")}.");
        }
    }
}

