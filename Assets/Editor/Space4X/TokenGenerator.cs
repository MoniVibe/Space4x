using System.Collections.Generic;
using Space4X.Authoring;
using Space4X.Registry;
using UnityEditor;
using UnityEngine;

namespace Space4X.Editor
{
    /// <summary>
    /// Token generator - one-click generators for civics/aggregate/biome tokens, only emitting prefabs when UI demands markers.
    /// </summary>
    public static class TokenGenerator
    {
        public enum TokenType
        {
            Civic,
            Aggregate,
            Biome,
            Profile
        }

        public static GameObject MaterializeToken(TokenType type, string tokenId, string outputDir)
        {
            var tokenObj = new GameObject(tokenId);

            switch (type)
            {
                case TokenType.Civic:
                    // Add civic-specific components
                    var aggregateId = tokenObj.AddComponent<AggregateIdAuthoring>();
                    aggregateId.aggregateId = tokenId;
                    break;

                case TokenType.Aggregate:
                    aggregateId = tokenObj.AddComponent<AggregateIdAuthoring>();
                    aggregateId.aggregateId = tokenId;
                    var aggregateType = tokenObj.AddComponent<AggregateTypeAuthoring>();
                    aggregateType.aggregateType = AffiliationType.Faction;
                    break;

                case TokenType.Biome:
                    // Biome tokens might have different components
                    break;

                case TokenType.Profile:
                    // Profile tokens
                    break;
            }

            // Add placeholder visual
            PlaceholderPrefabUtility.AddPlaceholderVisual(tokenObj, PrefabType.Aggregate);

            // Save prefab
            var prefabPath = $"{outputDir}/{tokenId}.prefab";
            PrefabUtility.SaveAsPrefabAsset(tokenObj, prefabPath);
            Object.DestroyImmediate(tokenObj);

            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        public static List<GameObject> MaterializeTokens(TokenType type, List<string> tokenIds, string outputDir)
        {
            var prefabs = new List<GameObject>();
            foreach (var tokenId in tokenIds)
            {
                var prefab = MaterializeToken(type, tokenId, outputDir);
                if (prefab != null)
                {
                    prefabs.Add(prefab);
                }
            }
            return prefabs;
        }
    }
}

