using System;
using System.Collections.Generic;

namespace Space4X.Systems.Modules.Bom
{
    public readonly struct Space4XModuleRollProbeResult
    {
        public Space4XModuleRollProbeResult(uint seed, int rollCount, uint catalogDigest, uint rollDigest, uint determinismDigest)
        {
            Seed = seed;
            RollCount = rollCount;
            CatalogDigest = catalogDigest;
            RollDigest = rollDigest;
            DeterminismDigest = determinismDigest;
        }

        public uint Seed { get; }
        public int RollCount { get; }
        public uint CatalogDigest { get; }
        public uint RollDigest { get; }
        public uint DeterminismDigest { get; }
    }

    public static class Space4XModuleBomRollProbe
    {
        public const string DeterminismMetricKey = "space4x.modules.catalog_roll.determinism.digest";

        public static bool TryRollDigest100(out Space4XModuleRollProbeResult result, out string error)
        {
            return TryRollDigest(seed: 43101u, count: 100, out result, out error);
        }

        public static bool TryRollDigest(uint seed, int count, out Space4XModuleRollProbeResult result, out string error)
        {
            result = default;
            error = string.Empty;

            if (count <= 0)
            {
                error = "roll count must be positive";
                return false;
            }

            if (!Space4XModuleBomCatalogV0Loader.TryLoadDefault(out var catalog, out _, out error))
            {
                return false;
            }

            var generator = new Space4XModuleBomDeterministicGenerator(catalog);
            var moduleFamilies = new List<Space4XModuleFamilyDefinition>(catalog.moduleFamilies ?? Array.Empty<Space4XModuleFamilyDefinition>());
            moduleFamilies.RemoveAll(x => x == null || string.IsNullOrWhiteSpace(x.id));
            moduleFamilies.Sort((a, b) => string.CompareOrdinal(a.id, b.id));

            if (moduleFamilies.Count == 0)
            {
                error = "catalog has no moduleFamilies";
                return false;
            }

            var catalogDigest = Space4XModuleBomDeterministicGenerator.ComputeCatalogDigest(catalog);
            var rollDigest = 0x811C9DC5u;

            for (var i = 0; i < count; i++)
            {
                var family = moduleFamilies[i % moduleFamilies.Count];
                var mark = 1 + (i % 3);
                var qualityTarget = 0.35f + ((i % 5) * 0.12f);
                var rollSeed = seed + (uint)(i * 7919);

                if (!generator.RollModule(rollSeed, family.id, mark, qualityTarget, out var rollResult))
                {
                    error = $"roll failed for family={family.id} index={i}";
                    return false;
                }

                rollDigest = Mix(rollDigest, rollResult.Digest);
                rollDigest = Mix(rollDigest, (uint)rollResult.Parts.Length);
            }

            var determinismDigest = Mix(catalogDigest, rollDigest);
            result = new Space4XModuleRollProbeResult(seed, count, catalogDigest, rollDigest, determinismDigest);
            return true;
        }

        public static string FormatMetricLine(in Space4XModuleRollProbeResult result)
        {
            return
                $"[Space4XModuleBomV0] seed={result.Seed} roll_count={result.RollCount} " +
                $"space4x.modules.catalog.digest={result.CatalogDigest} " +
                $"space4x.modules.roll.digest={result.RollDigest} " +
                $"{DeterminismMetricKey}={result.DeterminismDigest}";
        }

        private static uint Mix(uint seed, uint value)
        {
            unchecked
            {
                var hash = seed ^ 2166136261u;
                hash ^= value;
                hash *= 16777619u;
                hash ^= hash >> 13;
                hash *= 16777619u;
                return hash;
            }
        }
    }
}
