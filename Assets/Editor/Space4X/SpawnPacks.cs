using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Space4X.Authoring;
using UnityEditor;
using UnityEngine;

namespace Space4X.Editor
{
    using Debug = UnityEngine.Debug;

    /// <summary>
    /// Spawn packs - composes encounter/resource packs (weighted lists) as data; generators verify all referenced tokens/prefabs exist.
    /// </summary>
    public static class SpawnPacks
    {
        [Serializable]
        public class SpawnPack
        {
            public string Id;
            public string Name;
            public SpawnPackType Type;
            public List<WeightedEntry> Entries = new List<WeightedEntry>();
        }

        public enum SpawnPackType
        {
            Encounter,
            Resource,
            Fleet,
            Station
        }

        [Serializable]
        public class WeightedEntry
        {
            public string PrefabId;
            public float Weight;
            public int MinCount;
            public int MaxCount;
        }

        public static List<SpawnPack> LoadPacks(string packsPath)
        {
            var packs = new List<SpawnPack>();
            if (!Directory.Exists(packsPath)) return packs;

            var packFiles = Directory.GetFiles(packsPath, "*.json", System.IO.SearchOption.TopDirectoryOnly);
            foreach (var file in packFiles)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var pack = JsonConvert.DeserializeObject<SpawnPack>(json);
                    if (pack != null)
                    {
                        packs.Add(pack);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Failed to load spawn pack {file}: {ex.Message}");
                }
            }

            return packs;
        }

        public static List<string> ValidatePack(SpawnPack pack, string prefabBasePath = "Assets/Prefabs/Space4X")
        {
            var issues = new List<string>();

            foreach (var entry in pack.Entries)
            {
                if (string.IsNullOrEmpty(entry.PrefabId))
                {
                    issues.Add($"Pack '{pack.Id}' has entry with empty PrefabId");
                    continue;
                }

                // Check if prefab exists
                var prefabPath = FindPrefabPath(entry.PrefabId, prefabBasePath);
                if (string.IsNullOrEmpty(prefabPath))
                {
                    issues.Add($"Pack '{pack.Id}' references missing prefab '{entry.PrefabId}'");
                }
            }

            return issues;
        }

        private static string FindPrefabPath(string prefabId, string prefabBasePath)
        {
            var prefabDirs = new[] { "Hulls", "CapitalShips", "Carriers", "Stations", "Modules", "Resources", "Products", "Aggregates", "FX", "Individuals" };
            
            foreach (var dir in prefabDirs)
            {
                var fullDir = $"{prefabBasePath}/{dir}";
                if (!Directory.Exists(fullDir)) continue;

                var prefabs = Directory.GetFiles(fullDir, "*.prefab", System.IO.SearchOption.AllDirectories);
                foreach (var prefabPath in prefabs)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    if (prefab == null) continue;

                    // Check ID components
                    if (HasPrefabId(prefab, prefabId))
                    {
                        return prefabPath;
                    }
                }
            }

            return null;
        }

        private static bool HasPrefabId(GameObject prefab, string id)
        {
            return prefab.GetComponent<HullIdAuthoring>()?.hullId == id ||
                   prefab.GetComponent<ModuleIdAuthoring>()?.moduleId == id ||
                   prefab.GetComponent<StationIdAuthoring>()?.stationId == id ||
                   prefab.GetComponent<ResourceIdAuthoring>()?.resourceId == id ||
                   prefab.GetComponent<ProductIdAuthoring>()?.productId == id ||
                   prefab.GetComponent<AggregateIdAuthoring>()?.aggregateId == id ||
                   prefab.GetComponent<EffectIdAuthoring>()?.effectId == id ||
                   prefab.name == id;
        }
    }
}

