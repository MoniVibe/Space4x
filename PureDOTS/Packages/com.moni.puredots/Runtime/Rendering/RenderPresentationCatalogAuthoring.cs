using UnityEngine;

namespace PureDOTS.Rendering
{
    /// <summary>
    /// Game-side authoring component that references the presentation catalog asset.
    /// Baking/runtime bootstrap scripts consume this to produce the runtime catalog.
    /// </summary>
    [DisallowMultipleComponent]
    public class RenderPresentationCatalogAuthoring : MonoBehaviour
    {
        public RenderPresentationCatalogDefinition CatalogDefinition;
    }
}
