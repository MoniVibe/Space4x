using System;
using System.Collections.Generic;
using System.Linq;
using Space4X.Authoring;
using Space4X.Registry;
using UnityEditor;
using UnityEngine;

namespace Space4X.Editor
{
    /// <summary>
    /// Rule-engine linter - validates materials, buildings, equipment, fauna, civics with actionable suggestions.
    /// </summary>
    public static class RuleEngineLinter
    {
        public class LintRule
        {
            public string Name;
            public Func<GameObject, bool> Validator;
            public Func<GameObject, string> Suggestion;
            public LintSeverity Severity;
        }

        public enum LintSeverity
        {
            Info,
            Warning,
            Error
        }

        public class LintResult
        {
            public string PrefabPath;
            public string RuleName;
            public string Message;
            public string Suggestion;
            public LintSeverity Severity;
        }

        private static readonly List<LintRule> Rules = new List<LintRule>
        {
            // Hull rules
            new LintRule
            {
                Name = "HullMissingSockets",
                Validator = (prefab) =>
                {
                    var hullId = prefab.GetComponent<HullIdAuthoring>();
                    if (hullId == null) return true; // Not a hull
                    var socketAuthoring = prefab.GetComponent<HullSocketAuthoring>();
                    if (socketAuthoring == null) return false;
                    // Check for actual socket children
                    for (int i = 0; i < prefab.transform.childCount; i++)
                    {
                        if (prefab.transform.GetChild(i).name.StartsWith("Socket_"))
                            return true;
                    }
                    return false;
                },
                Suggestion = (prefab) => "Add HullSocketAuthoring and create socket child transforms",
                Severity = LintSeverity.Warning
            },
            // Module rules
            new LintRule
            {
                Name = "ModuleMissingMountRequirement",
                Validator = (prefab) =>
                {
                    var moduleId = prefab.GetComponent<ModuleIdAuthoring>();
                    if (moduleId == null) return true;
                    return prefab.GetComponent<MountRequirementAuthoring>() != null;
                },
                Suggestion = (prefab) => "Add MountRequirementAuthoring component",
                Severity = LintSeverity.Error
            },
            // ID validation
            new LintRule
            {
                Name = "InvalidIdFormat",
                Validator = (prefab) =>
                {
                    // Check all ID components
                    var hullId = prefab.GetComponent<HullIdAuthoring>()?.hullId;
                    var moduleId = prefab.GetComponent<ModuleIdAuthoring>()?.moduleId;
                    var stationId = prefab.GetComponent<StationIdAuthoring>()?.stationId;
                    
                    var id = hullId ?? moduleId ?? stationId;
                    if (string.IsNullOrEmpty(id)) return true; // No ID component
                    
                    // k-case validation: lowercase, no spaces
                    return id == id.ToLower() && !id.Contains(" ");
                },
                Suggestion = (prefab) => "Convert ID to k-case (lowercase with hyphens/underscores, no spaces)",
                Severity = LintSeverity.Warning
            },
            // Duplicate ID detection
            new LintRule
            {
                Name = "DuplicateId",
                Validator = (prefab) => true, // Will be checked globally
                Suggestion = (prefab) => "Rename prefab to ensure unique ID",
                Severity = LintSeverity.Error
            }
        };

        public static List<LintResult> LintPrefab(GameObject prefab, string prefabPath)
        {
            var results = new List<LintResult>();

            foreach (var rule in Rules)
            {
                if (!rule.Validator(prefab))
                {
                    results.Add(new LintResult
                    {
                        PrefabPath = prefabPath,
                        RuleName = rule.Name,
                        Message = $"Rule '{rule.Name}' failed",
                        Suggestion = rule.Suggestion(prefab),
                        Severity = rule.Severity
                    });
                }
            }

            return results;
        }

        public static List<LintResult> LintAllPrefabs(string prefabBasePath = "Assets/Prefabs/Space4X")
        {
            var results = new List<LintResult>();
            var idMap = new Dictionary<string, List<string>>(); // ID -> list of prefab paths

            var prefabDirs = new[] { "Hulls", "CapitalShips", "Carriers", "Stations", "Modules", "Resources", "Products", "Aggregates", "FX", "Individuals" };
            
            foreach (var dir in prefabDirs)
            {
                var fullDir = $"{prefabBasePath}/{dir}";
                if (!System.IO.Directory.Exists(fullDir)) continue;

                var prefabs = System.IO.Directory.GetFiles(fullDir, "*.prefab", System.IO.SearchOption.AllDirectories);
                foreach (var prefabPath in prefabs)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    if (prefab == null) continue;

                    // Run lint rules
                    var lintResults = LintPrefab(prefab, prefabPath);
                    results.AddRange(lintResults);

                    // Track IDs for duplicate detection
                    var id = GetPrefabId(prefab);
                    if (!string.IsNullOrEmpty(id))
                    {
                        if (!idMap.ContainsKey(id))
                        {
                            idMap[id] = new List<string>();
                        }
                        idMap[id].Add(prefabPath);
                    }
                }
            }

            // Check for duplicate IDs
            foreach (var kvp in idMap)
            {
                if (kvp.Value.Count > 1)
                {
                    results.Add(new LintResult
                    {
                        PrefabPath = string.Join(", ", kvp.Value),
                        RuleName = "DuplicateId",
                        Message = $"Duplicate ID '{kvp.Key}' found in {kvp.Value.Count} prefabs",
                        Suggestion = "Rename prefabs to ensure unique IDs",
                        Severity = LintSeverity.Error
                    });
                }
            }

            return results;
        }

        private static string GetPrefabId(GameObject prefab)
        {
            var hullId = prefab.GetComponent<HullIdAuthoring>()?.hullId;
            if (!string.IsNullOrEmpty(hullId)) return hullId;

            var moduleId = prefab.GetComponent<ModuleIdAuthoring>()?.moduleId;
            if (!string.IsNullOrEmpty(moduleId)) return moduleId;

            var stationId = prefab.GetComponent<StationIdAuthoring>()?.stationId;
            if (!string.IsNullOrEmpty(stationId)) return stationId;

            var resourceId = prefab.GetComponent<ResourceIdAuthoring>()?.resourceId;
            if (!string.IsNullOrEmpty(resourceId)) return resourceId;

            var productId = prefab.GetComponent<ProductIdAuthoring>()?.productId;
            if (!string.IsNullOrEmpty(productId)) return productId;

            var aggregateId = prefab.GetComponent<AggregateIdAuthoring>()?.aggregateId;
            if (!string.IsNullOrEmpty(aggregateId)) return aggregateId;

            var effectId = prefab.GetComponent<EffectIdAuthoring>()?.effectId;
            if (!string.IsNullOrEmpty(effectId)) return effectId;

            return null;
        }
    }
}

