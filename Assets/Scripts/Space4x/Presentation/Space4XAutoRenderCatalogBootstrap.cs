using PureDOTS.Rendering;
using UnityEngine;

namespace Space4X.Presentation
{
    internal static class Space4XAutoRenderCatalogBootstrap
    {
        private const string CatalogResourceName = "Space4XRenderCatalog_v2";
        private const string BootstrapObjectName = "Space4XAutoRenderCatalogBootstrap";
        private const string LogPrefix = "Space4XAutoRenderCatalogBootstrap";
        private static bool _logged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureCatalogBootstrap()
        {
            RenderCatalogAutoBootstrapUtility.EnsureCatalogBootstrapFromResources(
                CatalogResourceName,
                BootstrapObjectName,
                LogPrefix,
                ref _logged,
                warnIfCatalogMissing: true);
        }
    }
}
