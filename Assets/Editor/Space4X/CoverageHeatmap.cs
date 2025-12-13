using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Space4X.Authoring;
using Space4X.EditorUtilities;
using Space4X.Registry;
using Space4X.Presentation.Config;
using UnityEditor;
using UnityEngine;

namespace Space4X.Editor
{
    /// <summary>
    /// Coverage heatmap reporting - tracks % of catalog IDs with generated prefabs/bindings.
    /// </summary>
    public static class CoverageHeatmap
    {
        [Serializable]
        public class CoverageReport
        {
            public string Version = "1.0";
            public string GeneratedAt;
            public Dictionary<string, CategoryCoverage> Categories = new Dictionary<string, CategoryCoverage>();
            public float OverallCoverage;
        }

        [Serializable]
        public class CategoryCoverage
        {
            public int CatalogCount;
            public int PrefabCount;
            public int BindingCount;
            public float PrefabCoverage; // %
            public float BindingCoverage; // %
            public List<string> MissingPrefabs = new List<string>();
            public List<string> MissingBindings = new List<string>();
        }

        public static CoverageReport GenerateReport(string catalogPath, string prefabBasePath = "Assets/Prefabs/Space4X")
        {
            var report = new CoverageReport
            {
                GeneratedAt = DateTime.UtcNow.ToString("O")
            };

            // Check each catalog type
            CheckCatalogCoverage<HullCatalogAuthoring>("Hull", catalogPath, prefabBasePath, new[] { "Hulls", "CapitalShips", "Carriers", "Stations" }, report);
            CheckCatalogCoverage<ModuleCatalogAuthoring>("Module", catalogPath, prefabBasePath, new[] { "Modules" }, report);
            CheckCatalogCoverage<StationCatalogAuthoring>("Station", catalogPath, prefabBasePath, new[] { "Stations" }, report);
            CheckCatalogCoverage<ResourceCatalogAuthoring>("Resource", catalogPath, prefabBasePath, new[] { "Resources" }, report);
            CheckCatalogCoverage<ProductCatalogAuthoring>("Product", catalogPath, prefabBasePath, new[] { "Products" }, report);
            CheckCatalogCoverage<AggregateCatalogAuthoring>("Aggregate", catalogPath, prefabBasePath, new[] { "Aggregates" }, report);
            CheckCatalogCoverage<EffectCatalogAuthoring>("Effect", catalogPath, prefabBasePath, new[] { "FX" }, report);
            CheckCatalogCoverage<IndividualCatalogAuthoring>("Individual", catalogPath, prefabBasePath, new[] { "Individuals/Captains", "Individuals/Officers", "Individuals/Crew" }, report);

            // Calculate overall coverage
            var totalCatalog = report.Categories.Values.Sum(c => c.CatalogCount);
            var totalPrefab = report.Categories.Values.Sum(c => c.PrefabCount);
            report.OverallCoverage = totalCatalog > 0 ? (float)totalPrefab / totalCatalog * 100f : 0f;

            return report;
        }

        private static void CheckCatalogCoverage<T>(
            string categoryName,
            string catalogPath,
            string prefabBasePath,
            string[] prefabDirs,
            CoverageReport report) where T : MonoBehaviour
        {
            var coverage = new CategoryCoverage();

            // Load catalog
            var catalogFile = $"{categoryName}Catalog.prefab";
            var catalogPrefabPath = $"{catalogPath}/{catalogFile}";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(catalogPrefabPath);
            var catalog = prefab?.GetComponent<T>();

            if (catalog == null)
            {
                report.Categories[categoryName] = coverage;
                return;
            }

            // Get catalog IDs
            var catalogIds = GetCatalogIds(catalog);
            coverage.CatalogCount = catalogIds.Count;

            // Check prefab coverage
            var prefabIds = new HashSet<string>();
            foreach (var dir in prefabDirs)
            {
                var fullDir = $"{prefabBasePath}/{dir}";
                if (Directory.Exists(fullDir))
                {
                    var prefabs = Directory.GetFiles(fullDir, "*.prefab", SearchOption.AllDirectories);
                    foreach (var prefabPath in prefabs)
                    {
                        var assetPath = AssetPathUtil.ToAssetRelativePath(prefabPath);
                        var prefabObj = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                        if (prefabObj != null)
                        {
                            var id = GetPrefabId(prefabObj, categoryName);
                            if (!string.IsNullOrEmpty(id))
                            {
                                prefabIds.Add(id);
                            }
                        }
                    }
                }
            }

            coverage.PrefabCount = prefabIds.Count;
            coverage.PrefabCoverage = coverage.CatalogCount > 0 ? (float)coverage.PrefabCount / coverage.CatalogCount * 100f : 0f;
            coverage.MissingPrefabs = catalogIds.Except(prefabIds).ToList();

            // Check binding coverage (load binding asset)
            var bindingPath = "Assets/Space4X/Bindings/Space4XPresentationBinding.asset";
            var binding = AssetDatabase.LoadAssetAtPath<Space4XPresentationBinding>(bindingPath);
            if (binding != null)
            {
                var bindingCategory = GetBindingCategory(categoryName);
                var categoryBindings = binding.GetBindingsForCategory(bindingCategory);
                var bindingIds = categoryBindings.Select(b => b.entityId).Where(id => !string.IsNullOrWhiteSpace(id)).ToHashSet();
                coverage.BindingCount = categoryBindings.Count;
                coverage.BindingCoverage = coverage.CatalogCount > 0 ? (float)coverage.BindingCount / coverage.CatalogCount * 100f : 0f;
                coverage.MissingBindings = catalogIds.Except(bindingIds).ToList();
            }

            report.Categories[categoryName] = coverage;
        }

        private static List<string> GetCatalogIds<T>(T catalog) where T : MonoBehaviour
        {
            var ids = new List<string>();

            if (catalog is HullCatalogAuthoring hullCatalog)
            {
                ids.AddRange(hullCatalog.hulls?.Select(h => h.id) ?? Enumerable.Empty<string>());
            }
            else if (catalog is ModuleCatalogAuthoring moduleCatalog)
            {
                ids.AddRange(moduleCatalog.modules?.Select(m => m.id) ?? Enumerable.Empty<string>());
            }
            else if (catalog is StationCatalogAuthoring stationCatalog)
            {
                ids.AddRange(stationCatalog.stations?.Select(s => s.id) ?? Enumerable.Empty<string>());
            }
            else if (catalog is ResourceCatalogAuthoring resourceCatalog)
            {
                ids.AddRange(resourceCatalog.resources?.Select(r => r.id) ?? Enumerable.Empty<string>());
            }
            else if (catalog is ProductCatalogAuthoring productCatalog)
            {
                ids.AddRange(productCatalog.products?.Select(p => p.id) ?? Enumerable.Empty<string>());
            }
            else if (catalog is AggregateCatalogAuthoring aggregateCatalog)
            {
                ids.AddRange(aggregateCatalog.aggregates?.Select(a => a.id) ?? Enumerable.Empty<string>());
            }
            else if (catalog is EffectCatalogAuthoring effectCatalog)
            {
                ids.AddRange(effectCatalog.effects?.Select(e => e.id) ?? Enumerable.Empty<string>());
            }
            else if (catalog is IndividualCatalogAuthoring individualCatalog)
            {
                ids.AddRange(individualCatalog.individuals?.Select(i => i.id) ?? Enumerable.Empty<string>());
            }

            return ids.Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
        }

        private static string GetPrefabId(GameObject prefab, string categoryName)
        {
            switch (categoryName)
            {
                case "Hull":
                    return prefab.GetComponent<HullIdAuthoring>()?.hullId;
                case "Module":
                    return prefab.GetComponent<ModuleIdAuthoring>()?.moduleId;
                case "Station":
                    return prefab.GetComponent<StationIdAuthoring>()?.stationId;
                case "Resource":
                    return prefab.GetComponent<ResourceIdAuthoring>()?.resourceId;
                case "Product":
                    return prefab.GetComponent<ProductIdAuthoring>()?.productId;
                case "Aggregate":
                    return prefab.GetComponent<AggregateIdAuthoring>()?.aggregateId;
                case "Effect":
                    return prefab.GetComponent<EffectIdAuthoring>()?.effectId;
                case "Individual":
                    // Individuals use name or stats component
                    return prefab.name;
                default:
                    return null;
            }
        }

        private static Space4XPresentationBinding.EntityCategory GetBindingCategory(string categoryName)
        {
            switch (categoryName)
            {
                case "Hull": return Space4XPresentationBinding.EntityCategory.Hull;
                case "Module": return Space4XPresentationBinding.EntityCategory.Module;
                case "Station": return Space4XPresentationBinding.EntityCategory.Station;
                case "Resource": return Space4XPresentationBinding.EntityCategory.Resource;
                case "Product": return Space4XPresentationBinding.EntityCategory.Product;
                case "Aggregate": return Space4XPresentationBinding.EntityCategory.Aggregate;
                case "Effect": return Space4XPresentationBinding.EntityCategory.Effect;
                case "Individual": return Space4XPresentationBinding.EntityCategory.Individual;
                default: return Space4XPresentationBinding.EntityCategory.Hull;
            }
        }

        public static void SaveReport(CoverageReport report, string catalogPath)
        {
            var reportDir = Path.Combine(Path.GetDirectoryName(catalogPath), "Reports");
            if (!Directory.Exists(reportDir))
            {
                Directory.CreateDirectory(reportDir);
            }
            var reportPath = Path.Combine(reportDir, "coverage_heatmap.json");
            var json = JsonConvert.SerializeObject(report, Formatting.Indented);
            File.WriteAllText(reportPath, json);
            AssetDatabase.ImportAsset(reportPath);
        }

        public static void PrintReport(CoverageReport report)
        {
            Debug.Log("=== Prefab Maker Coverage Heatmap ===");
            Debug.Log($"Overall Coverage: {report.OverallCoverage:F1}%");
            Debug.Log("");

            foreach (var kvp in report.Categories.OrderBy(c => c.Key))
            {
                var cat = kvp.Value;
                Debug.Log($"{kvp.Key}:");
                Debug.Log($"  Catalog Entries: {cat.CatalogCount}");
                Debug.Log($"  Prefab Coverage: {cat.PrefabCoverage:F1}% ({cat.PrefabCount}/{cat.CatalogCount})");
                Debug.Log($"  Binding Coverage: {cat.BindingCoverage:F1}% ({cat.BindingCount}/{cat.CatalogCount})");
                if (cat.MissingPrefabs.Count > 0)
                {
                    Debug.LogWarning($"  Missing Prefabs: {string.Join(", ", cat.MissingPrefabs.Take(10))}" + 
                                   (cat.MissingPrefabs.Count > 10 ? $" ... ({cat.MissingPrefabs.Count - 10} more)" : ""));
                }
            }
        }
    }
}
