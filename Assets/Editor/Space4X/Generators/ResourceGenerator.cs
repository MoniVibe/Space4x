using Space4X.Authoring;
using UnityEditor;
using UnityEngine;

namespace Space4X.Editor.Generators
{
    public class ResourceGenerator : BasePrefabGenerator
    {
        public override bool Generate(PrefabMakerOptions options, PrefabMaker.GenerationResult result)
        {
            var catalog = LoadCatalog(options.CatalogPath);
            if (catalog == null || catalog.resources == null)
            {
                result.Errors.Add("Failed to load resource catalog");
                return false;
            }

            EnsureDirectory($"{PrefabBasePath}/Resources");

            bool anyChanged = false;
            foreach (var resourceData in catalog.resources)
            {
                // Apply selected IDs filter if specified
                if (options.SelectedIds != null && options.SelectedIds.Count > 0 && !options.SelectedIds.Contains(resourceData.id))
                {
                    continue;
                }
                
                // Apply selected category filter if specified
                if (options.SelectedCategory.HasValue && options.SelectedCategory.Value != PrefabTemplateCategory.Resources)
                {
                    continue;
                }
                
                if (string.IsNullOrWhiteSpace(resourceData.id))
                {
                    result.Warnings.Add("Skipping resource with empty ID");
                    continue;
                }

                var prefabPath = $"{PrefabBasePath}/Resources/{resourceData.id}.prefab";
                var existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

                if (existingPrefab != null)
                {
                    result.SkippedCount++;
                    continue;
                }

                if (options.DryRun)
                {
                    result.CreatedCount++;
                    anyChanged = true;
                    continue;
                }

                var resourceObj = LoadOrCreatePrefab(prefabPath, resourceData.id, out bool isNew);

                // Add ResourceIdAuthoring
                var resourceId = resourceObj.GetComponent<ResourceIdAuthoring>();
                if (resourceId == null) resourceId = resourceObj.AddComponent<ResourceIdAuthoring>();
                resourceId.resourceId = resourceData.id;

                // Add style tokens
                AddStyleTokens(resourceObj, resourceData.defaultPalette, resourceData.defaultRoughness, resourceData.defaultPattern);

                // Add placeholder visual
                if (options.PlaceholdersOnly)
                {
                    PlaceholderPrefabUtility.AddPlaceholderVisual(resourceObj, PrefabType.Resource);
                }

                SavePrefab(resourceObj, prefabPath, isNew, result);
                anyChanged = true;
            }

            return anyChanged;
        }

        public override void Validate(PrefabMaker.ValidationReport report)
        {
            var catalog = LoadCatalog("Assets/Data/Catalogs");
            if (catalog?.resources == null) return;

            foreach (var resourceData in catalog.resources)
            {
                var prefabPath = $"{PrefabBasePath}/Resources/{resourceData.id}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

                if (prefab == null)
                {
                    report.Issues.Add(new PrefabMaker.ValidationIssue
                    {
                        Severity = PrefabMaker.ValidationSeverity.Warning,
                        Message = $"Resource '{resourceData.id}' has no prefab",
                        PrefabPath = prefabPath
                    });
                }
            }
        }

        private ResourceCatalogAuthoring LoadCatalog(string catalogPath)
        {
            var prefabPath = $"{catalogPath}/ResourceCatalog.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            return prefab?.GetComponent<ResourceCatalogAuthoring>();
        }

        private void AddStyleTokens(GameObject obj, byte palette, byte roughness, byte pattern)
        {
            if (palette == 0 && roughness == 128 && pattern == 0) return;

            var styleTokens = obj.GetComponent<StyleTokensAuthoring>();
            if (styleTokens == null) styleTokens = obj.AddComponent<StyleTokensAuthoring>();
            styleTokens.palette = palette;
            styleTokens.roughness = roughness;
            styleTokens.pattern = pattern;
        }
    }
}

