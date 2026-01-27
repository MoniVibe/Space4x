using System;
using PureDOTS.Runtime.Components;
using Unity.Entities;

namespace PureDOTS.Editor
{
    /// <summary>
    /// Lightweight budget validator used by edit mode tests.
    /// </summary>
    public static class PureDotsBudgetValidator
    {
        public const float FixedTickBudgetMs = 33.3f;
        public const int SnapshotRingBudget = HistorySettingsDefaults.DefaultMaxGlobalSnapshots;
        public const uint PresentationSpawnCap = 1000;

        public struct BudgetResults
        {
            public float FixedTickMs;
            public float MemoryMB;
            public int SnapshotRingSize;
            public uint SpawnsPerFrame;
            public bool AllBudgetsMet;
        }

        public static BudgetResults ValidateBudgets(World world)
        {
            var results = new BudgetResults();
            if (world == null || !world.IsCreated)
            {
                return results;
            }

            results.FixedTickMs = ResolveFixedTickMs(world);
            results.MemoryMB = ResolveMemoryBudget(world);
            results.SnapshotRingSize = ResolveSnapshotRingSize(world);
            results.SpawnsPerFrame = ResolveSpawnsPerFrame(world);
            results.AllBudgetsMet = results.FixedTickMs <= FixedTickBudgetMs
                                    && results.SnapshotRingSize <= SnapshotRingBudget
                                    && results.SpawnsPerFrame <= PresentationSpawnCap;

            return results;
        }

        public static void AssertBudgetsMet(World world, string context)
        {
            var results = ValidateBudgets(world);
            if (results.AllBudgetsMet)
            {
                return;
            }

            throw new InvalidOperationException(
                $"Budget validation failed ({context}). FixedTickMs={results.FixedTickMs:F2}, " +
                $"SnapshotRingSize={results.SnapshotRingSize}, SpawnsPerFrame={results.SpawnsPerFrame}");
        }

        private static float ResolveFixedTickMs(World world)
        {
            var query = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<TimeState>());
            if (!query.IsEmptyIgnoreFilter)
            {
                return query.GetSingleton<TimeState>().FixedDeltaTime * 1000f;
            }

            return 0f;
        }

        private static float ResolveMemoryBudget(World world)
        {
            var query = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<HistorySettings>());
            if (!query.IsEmptyIgnoreFilter)
            {
                return query.GetSingleton<HistorySettings>().MemoryBudgetMegabytes;
            }

            return 0f;
        }

        private static int ResolveSnapshotRingSize(World world)
        {
            var query = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<HistorySettings>());
            if (!query.IsEmptyIgnoreFilter)
            {
                return query.GetSingleton<HistorySettings>().MaxGlobalSnapshots;
            }

            return 0;
        }

        private static uint ResolveSpawnsPerFrame(World world)
        {
            var query = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<PresentationPoolStats>());
            if (!query.IsEmptyIgnoreFilter)
            {
                return query.GetSingleton<PresentationPoolStats>().SpawnedThisFrame;
            }

            return 0;
        }
    }
}
