using System.Collections.Generic;
using Space4X.Authoring;
using Space4X.Registry;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityDebug = UnityEngine.Debug;

namespace Space4X.Editor
{
    using Debug = UnityEngine.Debug;

    public static class AggregateTokenPrefabGenerator
    {
        private const string PrefabBasePath = "Assets/Prefabs/Space4X/Aggregates";

        public static void MaterializeTokenPrefabs(
            List<uint> aggregateIds, 
            Dictionary<uint, ComposedAggregateSpec> combos,
            bool dryRun)
        {
            if (combos == null || aggregateIds == null) return;

            EnsureDirectory();

            foreach (var aggregateId in aggregateIds)
            {
                if (!combos.ContainsKey(aggregateId))
                {
                    UnityDebug.LogWarning($"Aggregate ID {aggregateId} not found in combo table");
                    continue;
                }

                var combo = combos[aggregateId];
                var prefabPath = $"{PrefabBasePath}/Aggregate_{aggregateId:X8}.prefab";

                if (dryRun)
                {
                    UnityDebug.Log($"DRY RUN: Would create token prefab at {prefabPath}");
                    continue;
                }

                var existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                GameObject tokenObj;

                if (existingPrefab != null)
                {
                    tokenObj = PrefabUtility.LoadPrefabContents(prefabPath);
                }
                else
                {
                    tokenObj = new GameObject($"Aggregate_{aggregateId:X8}");
                }

                // Add AggregateIdAuthoring with the composed ID
                var aggregateIdComp = tokenObj.GetComponent<AggregateIdAuthoring>();
                if (aggregateIdComp == null)
                {
                    aggregateIdComp = tokenObj.AddComponent<AggregateIdAuthoring>();
                }
                aggregateIdComp.aggregateId = aggregateId.ToString("X8");
                aggregateIdComp.alignment = 0; // These are resolved from combo
                aggregateIdComp.outlook = 0;
                aggregateIdComp.policy = 0;

                // Add style tokens from theme
                var styleTokens = tokenObj.GetComponent<StyleTokensAuthoring>();
                if (styleTokens == null)
                {
                    styleTokens = tokenObj.AddComponent<StyleTokensAuthoring>();
                }
                styleTokens.palette = combo.StyleTokens.Palette;
                styleTokens.roughness = combo.StyleTokens.Roughness;
                styleTokens.pattern = combo.StyleTokens.Pattern;

                // Add placeholder visual
                PlaceholderPrefabUtility.AddPlaceholderVisual(tokenObj, PrefabType.Aggregate);

                // Save prefab
                if (existingPrefab != null)
                {
                    PrefabUtility.SaveAsPrefabAsset(tokenObj, prefabPath);
                    PrefabUtility.UnloadPrefabContents(tokenObj);
                }
                else
                {
                    PrefabUtility.SaveAsPrefabAsset(tokenObj, prefabPath);
                    Object.DestroyImmediate(tokenObj);
                }
            }
        }

        private static void EnsureDirectory()
        {
            if (!AssetDatabase.IsValidFolder(PrefabBasePath))
            {
                var parts = PrefabBasePath.Split('/');
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
}

