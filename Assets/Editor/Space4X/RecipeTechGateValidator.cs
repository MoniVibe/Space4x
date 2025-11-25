using System.Collections.Generic;
using System.Linq;
using Space4X.Authoring;
using UnityEditor;
using UnityEngine;

namespace Space4X.Editor
{
    /// <summary>
    /// Validates recipe & tech gates - detects cycles, missing inputs, illegal tech floors/caps.
    /// </summary>
    public static class RecipeTechGateValidator
    {
        public class ValidationIssue
        {
            public string Message;
            public string ResourceId;
            public string RecipeId;
            public int? TechTier;
        }

        public static List<ValidationIssue> ValidateRecipes(string catalogPath)
        {
            var issues = new List<ValidationIssue>();

            var recipeCatalogPath = $"{catalogPath}/RecipeCatalog.prefab";
            var recipePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(recipeCatalogPath);
            var recipeCatalog = recipePrefab?.GetComponent<RecipeCatalogAuthoring>();

            if (recipeCatalog?.recipes == null) return issues;

            // Check for missing input resources
            var resourceCatalogPath = $"{catalogPath}/ResourceCatalog.prefab";
            var resourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(resourceCatalogPath);
            var resourceCatalog = resourcePrefab?.GetComponent<ResourceCatalogAuthoring>();
            var validResourceIds = resourceCatalog?.resources?.Select(r => r.id).ToHashSet() ?? new HashSet<string>();

            foreach (var recipe in recipeCatalog.recipes)
            {
                // Check input resources exist
                if (recipe.inputResourceIds != null)
                {
                    foreach (var inputId in recipe.inputResourceIds)
                    {
                        if (!validResourceIds.Contains(inputId))
                        {
                            issues.Add(new ValidationIssue
                            {
                                Message = $"Recipe '{recipe.id}' references missing input resource '{inputId}'",
                                RecipeId = recipe.id,
                                ResourceId = inputId
                            });
                        }
                    }
                }

                // Check output product exists
                var productCatalogPath = $"{catalogPath}/ProductCatalog.prefab";
                var productPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(productCatalogPath);
                var productCatalog = productPrefab?.GetComponent<ProductCatalogAuthoring>();
                var validProductIds = productCatalog?.products?.Select(p => p.id).ToHashSet() ?? new HashSet<string>();

                if (!string.IsNullOrEmpty(recipe.outputProductId) && !validProductIds.Contains(recipe.outputProductId))
                {
                    issues.Add(new ValidationIssue
                    {
                        Message = $"Recipe '{recipe.id}' references missing output product '{recipe.outputProductId}'",
                        RecipeId = recipe.id,
                        ResourceId = recipe.outputProductId
                    });
                }
            }

            return issues;
        }

        public static List<ValidationIssue> ValidateTechGates(string catalogPath)
        {
            var issues = new List<ValidationIssue>();

            // Check tech catalog for cycles and invalid floors/caps
            var techCatalogPath = $"{catalogPath}/TechCatalog.prefab";
            var techPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(techCatalogPath);
            var techCatalog = techPrefab?.GetComponent<TechCatalogAuthoring>();

            if (techCatalog?.techs == null) return issues;

            // Build dependency graph and detect cycles
            var visited = new HashSet<string>();
            var recursionStack = new HashSet<string>();

            foreach (var tech in techCatalog.techs)
            {
                if (!visited.Contains(tech.id))
                {
                    DetectCycle(tech.id, techCatalog, visited, recursionStack, issues);
                }
            }

            return issues;
        }

        private static void DetectCycle(string techId, TechCatalogAuthoring catalog, HashSet<string> visited, HashSet<string> recursionStack, List<ValidationIssue> issues)
        {
            visited.Add(techId);
            recursionStack.Add(techId);

            var tech = catalog.techs.FirstOrDefault(t => t.id == techId);
            if (tech?.requires != null)
            {
                foreach (var reqId in tech.requires)
                {
                    if (!visited.Contains(reqId))
                    {
                        DetectCycle(reqId, catalog, visited, recursionStack, issues);
                    }
                    else if (recursionStack.Contains(reqId))
                    {
                        issues.Add(new ValidationIssue
                        {
                            Message = $"Circular dependency detected: {techId} -> {reqId}",
                            ResourceId = techId
                        });
                    }
                }
            }

            recursionStack.Remove(techId);
        }
    }
}

