using System.Collections.Generic;
using System.Linq;
using Space4X.Authoring;
using UnityEditor;
using UnityEngine;

namespace Space4X.Editor
{
    /// <summary>
    /// Content budgets - derives runtime budget caps from catalogs; asserts in editor/test harness.
    /// </summary>
    public static class ContentBudgets
    {
        [System.Serializable]
        public class BudgetCaps
        {
            public int MaxCompanionsPerFrame;
            public int MaxPoolSize;
            public int MaxActivePrefabs;
            public Dictionary<string, int> CategoryLimits = new Dictionary<string, int>();
        }

        public static BudgetCaps GenerateBudgets(string catalogPath)
        {
            var budgets = new BudgetCaps();

            // Derive budgets from catalog sizes
            var catalogCounts = PrefabMaker.CountCatalogEntries(catalogPath);

            // Set pool sizes based on catalog counts (e.g., 2x catalog size)
            budgets.MaxPoolSize = catalogCounts.Values.Sum() * 2;
            budgets.MaxActivePrefabs = catalogCounts.Values.Sum();

            // Set category-specific limits
            foreach (var kvp in catalogCounts)
            {
                budgets.CategoryLimits[kvp.Key] = kvp.Value * 3; // 3x for variants/spawning
            }

            // Companions per frame (individuals)
            if (catalogCounts.TryGetValue("Individual", out var individualCount))
            {
                budgets.MaxCompanionsPerFrame = individualCount / 10; // Conservative estimate
            }
            else
            {
                budgets.MaxCompanionsPerFrame = 10; // Default
            }

            return budgets;
        }

        public static void AssertBudgets(BudgetCaps budgets, int currentCompanions, int currentPoolSize, int currentActivePrefabs)
        {
            if (currentCompanions > budgets.MaxCompanionsPerFrame)
            {
                Debug.LogError($"Budget exceeded: Companions per frame ({currentCompanions} > {budgets.MaxCompanionsPerFrame})");
            }

            if (currentPoolSize > budgets.MaxPoolSize)
            {
                Debug.LogError($"Budget exceeded: Pool size ({currentPoolSize} > {budgets.MaxPoolSize})");
            }

            if (currentActivePrefabs > budgets.MaxActivePrefabs)
            {
                Debug.LogError($"Budget exceeded: Active prefabs ({currentActivePrefabs} > {budgets.MaxActivePrefabs})");
            }
        }
    }
}

