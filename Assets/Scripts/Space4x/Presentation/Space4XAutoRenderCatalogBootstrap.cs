using PureDOTS.Rendering;
using PureDOTS.Runtime.Core;
using Unity.Entities;
using UnityEngine;
using UnityDebug = UnityEngine.Debug;

namespace Space4X.Presentation
{
    internal static class Space4XAutoRenderCatalogBootstrap
    {
        private const string CatalogResourceName = "Space4XRenderCatalog_v2";
        private const string BootstrapObjectName = "Space4XAutoRenderCatalogBootstrap";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureCatalogBootstrap()
        {
            if (!RuntimeMode.IsRenderingEnabled)
            {
                return;
            }

            var catalog = Resources.Load<RenderPresentationCatalogDefinition>(CatalogResourceName);
            var variantCount = catalog?.Variants?.Length ?? 0;
            var theme0Mappings = GetTheme0MappingCount(catalog);

            if (HasCatalogSingleton())
            {
                UnityDebug.Log($"[Space4XAutoRenderCatalogBootstrap] catalog_ready=1 variants={variantCount} theme0={theme0Mappings}");
                return;
            }

            var existingBootstrap = Object.FindFirstObjectByType<RenderPresentationCatalogRuntimeBootstrap>();
            if (existingBootstrap != null)
            {
                if (existingBootstrap.CatalogDefinition == null && catalog != null)
                {
                    existingBootstrap.CatalogDefinition = catalog;
                }
            }

            if (catalog == null)
            {
                UnityDebug.LogWarning($"[Space4XAutoRenderCatalogBootstrap] catalog_ready=0 missing_resource='{CatalogResourceName}'");
                return;
            }

            var bootstrapGo = new GameObject(BootstrapObjectName);
            Object.DontDestroyOnLoad(bootstrapGo);
            var runtimeBootstrap = bootstrapGo.AddComponent<RenderPresentationCatalogRuntimeBootstrap>();
            runtimeBootstrap.CatalogDefinition = catalog;
            UnityDebug.Log($"[Space4XAutoRenderCatalogBootstrap] catalog_ready=1 variants={variantCount} theme0={theme0Mappings}");
        }

        private static bool HasCatalogSingleton()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return false;
            }

            using var query = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<RenderPresentationCatalog>());
            return query.CalculateEntityCount() > 0;
        }

        private static int GetTheme0MappingCount(RenderPresentationCatalogDefinition catalog)
        {
            if (catalog == null || catalog.Themes == null)
            {
                return 0;
            }

            for (int i = 0; i < catalog.Themes.Length; i++)
            {
                var theme = catalog.Themes[i];
                if (theme.ThemeId == 0)
                {
                    return theme.SemanticVariants?.Length ?? 0;
                }
            }

            return 0;
        }
    }
}
