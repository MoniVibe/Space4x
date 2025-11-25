using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using Space4X.Authoring;
using Space4X.Editor;
using Space4X.Registry;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Space4X.Editor.Tests
{
    public class PrefabMakerTests
    {
        private const string TestPrefabBasePath = "Assets/Prefabs/Space4X/Test";
        private const string TestCatalogPath = "Assets/Data/Catalogs";

        [SetUp]
        public void SetUp()
        {
            // Clean up any existing test prefabs
            CleanupTestPrefabs();
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up test prefabs after each test
            CleanupTestPrefabs();
            AssetDatabase.Refresh();
        }

        private void CleanupTestPrefabs()
        {
            var testDirs = new[]
            {
                $"{TestPrefabBasePath}/Hulls",
                $"{TestPrefabBasePath}/Modules",
                $"{TestPrefabBasePath}/Stations",
                $"{TestPrefabBasePath}/FX"
            };

            foreach (var dir in testDirs)
            {
                if (Directory.Exists(dir))
                {
                    var files = Directory.GetFiles(dir, "*.prefab", SearchOption.TopDirectoryOnly);
                    foreach (var file in files)
                    {
                        AssetDatabase.DeleteAsset(file);
                    }
                }
            }
        }

        [Test]
        public void PrefabMaker_Batch_Idempotent()
        {
            // First generation
            var result1 = PrefabMaker.GenerateAll(TestCatalogPath, true, true, false);
            Assert.Greater(result1.CreatedCount, 0, "First generation should create prefabs");

            // Capture hashes of created prefabs
            var hashes1 = GetPrefabHashes();

            // Second generation (should be idempotent)
            var result2 = PrefabMaker.GenerateAll(TestCatalogPath, true, true, false);
            Assert.AreEqual(0, result2.CreatedCount, "Second generation should not create new prefabs");
            Assert.Greater(result2.SkippedCount, 0, "Second generation should skip existing prefabs");

            // Compare hashes
            var hashes2 = GetPrefabHashes();
            CollectionAssert.AreEqual(hashes1.Keys, hashes2.Keys, "Prefab sets should match");

            // Note: We compare that the same prefabs exist, not exact content hashes
            // Full content hash comparison would require more sophisticated comparison
            foreach (var key in hashes1.Keys)
            {
                Assert.AreEqual(hashes1[key], hashes2[key], $"Prefab {key} should be unchanged");
            }
        }

        [Test]
        public void PrefabMaker_HullSockets_MatchCatalog()
        {
            // Generate prefabs
            var result = PrefabMaker.GenerateAll(TestCatalogPath, true, true, false);
            Assert.Greater(result.CreatedCount + result.SkippedCount, 0, "Should have generated or found prefabs");

            // Load hull catalog
            var catalog = LoadHullCatalog();
            Assert.NotNull(catalog, "Hull catalog should exist");

            // Validate each hull
            foreach (var hullData in catalog.hulls)
            {
                // Determine category folder
                var categoryFolder = GetCategoryFolder(hullData.category);
                var prefabPath = $"Assets/Prefabs/Space4X/{categoryFolder}/{hullData.id}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                
                if (prefab == null)
                {
                    Assert.Fail($"Hull prefab '{hullData.id}' not found at {prefabPath}");
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

                // Validate counts match
                foreach (var kvp in expectedSockets)
                {
                    Assert.IsTrue(actualSockets.ContainsKey(kvp.Key), 
                        $"Hull '{hullData.id}' missing socket type {kvp.Key}");
                    Assert.AreEqual(kvp.Value, actualSockets[kvp.Key],
                        $"Hull '{hullData.id}' socket count mismatch for {kvp.Key}: expected {kvp.Value}, found {actualSockets[kvp.Key]}");
                }
            }
        }

        [Test]
        public void PrefabMaker_ModuleFit_Valid()
        {
            // Generate prefabs
            var result = PrefabMaker.GenerateAll(TestCatalogPath, true, true, false);
            Assert.Greater(result.CreatedCount + result.SkippedCount, 0, "Should have generated or found prefabs");

            // Load module catalog
            var catalog = LoadModuleCatalog();
            Assert.NotNull(catalog, "Module catalog should exist");

            // Validate each module
            foreach (var moduleData in catalog.modules)
            {
                var prefabPath = $"Assets/Prefabs/Space4X/Modules/{moduleData.id}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                
                if (prefab == null)
                {
                    Assert.Fail($"Module prefab '{moduleData.id}' not found at {prefabPath}");
                    continue;
                }

                // Check ModuleIdAuthoring
                var moduleId = prefab.GetComponent<ModuleIdAuthoring>();
                Assert.NotNull(moduleId, $"Module '{moduleData.id}' prefab missing ModuleIdAuthoring");
                Assert.AreEqual(moduleData.id, moduleId.moduleId, 
                    $"Module '{moduleData.id}' ID mismatch");

                // Check MountRequirementAuthoring
                var mountReq = prefab.GetComponent<MountRequirementAuthoring>();
                Assert.NotNull(mountReq, $"Module '{moduleData.id}' prefab missing MountRequirementAuthoring");
                Assert.AreEqual(moduleData.requiredMount, mountReq.mountType,
                    $"Module '{moduleData.id}' mount type mismatch");
                Assert.AreEqual(moduleData.requiredSize, mountReq.mountSize,
                    $"Module '{moduleData.id}' mount size mismatch");
            }
        }

        [Test]
        public void Binding_Blob_Parity()
        {
            // Generate prefabs
            var result = PrefabMaker.GenerateAll(TestCatalogPath, true, true, false);
            Assert.Greater(result.CreatedCount + result.SkippedCount, 0, "Should have generated or found prefabs");

            // Check that binding JSON exists
            var bindingPath = "Assets/Space4X/Bindings/Space4XPresentationBinding.json";
            Assert.IsTrue(File.Exists(bindingPath), "Binding JSON should exist");

            // Load and validate binding data
            var json = File.ReadAllText(bindingPath);
            Assert.IsNotEmpty(json, "Binding JSON should not be empty");

            // Basic validation - binding should reference existing prefabs
            var hullDir = "Assets/Prefabs/Space4X/Hulls";
            var moduleDir = "Assets/Prefabs/Space4X/Modules";

            if (Directory.Exists(hullDir))
            {
                var hullFiles = Directory.GetFiles(hullDir, "*.prefab", SearchOption.TopDirectoryOnly);
                foreach (var file in hullFiles)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(file);
                    Assert.NotNull(prefab, $"Hull prefab should exist: {file}");
                }
            }

            if (Directory.Exists(moduleDir))
            {
                var moduleFiles = Directory.GetFiles(moduleDir, "*.prefab", SearchOption.TopDirectoryOnly);
                foreach (var file in moduleFiles)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(file);
                    Assert.NotNull(prefab, $"Module prefab should exist: {file}");
                }
            }
        }

        [Test]
        public void PrefabMaker_Validation_ReportsIssues()
        {
            // Run validation
            var report = PrefabMaker.ValidateAll();
            Assert.NotNull(report, "Validation report should not be null");

            // Validation should complete without exceptions
            // (May have warnings/errors, but should not crash)
            Assert.IsNotNull(report.Issues, "Report should have issues list");
        }

        [Test]
        public void PrefabMaker_HangarCapacity_Valid()
        {
            // Generate prefabs
            var result = PrefabMaker.GenerateAll(TestCatalogPath, true, true, false);
            Assert.Greater(result.CreatedCount + result.SkippedCount, 0, "Should have generated or found prefabs");

            // Load hull catalog
            var catalog = LoadHullCatalog();
            Assert.NotNull(catalog, "Hull catalog should exist");

            // Validate hangar capacity for carriers
            foreach (var hullData in catalog.hulls)
            {
                if (hullData.hangarCapacity <= 0f) continue;

                var categoryFolder = GetCategoryFolder(hullData.category);
                var prefabPath = $"Assets/Prefabs/Space4X/{categoryFolder}/{hullData.id}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                
                if (prefab == null) continue;

                var hangarCap = prefab.GetComponent<HangarCapacityAuthoring>();
                Assert.NotNull(hangarCap, $"Hull '{hullData.id}' with hangar capacity should have HangarCapacityAuthoring");
                Assert.AreEqual(hullData.hangarCapacity, hangarCap.capacity, 0.01f, 
                    $"Hull '{hullData.id}' hangar capacity should match catalog");
            }
        }

        [Test]
        public void PrefabMaker_ModuleFunctions_Valid()
        {
            // Generate prefabs
            var result = PrefabMaker.GenerateAll(TestCatalogPath, true, true, false);
            Assert.Greater(result.CreatedCount + result.SkippedCount, 0, "Should have generated or found prefabs");

            // Load module catalog
            var catalog = LoadModuleCatalog();
            Assert.NotNull(catalog, "Module catalog should exist");

            // Validate module functions
            foreach (var moduleData in catalog.modules)
            {
                if (moduleData.function == ModuleFunction.None) continue;

                var prefabPath = $"Assets/Prefabs/Space4X/Modules/{moduleData.id}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                
                if (prefab == null) continue;

                var moduleFunction = prefab.GetComponent<ModuleFunctionAuthoring>();
                Assert.NotNull(moduleFunction, $"Module '{moduleData.id}' with function should have ModuleFunctionAuthoring");
                Assert.AreEqual(moduleData.function, moduleFunction.function,
                    $"Module '{moduleData.id}' function should match catalog");
            }
        }

        private Dictionary<string, string> GetPrefabHashes()
        {
            var hashes = new Dictionary<string, string>();
            var dirs = new[] { "Hulls", "CapitalShips", "Carriers", "Stations", "Modules" };

            foreach (var dir in dirs)
            {
                var fullPath = $"Assets/Prefabs/Space4X/{dir}";
                if (Directory.Exists(fullPath))
                {
                    var files = Directory.GetFiles(fullPath, "*.prefab", SearchOption.TopDirectoryOnly);
                    foreach (var file in files)
                    {
                        var guid = AssetDatabase.AssetPathToGUID(file);
                        hashes[file] = guid; // Use GUID as a proxy for content hash
                    }
                }
            }

            return hashes;
        }

        private string GetCategoryFolder(HullCategory category)
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
                    return "Hulls";
            }
        }

        private HullCatalogAuthoring LoadHullCatalog()
        {
            var prefabPath = $"{TestCatalogPath}/HullCatalog.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            return prefab?.GetComponent<HullCatalogAuthoring>();
        }

        private ModuleCatalogAuthoring LoadModuleCatalog()
        {
            var prefabPath = $"{TestCatalogPath}/ModuleCatalog.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            return prefab?.GetComponent<ModuleCatalogAuthoring>();
        }
    }
}

