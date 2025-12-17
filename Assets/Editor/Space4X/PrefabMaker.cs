using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Space4X.Authoring;
using Space4X.Editor.Generators;
using Space4X.Presentation.Config;
using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityDebug = UnityEngine.Debug;

namespace Space4X.Editor
{
    using Debug = UnityEngine.Debug;

    public static class PrefabMaker
    {
        private const string PrefabBasePath = "Assets/Prefabs/Space4X";
        private const string BindingPath = "Assets/Space4X/Bindings/Space4XPresentationBinding.asset";

        public class GenerationResult
        {
            public int CreatedCount { get; set; }
            public int UpdatedCount { get; set; }
            public int SkippedCount { get; set; }
            public List<string> Warnings { get; set; } = new List<string>();
            public List<string> Errors { get; set; } = new List<string>();
        }

        public class ValidationReport
        {
            public int TotalIssues => Issues.Count;
            public List<ValidationIssue> Issues { get; set; } = new List<ValidationIssue>();
        }

        public class ValidationIssue
        {
            public ValidationSeverity Severity { get; set; }
            public string Message { get; set; }
            public string PrefabPath { get; set; }
        }

        public enum ValidationSeverity
        {
            Warning,
            Error
        }

        public static GenerationResult GenerateAll(string catalogPath, bool placeholdersOnly, bool overwriteMissingSockets, bool dryRun, HullCategory hullCategoryFilter = HullCategory.Other)
        {
            var options = new PrefabMakerOptions
            {
                CatalogPath = catalogPath,
                PlaceholdersOnly = placeholdersOnly,
                OverwriteMissingSockets = overwriteMissingSockets,
                DryRun = dryRun,
                HullCategoryFilter = hullCategoryFilter
            };

            return GenerateAll(options);
        }

        /// <summary>
        /// Generate prefabs for specific template IDs.
        /// </summary>
        public static GenerationResult GenerateSelected(string catalogPath, List<string> selectedIds, PrefabTemplateCategory? category = null, bool placeholdersOnly = true, bool overwriteMissingSockets = false, bool dryRun = false)
        {
            var options = new PrefabMakerOptions
            {
                CatalogPath = catalogPath,
                PlaceholdersOnly = placeholdersOnly,
                OverwriteMissingSockets = overwriteMissingSockets,
                DryRun = dryRun,
                SelectedIds = selectedIds ?? new List<string>(),
                SelectedCategory = category
            };
            
            return GenerateAll(options);
        }
        
        public static GenerationResult GenerateAll(PrefabMakerOptions options)
        {
            var result = new GenerationResult();
            PrefabMakerHashReport hashReport = null;

            try
            {
                // Load previous hash report if in dry-run mode for comparison
                if (options.DryRun)
                {
                    hashReport = PrefabMakerHashReport.Load(options.CatalogPath);
                }

                // Ensure directories exist
                EnsureDirectories();

                // Create generators
                var generators = new IPrefabGenerator[]
                {
                    new Generators.HullGenerator(),
                    new Generators.ModuleGenerator(),
                    new Generators.StationGenerator(),
                    new Generators.ResourceGenerator(),
                    new Generators.ProductGenerator(),
                    new Generators.AggregateGenerator(),
                    new Generators.EffectGenerator(),
                    new Generators.IndividualEntityGenerator(),
                    new Generators.WeaponPresentationGenerator(),
                    new Generators.ProjectilePresentationGenerator(),
                    new Generators.TurretPresentationGenerator()
                };

                // Run generators (they will check SelectedIds/SelectedCategory if provided)
                foreach (var generator in generators)
                {
                    generator.Generate(options, result);
                }

                // Generate binding blob (both Minimal and Fancy sets)
                if (!options.DryRun)
                {
                    GenerateBindingBlob(result, BindingSet.Minimal);
                    GenerateBindingBlob(result, BindingSet.Fancy);
                }

                // Generate hash report
                var newHashReport = GenerateHashReport(options, result);
                if (options.DryRun && hashReport != null)
                {
                    var comparison = newHashReport.Compare(hashReport);
                    if (comparison.HasChanges)
                    {
                        result.Warnings.Add("DRY-RUN: Hash comparison detected changes (idempotency check failed)");
                        result.Warnings.Add($"Changed prefabs: {comparison.ChangedPrefabs.Count}");
                        result.Warnings.Add($"New prefabs: {comparison.NewPrefabs.Count}");
                        result.Warnings.Add($"Removed prefabs: {comparison.RemovedPrefabs.Count}");
                    }
                    else
                    {
                        result.Warnings.Add("DRY-RUN: Hash comparison passed (idempotent)");
                    }
                }

                if (!options.DryRun)
                {
                    newHashReport.Save(options.CatalogPath);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Exception during generation: {ex.Message}");
                UnityDebug.LogError($"PrefabMaker exception: {ex}\n{ex.StackTrace}");
            }

            return result;
        }

        private static PrefabMakerHashReport GenerateHashReport(PrefabMakerOptions options, GenerationResult result)
        {
            var report = new PrefabMakerHashReport();

            // Hash all prefabs
            var prefabDirs = new[] { "Hulls", "CapitalShips", "Carriers", "Stations", "Modules", "Resources", "Products", "Aggregates", "FX", "Individuals" };
            foreach (var dir in prefabDirs)
            {
                var fullDir = $"{PrefabBasePath}/{dir}";
                if (Directory.Exists(fullDir))
                {
                    var prefabs = Directory.GetFiles(fullDir, "*.prefab", SearchOption.AllDirectories);
                    foreach (var prefabPath in prefabs)
                    {
                        var relativePath = prefabPath.Replace('\\', '/');
                        var hash = PrefabMakerHashReport.ComputeFileHash(prefabPath);
                        if (hash != null)
                        {
                            report.PrefabHashes[relativePath] = hash;
                        }
                    }
                }
            }

            // Hash bindings
            var bindingAssetPath = BindingPath.Replace(".json", ".asset");
            var bindingJsonPath = BindingPath.Replace(".asset", ".json");
            if (File.Exists(bindingAssetPath))
            {
                report.BindingHashes["asset"] = PrefabMakerHashReport.ComputeFileHash(bindingAssetPath);
            }
            if (File.Exists(bindingJsonPath))
            {
                report.BindingHashes["json"] = PrefabMakerHashReport.ComputeFileHash(bindingJsonPath);
            }

            // Count catalog entries
            report.CatalogCounts = CountCatalogEntries(options.CatalogPath);

            return report;
        }

        public static Dictionary<string, int> CountCatalogEntries(string catalogPath)
        {
            var counts = new Dictionary<string, int>();

            var catalogFiles = new[]
            {
                "HullCatalog.prefab",
                "ModuleCatalog.prefab",
                "StationCatalog.prefab",
                "ResourceCatalog.prefab",
                "ProductCatalog.prefab",
                "AggregateCatalog.prefab",
                "EffectCatalog.prefab",
                "IndividualCatalog.prefab"
            };

            foreach (var catalogFile in catalogFiles)
            {
                var catalogPrefabPath = $"{catalogPath}/{catalogFile}";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(catalogPrefabPath);
                if (prefab != null)
                {
                    var catalogName = catalogFile.Replace("Catalog.prefab", "");
                    int count = 0;

                    // Count entries based on catalog type
                    var hullCatalog = prefab.GetComponent<HullCatalogAuthoring>();
                    if (hullCatalog != null) count = hullCatalog.hulls?.Count ?? 0;

                    var moduleCatalog = prefab.GetComponent<ModuleCatalogAuthoring>();
                    if (moduleCatalog != null) count = moduleCatalog.modules?.Count ?? 0;

                    var stationCatalog = prefab.GetComponent<StationCatalogAuthoring>();
                    if (stationCatalog != null) count = stationCatalog.stations?.Count ?? 0;

                    var resourceCatalog = prefab.GetComponent<ResourceCatalogAuthoring>();
                    if (resourceCatalog != null) count = resourceCatalog.resources?.Count ?? 0;

                    var productCatalog = prefab.GetComponent<ProductCatalogAuthoring>();
                    if (productCatalog != null) count = productCatalog.products?.Count ?? 0;

                    var aggregateCatalog = prefab.GetComponent<AggregateCatalogAuthoring>();
                    if (aggregateCatalog != null) count = aggregateCatalog.aggregates?.Count ?? 0;

                    var effectCatalog = prefab.GetComponent<EffectCatalogAuthoring>();
                    if (effectCatalog != null) count = effectCatalog.effects?.Count ?? 0;

                    var individualCatalog = prefab.GetComponent<IndividualCatalogAuthoring>();
                    if (individualCatalog != null) count = individualCatalog.individuals?.Count ?? 0;

                    counts[catalogName] = count;
                }
            }

            return counts;
        }

        private static ModuleCatalogAuthoring LoadModuleCatalog(string catalogPath)
        {
            var prefabPath = $"{catalogPath}/ModuleCatalog.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                UnityDebug.LogWarning($"Module catalog not found at {prefabPath}");
                return null;
            }

            return prefab.GetComponent<ModuleCatalogAuthoring>();
        }

        private static HullCatalogAuthoring LoadHullCatalog(string catalogPath)
        {
            var prefabPath = $"{catalogPath}/HullCatalog.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                UnityDebug.LogWarning($"Hull catalog not found at {prefabPath}");
                return null;
            }

            return prefab.GetComponent<HullCatalogAuthoring>();
        }

        private static StationCatalogAuthoring LoadStationCatalog(string catalogPath)
        {
            var prefabPath = $"{catalogPath}/StationCatalog.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            return prefab?.GetComponent<StationCatalogAuthoring>();
        }

        private static ResourceCatalogAuthoring LoadResourceCatalog(string catalogPath)
        {
            var prefabPath = $"{catalogPath}/ResourceCatalog.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            return prefab?.GetComponent<ResourceCatalogAuthoring>();
        }

        private static ProductCatalogAuthoring LoadProductCatalog(string catalogPath)
        {
            var prefabPath = $"{catalogPath}/ProductCatalog.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            return prefab?.GetComponent<ProductCatalogAuthoring>();
        }

        private static AggregateCatalogAuthoring LoadAggregateCatalog(string catalogPath)
        {
            var prefabPath = $"{catalogPath}/AggregateCatalog.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            return prefab?.GetComponent<AggregateCatalogAuthoring>();
        }

        private static EffectCatalogAuthoring LoadEffectCatalog(string catalogPath)
        {
            var prefabPath = $"{catalogPath}/EffectCatalog.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            return prefab?.GetComponent<EffectCatalogAuthoring>();
        }

        private static void EnsureDirectories()
        {
            var dirs = new[]
            {
                $"{PrefabBasePath}/Hulls",
                $"{PrefabBasePath}/CapitalShips",
                $"{PrefabBasePath}/Carriers",
                $"{PrefabBasePath}/Stations",
                $"{PrefabBasePath}/Modules",
                $"{PrefabBasePath}/Resources",
                $"{PrefabBasePath}/Products",
                $"{PrefabBasePath}/Aggregates",
                $"{PrefabBasePath}/FX",
                "Assets/Space4X/Bindings"
            };

            foreach (var dir in dirs)
            {
                if (!AssetDatabase.IsValidFolder(dir))
                {
                    var parts = dir.Split('/');
                    var currentPath = parts[0];
                    for (int i = 1; i < parts.Length; i++)
                    {
                        var nextPath = $"{currentPath}/{parts[i]}";
                        if (!AssetDatabase.IsValidFolder(nextPath))
                        {
                            AssetDatabase.CreateFolder(currentPath, parts[i]);
                        }
                        currentPath = nextPath;
                    }
                }
            }
        }

        private static void AddCategoryComponents(GameObject hullObj, HullCategory category, float hangarCapacity)
        {
            // Remove existing category tags
            var existingCapitalShip = hullObj.GetComponent<CapitalShipAuthoring>();
            var existingCarrier = hullObj.GetComponent<CarrierAuthoring>();
            
            if (existingCapitalShip != null) Object.DestroyImmediate(existingCapitalShip);
            if (existingCarrier != null) Object.DestroyImmediate(existingCarrier);

            // Add appropriate category tag
            switch (category)
            {
                case HullCategory.CapitalShip:
                    hullObj.AddComponent<CapitalShipAuthoring>();
                    break;
                case HullCategory.Carrier:
                    hullObj.AddComponent<CarrierAuthoring>();
                    break;
            }

            // Add hangar capacity if specified
            if (hangarCapacity > 0f)
            {
                var hangarCap = hullObj.GetComponent<HangarCapacityAuthoring>();
                if (hangarCap == null)
                {
                    hangarCap = hullObj.AddComponent<HangarCapacityAuthoring>();
                }
                hangarCap.capacity = hangarCapacity;
            }
            else
            {
                // Remove hangar capacity if it exists but shouldn't
                var hangarCap = hullObj.GetComponent<HangarCapacityAuthoring>();
                if (hangarCap != null)
                {
                    Object.DestroyImmediate(hangarCap);
                }
            }
        }

        private static void GenerateHullPrefabs(HullCatalogAuthoring catalog, bool placeholdersOnly, bool overwriteMissingSockets, bool dryRun, GenerationResult result)
        {
            if (catalog == null || catalog.hulls == null) return;

            foreach (var hullData in catalog.hulls)
            {
                if (string.IsNullOrWhiteSpace(hullData.id))
                {
                    result.Warnings.Add($"Skipping hull with empty ID");
                    continue;
                }

                // Determine category-specific folder
                var categoryFolder = GetCategoryFolder(hullData.category);
                var prefabPath = $"{PrefabBasePath}/{categoryFolder}/{hullData.id}.prefab";
                var existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

                if (existingPrefab != null && !overwriteMissingSockets)
                {
                    result.SkippedCount++;
                    continue;
                }

                if (dryRun)
                {
                    result.CreatedCount++;
                    continue;
                }

                GameObject hullObj;
                if (existingPrefab != null)
                {
                    hullObj = PrefabUtility.LoadPrefabContents(prefabPath);
                }
                else
                {
                    hullObj = new GameObject(hullData.id);
                }

                // Add/update HullIdAuthoring
                var hullId = hullObj.GetComponent<HullIdAuthoring>();
                if (hullId == null)
                {
                    hullId = hullObj.AddComponent<HullIdAuthoring>();
                }
                hullId.hullId = hullData.id;

                // Add/update HullSocketAuthoring
                var socketAuthoring = hullObj.GetComponent<HullSocketAuthoring>();
                if (socketAuthoring == null)
                {
                    socketAuthoring = hullObj.AddComponent<HullSocketAuthoring>();
                }
                socketAuthoring.autoCreateFromCatalog = true;

                // Add category-specific components
                AddCategoryComponents(hullObj, hullData.category, hullData.hangarCapacity);

                // Add style tokens if specified
                if (hullData.defaultPalette != 0 || hullData.defaultRoughness != 128 || hullData.defaultPattern != 0)
                {
                    var styleTokens = hullObj.GetComponent<StyleTokensAuthoring>();
                    if (styleTokens == null)
                    {
                        styleTokens = hullObj.AddComponent<StyleTokensAuthoring>();
                    }
                    styleTokens.palette = hullData.defaultPalette;
                    styleTokens.roughness = hullData.defaultRoughness;
                    styleTokens.pattern = hullData.defaultPattern;
                }

                // Create socket child transforms
                CreateHullSockets(hullObj, hullData.slots, overwriteMissingSockets);

                // Add placeholder visual
                if (placeholdersOnly)
                {
                    PlaceholderPrefabUtility.AddPlaceholderVisual(hullObj, PrefabType.Hull);
                }

                // Save prefab
                if (existingPrefab != null)
                {
                    PrefabUtility.SaveAsPrefabAsset(hullObj, prefabPath);
                    PrefabUtility.UnloadPrefabContents(hullObj);
                    result.UpdatedCount++;
                }
                else
                {
                    PrefabUtility.SaveAsPrefabAsset(hullObj, prefabPath);
                    Object.DestroyImmediate(hullObj);
                    result.CreatedCount++;
                }
            }
        }

        private static void GenerateModulePrefabs(ModuleCatalogAuthoring catalog, bool placeholdersOnly, bool dryRun, GenerationResult result)
        {
            if (catalog == null || catalog.modules == null) return;

            foreach (var moduleData in catalog.modules)
            {
                if (string.IsNullOrWhiteSpace(moduleData.id))
                {
                    result.Warnings.Add($"Skipping module with empty ID");
                    continue;
                }

                var prefabPath = $"{PrefabBasePath}/Modules/{moduleData.id}.prefab";
                var existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

                if (existingPrefab != null)
                {
                    result.SkippedCount++;
                    continue;
                }

                if (dryRun)
                {
                    result.CreatedCount++;
                    continue;
                }

                var moduleObj = new GameObject(moduleData.id);

                // Add ModuleIdAuthoring
                var moduleId = moduleObj.AddComponent<ModuleIdAuthoring>();
                moduleId.moduleId = moduleData.id;

                // Add MountRequirementAuthoring
                var mountReq = moduleObj.AddComponent<MountRequirementAuthoring>();
                mountReq.mountType = moduleData.requiredMount;
                mountReq.mountSize = moduleData.requiredSize;

                // Add module function if specified
                if (moduleData.function != ModuleFunction.None)
                {
                    var moduleFunction = moduleObj.AddComponent<ModuleFunctionAuthoring>();
                    moduleFunction.function = moduleData.function;
                    moduleFunction.capacity = moduleData.functionCapacity;
                    moduleFunction.description = moduleData.functionDescription;
                }

                // Add placeholder visual
                if (placeholdersOnly)
                {
                    PlaceholderPrefabUtility.AddPlaceholderVisual(moduleObj, PrefabType.Module);
                }

                // Save prefab
                PrefabUtility.SaveAsPrefabAsset(moduleObj, prefabPath);
                Object.DestroyImmediate(moduleObj);
                result.CreatedCount++;
            }
        }

        private static void CreateHullSockets(GameObject hullObj, List<HullCatalogAuthoring.HullSlotData> slots, bool overwriteMissing)
        {
            if (slots == null || slots.Count == 0) return;

            // Count sockets by type/size to generate proper indices
            var socketIndices = new Dictionary<string, int>();
            var expectedNames = new HashSet<string>();

            // First pass: create socket transforms for each slot
            foreach (var slot in slots)
            {
                var key = $"{slot.type}_{slot.size}";
                if (!socketIndices.ContainsKey(key))
                {
                    socketIndices[key] = 0;
                }
                socketIndices[key]++;

                var socketName = $"Socket_{slot.type}_{slot.size}_{socketIndices[key]:D2}";
                expectedNames.Add(socketName);

                var existingSocket = hullObj.transform.Find(socketName);
                
                if (existingSocket == null)
                {
                    var socketObj = new GameObject(socketName);
                    socketObj.transform.SetParent(hullObj.transform);
                    socketObj.transform.localPosition = Vector3.zero;
                    socketObj.transform.localRotation = Quaternion.identity;
                }
                else if (overwriteMissing)
                {
                    // Reset position/rotation if overwriting
                    existingSocket.localPosition = Vector3.zero;
                    existingSocket.localRotation = Quaternion.identity;
                }
            }

            // Remove orphaned sockets that don't match catalog
            if (overwriteMissing)
            {
                var socketsToRemove = new List<Transform>();
                for (int i = 0; i < hullObj.transform.childCount; i++)
                {
                    var child = hullObj.transform.GetChild(i);
                    if (child.name.StartsWith("Socket_") && !expectedNames.Contains(child.name))
                    {
                        socketsToRemove.Add(child);
                    }
                }

                foreach (var socket in socketsToRemove)
                {
                    Object.DestroyImmediate(socket.gameObject);
                }
            }
        }


        private static string GetCategoryFolder(HullCategory category)
        {
            switch (category)
            {
                case HullCategory.CapitalShip:
                    return "CapitalShips";
                case HullCategory.Carrier:
                    return "Carriers";
                case HullCategory.Station:
                    return "Stations";
                default:
                    return "Hulls"; // Default fallback
            }
        }

        public enum BindingSet
        {
            Minimal,  // Essential bindings only
            Fancy      // Full bindings with metadata
        }

        private static void GenerateBindingBlob(GenerationResult result, BindingSet bindingSet = BindingSet.Minimal)
        {
            try
            {
                // Ensure binding directory exists
                var bindingDir = Path.GetDirectoryName(BindingPath.Replace('/', Path.DirectorySeparatorChar));
                if (!Directory.Exists(bindingDir))
                {
                    Directory.CreateDirectory(bindingDir);
                }

                // Create binding asset with set suffix
                var bindingSuffix = bindingSet == BindingSet.Minimal ? "_Minimal" : "_Fancy";
                var bindingAssetPath = BindingPath.Replace(".asset", $"{bindingSuffix}.asset");
                var bindingAsset = AssetDatabase.LoadAssetAtPath<Space4XPresentationBinding>(bindingAssetPath);
                if (bindingAsset == null)
                {
                    bindingAsset = ScriptableObject.CreateInstance<Space4XPresentationBinding>();
                    AssetDatabase.CreateAsset(bindingAsset, bindingAssetPath);
                }
                bindingAsset.Clear();

                // Collect all prefab references
                var hullPrefabs = new Dictionary<string, string>();
                var modulePrefabs = new Dictionary<string, string>();
                var stationPrefabs = new Dictionary<string, string>();
                var resourcePrefabs = new Dictionary<string, string>();
                var productPrefabs = new Dictionary<string, string>();
                var aggregatePrefabs = new Dictionary<string, string>();
                var fxPrefabs = new Dictionary<string, string>();
                var individualPrefabs = new Dictionary<string, string>();

                // Scan hull prefabs (all category folders)
                var hullCategoryDirs = new[] { "Hulls", "CapitalShips", "Carriers", "Stations" };
                foreach (var categoryDir in hullCategoryDirs)
                {
                    var hullDir = $"{PrefabBasePath}/{categoryDir}";
                    if (Directory.Exists(hullDir))
                    {
                        var hullFiles = Directory.GetFiles(hullDir, "*.prefab", SearchOption.TopDirectoryOnly);
                        foreach (var file in hullFiles)
                        {
                            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(file.Replace('\\', '/'));
                            if (prefab != null)
                            {
                                var hullId = prefab.GetComponent<HullIdAuthoring>();
                                if (hullId != null && !string.IsNullOrWhiteSpace(hullId.hullId))
                                {
                                    hullPrefabs[hullId.hullId] = file.Replace('\\', '/');
                                }
                            }
                        }
                    }
                }

                // Scan module prefabs (with quality metadata)
                ScanPrefabsInCategory("Modules", modulePrefabs, (prefab) =>
                {
                    var moduleId = prefab.GetComponent<ModuleIdAuthoring>()?.moduleId;
                    if (!string.IsNullOrWhiteSpace(moduleId))
                    {
                        // Include quality metadata in binding
                        var quality = prefab.GetComponent<ModuleQualityAuthoring>();
                        var rarity = prefab.GetComponent<ModuleRarityAuthoring>();
                        var tier = prefab.GetComponent<ModuleTierAuthoring>();
                        var manufacturer = prefab.GetComponent<ModuleManufacturerAuthoring>();
                        // For now, just return the ID - full metadata can be added to binding JSON structure later
                    }
                    return moduleId;
                });
                
                // Scan station prefabs
                ScanPrefabsInCategory("Stations", stationPrefabs, (prefab) => prefab.GetComponent<StationIdAuthoring>()?.stationId);
                
                // Scan resource prefabs
                ScanPrefabsInCategory("Resources", resourcePrefabs, (prefab) => prefab.GetComponent<ResourceIdAuthoring>()?.resourceId);
                
                // Scan product prefabs
                ScanPrefabsInCategory("Products", productPrefabs, (prefab) => prefab.GetComponent<ProductIdAuthoring>()?.productId);
                
                // Scan aggregate prefabs
                ScanPrefabsInCategory("Aggregates", aggregatePrefabs, (prefab) => prefab.GetComponent<AggregateIdAuthoring>()?.aggregateId);
                
                // Scan FX prefabs
                ScanPrefabsInCategory("FX", fxPrefabs, (prefab) => prefab.GetComponent<EffectIdAuthoring>()?.effectId);

                // Scan individual prefabs (Captains, Officers, Crew)
                ScanPrefabsInCategory("Individuals/Captains", individualPrefabs, (prefab) => 
                {
                    // Try to get individual ID from various components
                    var individualId = prefab.GetComponent<IndividualStatsAuthoring>()?.name;
                    return string.IsNullOrWhiteSpace(individualId) ? null : individualId;
                });
                ScanPrefabsInCategory("Individuals/Officers", individualPrefabs, (prefab) => 
                {
                    var individualId = prefab.GetComponent<IndividualStatsAuthoring>()?.name;
                    return string.IsNullOrWhiteSpace(individualId) ? null : individualId;
                });
                ScanPrefabsInCategory("Individuals/Crew", individualPrefabs, (prefab) => 
                {
                    var individualId = prefab.GetComponent<IndividualStatsAuthoring>()?.name;
                    return string.IsNullOrWhiteSpace(individualId) ? null : individualId;
                });

                // Populate ScriptableObject binding
                foreach (var kvp in hullPrefabs)
                {
                    bindingAsset.SetBinding(kvp.Key, kvp.Value, Space4XPresentationBinding.EntityCategory.Hull);
                }
                foreach (var kvp in modulePrefabs)
                {
                    bindingAsset.SetBinding(kvp.Key, kvp.Value, Space4XPresentationBinding.EntityCategory.Module);
                }
                foreach (var kvp in stationPrefabs)
                {
                    bindingAsset.SetBinding(kvp.Key, kvp.Value, Space4XPresentationBinding.EntityCategory.Station);
                }
                foreach (var kvp in resourcePrefabs)
                {
                    bindingAsset.SetBinding(kvp.Key, kvp.Value, Space4XPresentationBinding.EntityCategory.Resource);
                }
                foreach (var kvp in productPrefabs)
                {
                    bindingAsset.SetBinding(kvp.Key, kvp.Value, Space4XPresentationBinding.EntityCategory.Product);
                }
                foreach (var kvp in aggregatePrefabs)
                {
                    bindingAsset.SetBinding(kvp.Key, kvp.Value, Space4XPresentationBinding.EntityCategory.Aggregate);
                }
                foreach (var kvp in fxPrefabs)
                {
                    bindingAsset.SetBinding(kvp.Key, kvp.Value, Space4XPresentationBinding.EntityCategory.Effect);
                }
                foreach (var kvp in individualPrefabs)
                {
                    bindingAsset.SetBinding(kvp.Key, kvp.Value, Space4XPresentationBinding.EntityCategory.Individual);
                }
                
                // Scan weapon/projectile/turret presentation tokens
                var weaponPrefabs = new Dictionary<string, string>();
                var projectilePrefabs = new Dictionary<string, string>();
                var turretPrefabs = new Dictionary<string, string>();
                
                ScanPrefabsInCategory("Weapons", weaponPrefabs, go =>
                {
                    var weaponId = go.GetComponent<Generators.WeaponIdAuthoring>();
                    return weaponId != null ? weaponId.Id : null;
                });
                
                ScanPrefabsInCategory("Projectiles", projectilePrefabs, go =>
                {
                    var projId = go.GetComponent<Generators.ProjectileIdAuthoring>();
                    return projId != null ? projId.Id : null;
                });
                
                ScanPrefabsInCategory("Turrets", turretPrefabs, go =>
                {
                    var turretId = go.GetComponent<Generators.TurretIdAuthoring>();
                    return turretId != null ? turretId.Id : null;
                });
                
                foreach (var kvp in weaponPrefabs)
                {
                    bindingAsset.SetBinding(kvp.Key, kvp.Value, Space4XPresentationBinding.EntityCategory.Weapon);
                }
                foreach (var kvp in projectilePrefabs)
                {
                    bindingAsset.SetBinding(kvp.Key, kvp.Value, Space4XPresentationBinding.EntityCategory.Projectile);
                }
                foreach (var kvp in turretPrefabs)
                {
                    bindingAsset.SetBinding(kvp.Key, kvp.Value, Space4XPresentationBinding.EntityCategory.Turret);
                }

                // Mark asset as dirty and save
                EditorUtility.SetDirty(bindingAsset);
                AssetDatabase.SaveAssets();

                // Also save as JSON for backwards compatibility and external tooling
                var bindingData = new
                {
                    Version = "1.0",
                    GeneratedAt = DateTime.UtcNow.ToString("O"),
                    BindingSet = bindingSet.ToString(),
                    Hulls = hullPrefabs,
                    Modules = modulePrefabs,
                    Stations = stationPrefabs,
                    Resources = resourcePrefabs,
                    Products = productPrefabs,
                    Aggregates = aggregatePrefabs,
                    FX = fxPrefabs,
                    Individuals = individualPrefabs,
                    Weapons = weaponPrefabs,
                    Projectiles = projectilePrefabs,
                    Turrets = turretPrefabs
                };

                var jsonPath = BindingPath.Replace(".asset", $"{bindingSuffix}.json");
                var json = JsonConvert.SerializeObject(bindingData, Formatting.Indented);
                File.WriteAllText(jsonPath, json);
                AssetDatabase.ImportAsset(jsonPath);

                result.Warnings.Add($"Binding data ({bindingSet}) saved to {bindingAssetPath} (ScriptableObject) and {jsonPath} (JSON)");
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Failed to generate binding blob: {ex.Message}");
            }
        }

        private static void ScanPrefabsInCategory(string categoryDir, Dictionary<string, string> prefabDict, Func<GameObject, string> getId)
        {
            var dir = $"{PrefabBasePath}/{categoryDir}";
            if (!Directory.Exists(dir)) return;

            var files = Directory.GetFiles(dir, "*.prefab", SearchOption.TopDirectoryOnly);
            foreach (var file in files)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(file.Replace('\\', '/'));
                if (prefab != null)
                {
                    var id = getId(prefab);
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        prefabDict[id] = file.Replace('\\', '/');
                    }
                }
            }
        }

        public static ValidationReport ValidateAll()
        {
            var report = new ValidationReport();

            try
            {
                // Create generators and run validation
                var generators = new IPrefabGenerator[]
                {
                    new Generators.HullGenerator(),
                    new Generators.ModuleGenerator(),
                    new Generators.StationGenerator(),
                    new Generators.ResourceGenerator(),
                    new Generators.ProductGenerator(),
                    new Generators.AggregateGenerator(),
                    new Generators.EffectGenerator(),
                    new Generators.IndividualEntityGenerator()
                };

                foreach (var generator in generators)
                {
                    generator.Validate(report);
                }

                // Additional cross-category validations
                ValidateOrphanedPrefabs(report);
                ValidateBindingIntegrity(report);
            }
            catch (Exception ex)
            {
                report.Issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Error,
                    Message = $"Validation exception: {ex.Message}",
                    PrefabPath = ""
                });
            }

            return report;
        }

        private static void ValidateHullSockets(HullCatalogAuthoring catalog, ValidationReport report)
        {
            if (catalog?.hulls == null) return;

            foreach (var hullData in catalog.hulls)
            {
                var categoryFolder = GetCategoryFolder(hullData.category);
                var prefabPath = $"{PrefabBasePath}/{categoryFolder}/{hullData.id}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                
                if (prefab == null)
                {
                    report.Issues.Add(new ValidationIssue
                    {
                        Severity = ValidationSeverity.Warning,
                        Message = $"Hull '{hullData.id}' has no prefab",
                        PrefabPath = prefabPath
                    });
                    continue;
                }

                // Count expected sockets
                var expectedSockets = new Dictionary<string, int>();
                if (hullData.slots != null)
                {
                    foreach (var slot in hullData.slots)
                    {
                        var key = $"{slot.type}_{slot.size}";
                        if (!expectedSockets.ContainsKey(key))
                        {
                            expectedSockets[key] = 0;
                        }
                        expectedSockets[key]++;
                    }
                }

                // Count actual sockets
                var actualSockets = new Dictionary<string, int>();
                for (int i = 0; i < prefab.transform.childCount; i++)
                {
                    var child = prefab.transform.GetChild(i);
                    if (child.name.StartsWith("Socket_"))
                    {
                        var parts = child.name.Split('_');
                        if (parts.Length >= 3)
                        {
                            var key = $"{parts[1]}_{parts[2]}";
                            if (!actualSockets.ContainsKey(key))
                            {
                                actualSockets[key] = 0;
                            }
                            actualSockets[key]++;
                        }
                    }
                }

                // Compare
                foreach (var kvp in expectedSockets)
                {
                    if (!actualSockets.ContainsKey(kvp.Key) || actualSockets[kvp.Key] != kvp.Value)
                    {
                        report.Issues.Add(new ValidationIssue
                        {
                            Severity = ValidationSeverity.Error,
                            Message = $"Hull '{hullData.id}' socket mismatch: Expected {kvp.Value}×{kvp.Key}, found {(actualSockets.ContainsKey(kvp.Key) ? actualSockets[kvp.Key] : 0)}",
                            PrefabPath = prefabPath
                        });
                    }
                }
            }
        }

        private static void ValidateModuleMounts(ModuleCatalogAuthoring catalog, ValidationReport report)
        {
            if (catalog?.modules == null) return;

            foreach (var moduleData in catalog.modules)
            {
                var prefabPath = $"{PrefabBasePath}/Modules/{moduleData.id}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                
                if (prefab == null)
                {
                    report.Issues.Add(new ValidationIssue
                    {
                        Severity = ValidationSeverity.Warning,
                        Message = $"Module '{moduleData.id}' has no prefab",
                        PrefabPath = prefabPath
                    });
                    continue;
                }

                var mountReq = prefab.GetComponent<MountRequirementAuthoring>();
                if (mountReq == null)
                {
                    report.Issues.Add(new ValidationIssue
                    {
                        Severity = ValidationSeverity.Error,
                        Message = $"Module '{moduleData.id}' prefab missing MountRequirementAuthoring",
                        PrefabPath = prefabPath
                    });
                    continue;
                }

                if (mountReq.mountType != moduleData.requiredMount || mountReq.mountSize != moduleData.requiredSize)
                {
                    report.Issues.Add(new ValidationIssue
                    {
                        Severity = ValidationSeverity.Error,
                        Message = $"Module '{moduleData.id}' mount mismatch: Catalog={moduleData.requiredMount}/{moduleData.requiredSize}, Prefab={mountReq.mountType}/{mountReq.mountSize}",
                        PrefabPath = prefabPath
                    });
                }
            }
        }

        private static void ValidateOrphanedPrefabs(ValidationReport report)
        {
            var catalogIds = new HashSet<string>();

            // Load all catalogs and collect IDs
            var moduleCatalog = LoadModuleCatalog("Assets/Data/Catalogs");
            var hullCatalog = LoadHullCatalog("Assets/Data/Catalogs");
            var stationCatalog = LoadStationCatalog("Assets/Data/Catalogs");
            var resourceCatalog = LoadResourceCatalog("Assets/Data/Catalogs");
            var productCatalog = LoadProductCatalog("Assets/Data/Catalogs");
            var aggregateCatalog = LoadAggregateCatalog("Assets/Data/Catalogs");
            var effectCatalog = LoadEffectCatalog("Assets/Data/Catalogs");

            if (moduleCatalog?.modules != null)
            {
                foreach (var module in moduleCatalog.modules)
                {
                    if (!string.IsNullOrWhiteSpace(module.id))
                    {
                        catalogIds.Add(module.id);
                    }
                }
            }

            if (hullCatalog?.hulls != null)
            {
                foreach (var hull in hullCatalog.hulls)
                {
                    if (!string.IsNullOrWhiteSpace(hull.id))
                    {
                        catalogIds.Add(hull.id);
                    }
                }
            }

            if (stationCatalog?.stations != null)
            {
                foreach (var station in stationCatalog.stations)
                {
                    if (!string.IsNullOrWhiteSpace(station.id))
                    {
                        catalogIds.Add(station.id);
                    }
                }
            }

            if (resourceCatalog?.resources != null)
            {
                foreach (var resource in resourceCatalog.resources)
                {
                    if (!string.IsNullOrWhiteSpace(resource.id))
                    {
                        catalogIds.Add(resource.id);
                    }
                }
            }

            if (productCatalog?.products != null)
            {
                foreach (var product in productCatalog.products)
                {
                    if (!string.IsNullOrWhiteSpace(product.id))
                    {
                        catalogIds.Add(product.id);
                    }
                }
            }

            if (aggregateCatalog?.aggregates != null)
            {
                foreach (var aggregate in aggregateCatalog.aggregates)
                {
                    if (!string.IsNullOrWhiteSpace(aggregate.id))
                    {
                        catalogIds.Add(aggregate.id);
                    }
                }
            }

            if (effectCatalog?.effects != null)
            {
                foreach (var effect in effectCatalog.effects)
                {
                    if (!string.IsNullOrWhiteSpace(effect.id))
                    {
                        catalogIds.Add(effect.id);
                    }
                }
            }

            // Check module prefabs
            var moduleDir = $"{PrefabBasePath}/Modules";
            if (Directory.Exists(moduleDir))
            {
                var moduleFiles = Directory.GetFiles(moduleDir, "*.prefab", SearchOption.TopDirectoryOnly);
                foreach (var file in moduleFiles)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(file.Replace('\\', '/'));
                    if (prefab != null)
                    {
                        var moduleId = prefab.GetComponent<ModuleIdAuthoring>();
                        if (moduleId != null && !string.IsNullOrWhiteSpace(moduleId.moduleId))
                        {
                            if (!catalogIds.Contains(moduleId.moduleId))
                            {
                                report.Issues.Add(new ValidationIssue
                                {
                                    Severity = ValidationSeverity.Warning,
                                    Message = $"Orphaned module prefab: '{moduleId.moduleId}' not found in catalog",
                                    PrefabPath = file.Replace('\\', '/')
                                });
                            }
                        }
                    }
                }
            }

            // Check hull prefabs in all category folders
            var hullCategoryDirs = new[] { "Hulls", "CapitalShips", "Carriers", "Stations" };
            foreach (var categoryDir in hullCategoryDirs)
            {
                var hullDir = $"{PrefabBasePath}/{categoryDir}";
                if (Directory.Exists(hullDir))
                {
                    var hullFiles = Directory.GetFiles(hullDir, "*.prefab", SearchOption.TopDirectoryOnly);
                    foreach (var file in hullFiles)
                    {
                        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(file.Replace('\\', '/'));
                        if (prefab != null)
                        {
                            var hullId = prefab.GetComponent<HullIdAuthoring>();
                            if (hullId != null && !string.IsNullOrWhiteSpace(hullId.hullId))
                            {
                                if (!catalogIds.Contains(hullId.hullId))
                                {
                                    report.Issues.Add(new ValidationIssue
                                    {
                                        Severity = ValidationSeverity.Warning,
                                        Message = $"Orphaned hull prefab: '{hullId.hullId}' not found in catalog",
                                        PrefabPath = file.Replace('\\', '/')
                                    });
                                }
                            }
                        }
                    }
                }
            }

            // Check other category prefabs
            CheckOrphanedPrefabsInCategory("Stations", catalogIds, report, (prefab) => prefab.GetComponent<StationIdAuthoring>()?.stationId);
            CheckOrphanedPrefabsInCategory("Resources", catalogIds, report, (prefab) => prefab.GetComponent<ResourceIdAuthoring>()?.resourceId);
            CheckOrphanedPrefabsInCategory("Products", catalogIds, report, (prefab) => prefab.GetComponent<ProductIdAuthoring>()?.productId);
            CheckOrphanedPrefabsInCategory("Aggregates", catalogIds, report, (prefab) => prefab.GetComponent<AggregateIdAuthoring>()?.aggregateId);
            CheckOrphanedPrefabsInCategory("FX", catalogIds, report, (prefab) => prefab.GetComponent<EffectIdAuthoring>()?.effectId);
        }

        private static void CheckOrphanedPrefabsInCategory(string categoryDir, HashSet<string> catalogIds, ValidationReport report, Func<GameObject, string> getId)
        {
            var dir = $"{PrefabBasePath}/{categoryDir}";
            if (!Directory.Exists(dir)) return;

            var files = Directory.GetFiles(dir, "*.prefab", SearchOption.TopDirectoryOnly);
            foreach (var file in files)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(file.Replace('\\', '/'));
                if (prefab != null)
                {
                    var id = getId(prefab);
                    if (!string.IsNullOrWhiteSpace(id) && !catalogIds.Contains(id))
                    {
                        report.Issues.Add(new ValidationIssue
                        {
                            Severity = ValidationSeverity.Warning,
                            Message = $"Orphaned {categoryDir.ToLower()} prefab: '{id}' not found in catalog",
                            PrefabPath = file.Replace('\\', '/')
                        });
                    }
                }
            }
        }

        private static void ValidateHangarCapacity(HullCatalogAuthoring catalog, ValidationReport report)
        {
            if (catalog?.hulls == null) return;

            foreach (var hullData in catalog.hulls)
            {
                if (hullData.hangarCapacity <= 0f) continue; // Skip if no hangar capacity

                var categoryFolder = GetCategoryFolder(hullData.category);
                var prefabPath = $"{PrefabBasePath}/{categoryFolder}/{hullData.id}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                
                if (prefab == null) continue;

                var hangarCap = prefab.GetComponent<HangarCapacityAuthoring>();
                if (hangarCap == null)
                {
                    report.Issues.Add(new ValidationIssue
                    {
                        Severity = ValidationSeverity.Warning,
                        Message = $"Hull '{hullData.id}' has hangar capacity in catalog ({hullData.hangarCapacity}) but missing HangarCapacityAuthoring",
                        PrefabPath = prefabPath
                    });
                }
                else if (Math.Abs(hangarCap.capacity - hullData.hangarCapacity) > 0.01f)
                {
                    report.Issues.Add(new ValidationIssue
                    {
                        Severity = ValidationSeverity.Warning,
                        Message = $"Hull '{hullData.id}' hangar capacity mismatch: Catalog={hullData.hangarCapacity}, Prefab={hangarCap.capacity}",
                        PrefabPath = prefabPath
                    });
                }
            }
        }

        private static void ValidateModuleFunctions(ModuleCatalogAuthoring catalog, ValidationReport report)
        {
            if (catalog?.modules == null) return;

            foreach (var moduleData in catalog.modules)
            {
                if (moduleData.function == ModuleFunction.None) continue;

                var prefabPath = $"{PrefabBasePath}/Modules/{moduleData.id}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                
                if (prefab == null) continue;

                var moduleFunction = prefab.GetComponent<ModuleFunctionAuthoring>();
                if (moduleFunction == null)
                {
                    report.Issues.Add(new ValidationIssue
                    {
                        Severity = ValidationSeverity.Warning,
                        Message = $"Module '{moduleData.id}' has function '{moduleData.function}' in catalog but missing ModuleFunctionAuthoring",
                        PrefabPath = prefabPath
                    });
                }
                else if (moduleFunction.function != moduleData.function)
                {
                    report.Issues.Add(new ValidationIssue
                    {
                        Severity = ValidationSeverity.Error,
                        Message = $"Module '{moduleData.id}' function mismatch: Catalog={moduleData.function}, Prefab={moduleFunction.function}",
                        PrefabPath = prefabPath
                    });
                }
            }
        }

        private static void ValidateBindingIntegrity(ValidationReport report)
        {
            var bindingJsonPath = BindingPath.Replace(".asset", ".json");
            if (!File.Exists(bindingJsonPath))
            {
                report.Issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Warning,
                    Message = "Binding JSON file not found - run generation first",
                    PrefabPath = bindingJsonPath
                });
                return;
            }

            try
            {
                var json = File.ReadAllText(bindingJsonPath);
                var bindingData = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);

                // Validate that all referenced prefabs exist
                // This is a simplified check - full implementation would validate all entries
                report.Issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Warning,
                    Message = "Binding integrity check partially implemented",
                    PrefabPath = bindingJsonPath
                });
            }
            catch (Exception ex)
            {
                report.Issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Error,
                    Message = $"Failed to validate binding integrity: {ex.Message}",
                    PrefabPath = bindingJsonPath
                });
            }
        }

        // CLI entry point for batchmode/CI
        public static void Run(string catalogPath = "Assets/Data/Catalogs", bool dryRun = false)
        {
            var result = GenerateAll(catalogPath, true, true, dryRun);
            
            // Generate coverage heatmap
            var coverageReport = CoverageHeatmap.GenerateReport(catalogPath);
            if (!dryRun)
            {
                CoverageHeatmap.SaveReport(coverageReport, catalogPath);
            }
            CoverageHeatmap.PrintReport(coverageReport);
            
            var logMsg = $"PrefabMaker Run Complete: Created={result.CreatedCount}, Updated={result.UpdatedCount}, Skipped={result.SkippedCount}";
            logMsg += $"\nCoverage: {coverageReport.OverallCoverage:F1}% overall";
            
            if (result.Warnings.Count > 0)
            {
                logMsg += $"\nWarnings: {string.Join("; ", result.Warnings)}";
            }
            
            if (result.Errors.Count > 0)
            {
                logMsg += $"\nErrors: {string.Join("; ", result.Errors)}";
                UnityDebug.LogError(logMsg);
            }
            else
            {
                UnityDebug.Log(logMsg);
            }
        }

        [MenuItem("Tools/Space4X/Prefab Maker/Run (CLI)")]
        public static void RunCLI()
        {
            Run("Assets/Data/Catalogs", false);
        }
    }
}

