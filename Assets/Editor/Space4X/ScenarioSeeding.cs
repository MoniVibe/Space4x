using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Space4X.Authoring;
using Space4X.Registry;
using UnityEditor;
using UnityEngine;

namespace Space4X.Editor
{
    /// <summary>
    /// Scenario seeding - writes content seed blob describing assets/variants per run for ScenarioRunner determinism.
    /// </summary>
    public static class ScenarioSeeding
    {
        [Serializable]
        public class ContentSeed
        {
            public string Version = "1.0";
            public string GeneratedAt;
            public string CatalogPath;
            public Dictionary<string, List<string>> PrefabIds = new Dictionary<string, List<string>>();
            public Dictionary<string, object> Variants = new Dictionary<string, object>();
            public Dictionary<string, int> CatalogCounts = new Dictionary<string, int>();
        }

        public static ContentSeed GenerateSeed(string catalogPath, string outputPath = null)
        {
            var seed = new ContentSeed
            {
                GeneratedAt = DateTime.UtcNow.ToString("O"),
                CatalogPath = catalogPath
            };

            // Collect prefab IDs by category
            var prefabBasePath = "Assets/Prefabs/Space4X";
            seed.PrefabIds["Hulls"] = CollectPrefabIds($"{prefabBasePath}/Hulls", typeof(HullIdAuthoring), "hullId");
            seed.PrefabIds["CapitalShips"] = CollectPrefabIds($"{prefabBasePath}/CapitalShips", typeof(HullIdAuthoring), "hullId");
            seed.PrefabIds["Carriers"] = CollectPrefabIds($"{prefabBasePath}/Carriers", typeof(HullIdAuthoring), "hullId");
            seed.PrefabIds["Stations"] = CollectPrefabIds($"{prefabBasePath}/Stations", typeof(StationIdAuthoring), "stationId");
            seed.PrefabIds["Modules"] = CollectPrefabIds($"{prefabBasePath}/Modules", typeof(ModuleIdAuthoring), "moduleId");
            seed.PrefabIds["Resources"] = CollectPrefabIds($"{prefabBasePath}/Resources", typeof(ResourceIdAuthoring), "resourceId");
            seed.PrefabIds["Products"] = CollectPrefabIds($"{prefabBasePath}/Products", typeof(ProductIdAuthoring), "productId");
            seed.PrefabIds["Aggregates"] = CollectPrefabIds($"{prefabBasePath}/Aggregates", typeof(AggregateIdAuthoring), "aggregateId");
            seed.PrefabIds["FX"] = CollectPrefabIds($"{prefabBasePath}/FX", typeof(EffectIdAuthoring), "effectId");

            // Get catalog counts
            seed.CatalogCounts = PrefabMaker.CountCatalogEntries(catalogPath);

            // Save seed
            if (!string.IsNullOrEmpty(outputPath))
            {
                var json = JsonConvert.SerializeObject(seed, Formatting.Indented);
                File.WriteAllText(outputPath, json);
                AssetDatabase.ImportAsset(outputPath);
            }

            return seed;
        }

        private static List<string> CollectPrefabIds(string dir, Type idComponentType, string idFieldName)
        {
            var ids = new List<string>();
            if (!Directory.Exists(dir)) return ids;

            var prefabs = Directory.GetFiles(dir, "*.prefab", SearchOption.TopDirectoryOnly);
            foreach (var prefabPath in prefabs)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null) continue;

                var component = prefab.GetComponent(idComponentType);
                if (component == null) continue;

                var field = idComponentType.GetField(idFieldName);
                if (field != null)
                {
                    var id = field.GetValue(component) as string;
                    if (!string.IsNullOrEmpty(id))
                    {
                        ids.Add(id);
                    }
                }
            }

            return ids;
        }
    }
}

