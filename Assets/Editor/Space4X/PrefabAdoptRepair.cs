using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Space4X.Authoring;
using Space4X.Registry;
using UnityEditor;
using UnityEngine;

namespace Space4X.Editor
{
    /// <summary>
    /// Adopt/Repair tool - scans existing prefabs, adds missing components/sockets, normalizes names/paths.
    /// </summary>
    public static class PrefabAdoptRepair
    {
        public class RepairResult
        {
            public int ScannedCount;
            public int RepairedCount;
            public int AdoptedCount;
            public List<string> Issues = new List<string>();
            public List<string> Repairs = new List<string>();
        }

        public static RepairResult ScanAndRepair(string prefabBasePath = "Assets/Prefabs/Space4X", bool dryRun = false)
        {
            var result = new RepairResult();

            // Scan all prefab directories
            var prefabDirs = new[]
            {
                ("Hulls", typeof(HullIdAuthoring)),
                ("CapitalShips", typeof(HullIdAuthoring)),
                ("Carriers", typeof(HullIdAuthoring)),
                ("Stations", typeof(StationIdAuthoring)),
                ("Modules", typeof(ModuleIdAuthoring)),
                ("Resources", typeof(ResourceIdAuthoring)),
                ("Products", typeof(ProductIdAuthoring)),
                ("Aggregates", typeof(AggregateIdAuthoring)),
                ("FX", typeof(EffectIdAuthoring)),
                ("Individuals/Captains", null),
                ("Individuals/Officers", null),
                ("Individuals/Crew", null)
            };

            foreach (var (dir, expectedIdType) in prefabDirs)
            {
                var fullDir = $"{prefabBasePath}/{dir}";
                if (Directory.Exists(fullDir))
                {
                    var prefabs = Directory.GetFiles(fullDir, "*.prefab", SearchOption.TopDirectoryOnly);
                    foreach (var prefabPath in prefabs)
                    {
                        result.ScannedCount++;
                        RepairPrefab(prefabPath, expectedIdType, prefabBasePath, result, dryRun);
                    }
                }
            }

            return result;
        }

        private static void RepairPrefab(string prefabPath, Type expectedIdType, string prefabBasePath, RepairResult result, bool dryRun)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                result.Issues.Add($"Could not load prefab: {prefabPath}");
                return;
            }

            var needsSave = false;
            var prefabObj = PrefabUtility.LoadPrefabContents(prefabPath);

            try
            {
                // Check for expected ID component
                if (expectedIdType != null)
                {
                    var hasIdComponent = prefabObj.GetComponent(expectedIdType) != null;
                    if (!hasIdComponent)
                    {
                        // Try to infer ID from name
                        var inferredId = InferIdFromName(prefabObj.name, expectedIdType);
                        if (!string.IsNullOrEmpty(inferredId))
                        {
                            AddIdComponent(prefabObj, expectedIdType, inferredId);
                            result.Repairs.Add($"{prefabPath}: Added missing {expectedIdType.Name} with ID '{inferredId}'");
                            needsSave = true;
                            result.RepairedCount++;
                        }
                        else
                        {
                            result.Issues.Add($"{prefabPath}: Missing {expectedIdType.Name} and could not infer ID");
                        }
                    }
                }

                // Check for hull sockets if it's a hull
                if (prefabObj.GetComponent<HullIdAuthoring>() != null)
                {
                    var socketAuthoring = prefabObj.GetComponent<HullSocketAuthoring>();
                    if (socketAuthoring == null)
                    {
                        socketAuthoring = prefabObj.AddComponent<HullSocketAuthoring>();
                        socketAuthoring.autoCreateFromCatalog = true;
                        result.Repairs.Add($"{prefabPath}: Added missing HullSocketAuthoring");
                        needsSave = true;
                        result.RepairedCount++;
                    }

                    // Check socket naming consistency
                    var sockets = new List<Transform>();
                    for (int i = 0; i < prefabObj.transform.childCount; i++)
                    {
                        var child = prefabObj.transform.GetChild(i);
                        if (child.name.StartsWith("Socket_"))
                        {
                            sockets.Add(child);
                        }
                    }

                    // Normalize socket names
                    foreach (var socket in sockets)
                    {
                        if (!IsValidSocketName(socket.name))
                        {
                            var normalizedName = NormalizeSocketName(socket.name);
                            if (normalizedName != socket.name)
                            {
                                socket.name = normalizedName;
                                result.Repairs.Add($"{prefabPath}: Normalized socket name '{socket.name}' -> '{normalizedName}'");
                                needsSave = true;
                            }
                        }
                    }
                }

                // Normalize prefab name
                var normalizedPrefabName = NormalizePrefabName(prefabObj.name);
                if (normalizedPrefabName != prefabObj.name)
                {
                    result.Repairs.Add($"{prefabPath}: Normalized prefab name '{prefabObj.name}' -> '{normalizedPrefabName}'");
                    prefabObj.name = normalizedPrefabName;
                    needsSave = true;
                }

                // Check path consistency
                var expectedPath = GetExpectedPath(prefabObj, prefabBasePath);
                if (expectedPath != prefabPath && !string.IsNullOrEmpty(expectedPath))
                {
                    result.Repairs.Add($"{prefabPath}: Should be at '{expectedPath}'");
                    if (!dryRun)
                    {
                        // Move prefab to expected location
                        var error = AssetDatabase.MoveAsset(prefabPath, expectedPath);
                        if (string.IsNullOrEmpty(error))
                        {
                            result.Repairs.Add($"Moved prefab to {expectedPath}");
                            needsSave = false; // Already saved by MoveAsset
                        }
                        else
                        {
                            result.Issues.Add($"Failed to move prefab: {error}");
                        }
                    }
                }

                if (needsSave && !dryRun)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabObj, prefabPath);
                    result.AdoptedCount++;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabObj);
            }
        }

        private static string InferIdFromName(string name, Type idType)
        {
            // Remove common suffixes/prefixes
            var id = name;
            id = id.Replace(".prefab", "");
            id = id.Trim();

            // Validate ID format (k-case, no spaces)
            if (IsValidId(id))
            {
                return id;
            }

            // Try to extract ID from name
            var parts = id.Split('_', '-', ' ');
            if (parts.Length > 0)
            {
                var candidate = parts[0];
                if (IsValidId(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static bool IsValidId(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            // k-case: lowercase with hyphens/underscores
            return id == id.ToLower() && !id.Contains(" ");
        }

        private static void AddIdComponent(GameObject obj, Type idType, string id)
        {
            if (idType == typeof(HullIdAuthoring))
            {
                var component = obj.AddComponent<HullIdAuthoring>();
                component.hullId = id;
            }
            else if (idType == typeof(ModuleIdAuthoring))
            {
                var component = obj.AddComponent<ModuleIdAuthoring>();
                component.moduleId = id;
            }
            else if (idType == typeof(StationIdAuthoring))
            {
                var component = obj.AddComponent<StationIdAuthoring>();
                component.stationId = id;
            }
            else if (idType == typeof(ResourceIdAuthoring))
            {
                var component = obj.AddComponent<ResourceIdAuthoring>();
                component.resourceId = id;
            }
            else if (idType == typeof(ProductIdAuthoring))
            {
                var component = obj.AddComponent<ProductIdAuthoring>();
                component.productId = id;
            }
            else if (idType == typeof(AggregateIdAuthoring))
            {
                var component = obj.AddComponent<AggregateIdAuthoring>();
                component.aggregateId = id;
            }
            else if (idType == typeof(EffectIdAuthoring))
            {
                var component = obj.AddComponent<EffectIdAuthoring>();
                component.effectId = id;
            }
        }

        private static bool IsValidSocketName(string name)
        {
            // Format: Socket_MountType_Size_Index
            if (!name.StartsWith("Socket_")) return false;
            var parts = name.Split('_');
            if (parts.Length != 4) return false;
            if (parts[0] != "Socket") return false;
            // Check index format (should be 2-digit)
            if (parts[3].Length != 2 || !int.TryParse(parts[3], out _)) return false;
            return true;
        }

        private static string NormalizeSocketName(string name)
        {
            // Try to fix common issues
            if (!name.StartsWith("Socket_")) return name;

            var parts = name.Split('_');
            if (parts.Length < 4)
            {
                // Try to reconstruct
                if (parts.Length >= 2)
                {
                    var mountType = parts[1];
                    var size = parts.Length > 2 ? parts[2] : "M";
                    var index = parts.Length > 3 ? parts[3] : "01";
                    // Pad index to 2 digits
                    if (int.TryParse(index, out var idx))
                    {
                        index = idx.ToString("D2");
                    }
                    return $"Socket_{mountType}_{size}_{index}";
                }
            }
            else if (parts.Length == 4)
            {
                // Pad index to 2 digits
                if (int.TryParse(parts[3], out var idx))
                {
                    return $"Socket_{parts[1]}_{parts[2]}_{idx:D2}";
                }
            }

            return name;
        }

        private static string NormalizePrefabName(string name)
        {
            // Remove .prefab extension if present
            name = name.Replace(".prefab", "");
            // Convert to k-case
            name = name.ToLower();
            // Replace spaces with underscores
            name = name.Replace(" ", "_");
            return name;
        }

        private static string GetExpectedPath(GameObject prefab, string basePath)
        {
            // Determine category from components
            if (prefab.GetComponent<HullIdAuthoring>() != null)
            {
                var category = HullCategory.Other;
                if (prefab.GetComponent<CapitalShipAuthoring>() != null) category = HullCategory.CapitalShip;
                else if (prefab.GetComponent<CarrierAuthoring>() != null) category = HullCategory.Carrier;
                else if (prefab.GetComponent<StationIdAuthoring>() != null) category = HullCategory.Station;

                var categoryFolder = GetCategoryFolder(category);
                var id = prefab.GetComponent<HullIdAuthoring>()?.hullId ?? prefab.name;
                return $"{basePath}/{categoryFolder}/{id}.prefab";
            }
            else if (prefab.GetComponent<ModuleIdAuthoring>() != null)
            {
                var id = prefab.GetComponent<ModuleIdAuthoring>()?.moduleId ?? prefab.name;
                return $"{basePath}/Modules/{id}.prefab";
            }
            else if (prefab.GetComponent<StationIdAuthoring>() != null)
            {
                var id = prefab.GetComponent<StationIdAuthoring>()?.stationId ?? prefab.name;
                return $"{basePath}/Stations/{id}.prefab";
            }
            // ... similar for other types

            return null;
        }

        private static string GetCategoryFolder(HullCategory category)
        {
            switch (category)
            {
                case HullCategory.CapitalShip: return "CapitalShips";
                case HullCategory.Carrier: return "Carriers";
                case HullCategory.Station: return "Stations";
                default: return "Hulls";
            }
        }
    }
}

