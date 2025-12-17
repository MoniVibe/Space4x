using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Space4X.Authoring;
using Space4X.EditorUtilities;
using Space4X.Registry;
using UnityEditor;
using UnityEngine;
using UnityDebug = UnityEngine.Debug;

namespace Space4X.Editor
{
    using Debug = UnityEngine.Debug;

    /// <summary>
    /// ID/name policy enforcer - enforces k-case, path policy, and duplicate-ID bans with auto-fix.
    /// </summary>
    public static class IdPolicyEnforcer
    {
        private static readonly Regex KCasePattern = new Regex(@"^[a-z0-9_-]+$", RegexOptions.Compiled);

        public class PolicyViolation
        {
            public string PrefabPath;
            public string CurrentId;
            public string SuggestedId;
            public string ViolationType; // InvalidCase, InvalidPath, DuplicateId
        }

        public static List<PolicyViolation> CheckIdPolicy(string prefabBasePath = "Assets/Prefabs/Space4X")
        {
            var violations = new List<PolicyViolation>();
            var idMap = new Dictionary<string, List<string>>();

            var prefabDirs = new[] { "Hulls", "CapitalShips", "Carriers", "Stations", "Modules", "Resources", "Products", "Aggregates", "FX", "Individuals" };
            
            foreach (var dir in prefabDirs)
            {
                var fullDir = $"{prefabBasePath}/{dir}";
                if (!System.IO.Directory.Exists(fullDir)) continue;

                var prefabs = System.IO.Directory.GetFiles(fullDir, "*.prefab", System.IO.SearchOption.AllDirectories);
                foreach (var prefabPath in prefabs)
                {
                    var assetPath = AssetPathUtil.ToAssetRelativePath(prefabPath);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                    if (prefab == null) continue;

                    var id = GetPrefabId(prefab);
                    if (string.IsNullOrEmpty(id)) continue;

                    // Check k-case format
                    if (!KCasePattern.IsMatch(id))
                    {
                        var suggestedId = NormalizeToKCase(id);
                        violations.Add(new PolicyViolation
                        {
                            PrefabPath = assetPath,
                            CurrentId = id,
                            SuggestedId = suggestedId,
                            ViolationType = "InvalidCase"
                        });
                    }

                    // Track for duplicate detection
                    if (!idMap.ContainsKey(id))
                    {
                        idMap[id] = new List<string>();
                    }
                    idMap[id].Add(assetPath);

                    // Check path consistency
                    var expectedPath = GetExpectedPath(prefab, prefabBasePath, id);
                    if (expectedPath != assetPath && !string.IsNullOrEmpty(expectedPath))
                    {
                        violations.Add(new PolicyViolation
                        {
                            PrefabPath = assetPath,
                            CurrentId = id,
                            SuggestedId = expectedPath,
                            ViolationType = "InvalidPath"
                        });
                    }
                }
            }

            // Check for duplicates
            foreach (var kvp in idMap)
            {
                if (kvp.Value.Count > 1)
                {
                    foreach (var path in kvp.Value)
                    {
                        violations.Add(new PolicyViolation
                        {
                            PrefabPath = path,
                            CurrentId = kvp.Key,
                            SuggestedId = null,
                            ViolationType = "DuplicateId"
                        });
                    }
                }
            }

            return violations;
        }

        public static void FixViolations(List<PolicyViolation> violations, bool dryRun = false)
        {
            foreach (var violation in violations)
            {
                if (dryRun)
                {
                    UnityDebug.Log($"[DRY-RUN] Would fix: {violation.PrefabPath} - {violation.ViolationType}");
                    continue;
                }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(violation.PrefabPath);
                if (prefab == null) continue;

                var prefabObj = PrefabUtility.LoadPrefabContents(violation.PrefabPath);
                try
                {
                    switch (violation.ViolationType)
                    {
                        case "InvalidCase":
                            FixIdCase(prefabObj, violation.SuggestedId);
                            break;
                        case "InvalidPath":
                            // Path fixes are handled separately via AssetDatabase.MoveAsset
                            break;
                        case "DuplicateId":
                            // Duplicate fixes require manual intervention or unique suffix
                            UnityDebug.LogWarning($"Duplicate ID '{violation.CurrentId}' requires manual fix: {violation.PrefabPath}");
                            break;
                    }

                    PrefabUtility.SaveAsPrefabAsset(prefabObj, violation.PrefabPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefabObj);
                }
            }
        }

        private static void FixIdCase(GameObject prefab, string suggestedId)
        {
            var hullId = prefab.GetComponent<HullIdAuthoring>();
            if (hullId != null) hullId.hullId = suggestedId;

            var moduleId = prefab.GetComponent<ModuleIdAuthoring>();
            if (moduleId != null) moduleId.moduleId = suggestedId;

            var stationId = prefab.GetComponent<StationIdAuthoring>();
            if (stationId != null) stationId.stationId = suggestedId;

            var resourceId = prefab.GetComponent<ResourceIdAuthoring>();
            if (resourceId != null) resourceId.resourceId = suggestedId;

            var productId = prefab.GetComponent<ProductIdAuthoring>();
            if (productId != null) productId.productId = suggestedId;

            var aggregateId = prefab.GetComponent<AggregateIdAuthoring>();
            if (aggregateId != null) aggregateId.aggregateId = suggestedId;

            var effectId = prefab.GetComponent<EffectIdAuthoring>();
            if (effectId != null) effectId.effectId = suggestedId;
        }

        private static string NormalizeToKCase(string id)
        {
            // Convert to lowercase
            id = id.ToLower();
            // Replace spaces with underscores
            id = id.Replace(" ", "_");
            // Remove invalid characters (keep only a-z, 0-9, _, -)
            id = System.Text.RegularExpressions.Regex.Replace(id, @"[^a-z0-9_-]", "");
            return id;
        }

        private static string GetPrefabId(GameObject prefab)
        {
            return prefab.GetComponent<HullIdAuthoring>()?.hullId ??
                   prefab.GetComponent<ModuleIdAuthoring>()?.moduleId ??
                   prefab.GetComponent<StationIdAuthoring>()?.stationId ??
                   prefab.GetComponent<ResourceIdAuthoring>()?.resourceId ??
                   prefab.GetComponent<ProductIdAuthoring>()?.productId ??
                   prefab.GetComponent<AggregateIdAuthoring>()?.aggregateId ??
                   prefab.GetComponent<EffectIdAuthoring>()?.effectId;
        }

        private static string GetExpectedPath(GameObject prefab, string basePath, string id)
        {
            if (prefab.GetComponent<HullIdAuthoring>() != null)
            {
                var category = HullCategory.Other;
                if (prefab.GetComponent<CapitalShipAuthoring>() != null) category = HullCategory.CapitalShip;
                else if (prefab.GetComponent<CarrierAuthoring>() != null) category = HullCategory.Carrier;
                else if (prefab.GetComponent<StationIdAuthoring>() != null) category = HullCategory.Station;

                var categoryFolder = GetCategoryFolder(category);
                return $"{basePath}/{categoryFolder}/{id}.prefab";
            }
            else if (prefab.GetComponent<ModuleIdAuthoring>() != null)
            {
                return $"{basePath}/Modules/{id}.prefab";
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

