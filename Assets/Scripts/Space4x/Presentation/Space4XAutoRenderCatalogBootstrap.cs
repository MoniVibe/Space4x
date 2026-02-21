using System;
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
        private static bool _logged;

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
                LogOnce(catalogReady: true, variantCount, theme0Mappings);
                return;
            }

            var existingBootstrap = Object.FindFirstObjectByType<RenderPresentationCatalogRuntimeBootstrap>();
            if (existingBootstrap != null)
            {
                if (HasCatalogSingleton())
                {
                    LogOnce(catalogReady: true, variantCount, theme0Mappings);
                    return;
                }

                var selectedCatalog = existingBootstrap.CatalogDefinition ?? catalog;
                if (selectedCatalog == null)
                {
                    LogOnce(catalogReady: false, variantCount, theme0Mappings);
                    return;
                }

                // Runtime bootstrap only initializes in Awake. If we arrive after Awake without a catalog
                // singleton, recreate the bootstrap with the catalog preassigned so Awake can build it.
                var replacementName = existingBootstrap.gameObject != null
                    ? existingBootstrap.gameObject.name
                    : BootstrapObjectName;

                if (existingBootstrap.gameObject != null)
                {
                    Object.Destroy(existingBootstrap.gameObject);
                }

                CreateRuntimeBootstrap(replacementName, selectedCatalog, variantCount, theme0Mappings);
                return;
            }

            if (catalog == null)
            {
                LogOnce(catalogReady: false, variantCount, theme0Mappings);
                return;
            }

            CreateRuntimeBootstrap(BootstrapObjectName, catalog, variantCount, theme0Mappings);
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

        private static void LogOnce(bool catalogReady, int variantCount, int theme0Mappings)
        {
            if (_logged)
            {
                return;
            }

            _logged = true;
            if (catalogReady)
            {
                UnityDebug.Log($"[Space4XAutoRenderCatalogBootstrap] catalog_ready=1 variants={variantCount} theme0={theme0Mappings}");
            }
            else
            {
                UnityDebug.LogWarning($"[Space4XAutoRenderCatalogBootstrap] catalog_ready=0 missing_resource='{CatalogResourceName}'");
            }
        }

        private static void CreateRuntimeBootstrap(string name, RenderPresentationCatalogDefinition catalog, int variantCount, int theme0Mappings)
        {
            if (catalog == null)
            {
                LogOnce(catalogReady: false, variantCount, theme0Mappings);
                return;
            }

            var bootstrapGo = new GameObject(string.IsNullOrWhiteSpace(name) ? BootstrapObjectName : name);
            bootstrapGo.SetActive(false);
            Object.DontDestroyOnLoad(bootstrapGo);

            var runtimeBootstrap = bootstrapGo.AddComponent<RenderPresentationCatalogRuntimeBootstrap>();
            runtimeBootstrap.CatalogDefinition = catalog;
            bootstrapGo.SetActive(true);

            LogOnce(catalogReady: HasCatalogSingleton(), variantCount, theme0Mappings);
        }
    }
}
