using Space4X.Authoring;
using UnityEditor;
using UnityEngine;

namespace Space4X.Editor.Generators
{
    public class ProductGenerator : BasePrefabGenerator
    {
        public override bool Generate(PrefabMakerOptions options, PrefabMaker.GenerationResult result)
        {
            var catalog = LoadCatalog(options.CatalogPath);
            if (catalog == null || catalog.products == null)
            {
                result.Errors.Add("Failed to load product catalog");
                return false;
            }

            EnsureDirectory($"{PrefabBasePath}/Products");

            bool anyChanged = false;
            foreach (var productData in catalog.products)
            {
                // Apply selected IDs filter if specified
                if (options.SelectedIds != null && options.SelectedIds.Count > 0 && !options.SelectedIds.Contains(productData.id))
                {
                    continue;
                }
                
                // Apply selected category filter if specified
                if (options.SelectedCategory.HasValue && options.SelectedCategory.Value != PrefabTemplateCategory.Products)
                {
                    continue;
                }
                
                if (string.IsNullOrWhiteSpace(productData.id))
                {
                    result.Warnings.Add("Skipping product with empty ID");
                    continue;
                }

                var prefabPath = $"{PrefabBasePath}/Products/{productData.id}.prefab";
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

                var productObj = LoadOrCreatePrefab(prefabPath, productData.id, out bool isNew);

                // Add ProductIdAuthoring
                var productId = productObj.GetComponent<ProductIdAuthoring>();
                if (productId == null) productId = productObj.AddComponent<ProductIdAuthoring>();
                productId.productId = productData.id;
                productId.requiredTechTier = productData.requiredTechTier;

                // Add style tokens
                AddStyleTokens(productObj, productData.defaultPalette, productData.defaultRoughness, productData.defaultPattern);

                // Add placeholder visual
                if (options.PlaceholdersOnly)
                {
                    PlaceholderPrefabUtility.AddPlaceholderVisual(productObj, PrefabType.Product);
                }

                SavePrefab(productObj, prefabPath, isNew, result);
                anyChanged = true;
            }

            return anyChanged;
        }

        public override void Validate(PrefabMaker.ValidationReport report)
        {
            var catalog = LoadCatalog("Assets/Data/Catalogs");
            if (catalog?.products == null) return;

            foreach (var productData in catalog.products)
            {
                var prefabPath = $"{PrefabBasePath}/Products/{productData.id}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

                if (prefab == null)
                {
                    report.Issues.Add(new PrefabMaker.ValidationIssue
                    {
                        Severity = PrefabMaker.ValidationSeverity.Warning,
                        Message = $"Product '{productData.id}' has no prefab",
                        PrefabPath = prefabPath
                    });
                }
            }
        }

        private ProductCatalogAuthoring LoadCatalog(string catalogPath)
        {
            var prefabPath = $"{catalogPath}/ProductCatalog.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            return prefab?.GetComponent<ProductCatalogAuthoring>();
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

