#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Space4X.Systems.Modules.Bom;
using UnityEditor;
using UnityEngine;

namespace Space4X.EditorTools
{
    public static class Space4XModuleBomDebugCommands
    {
        [MenuItem("Space4X/Modules/BOM V0/Roll 100 Modules Digest")]
        public static void Roll100ModulesDigestMenu()
        {
            Roll100ModulesDigest();
        }

        public static void Roll100ModulesDigest()
        {
            if (!Space4XModuleBomRollProbe.TryRollDigest100(out var result, out var error))
            {
                Debug.LogError($"[Space4XModuleBomV0] roll probe failed: {error}");
                return;
            }

            Debug.Log(Space4XModuleBomRollProbe.FormatMetricLine(result));
        }

        [MenuItem("Space4X/Modules/BOM V0/Roll 200 Modules Preview")]
        public static void Roll200ModulesPreviewMenu()
        {
            Roll200ModulesPreview();
        }

        public static void Roll200ModulesPreview()
        {
            var args = Environment.GetCommandLineArgs();
            var seed = ParseUIntArg(args, "--seed", 43101u);
            var count = ParseIntArg(args, "--count", 200);
            var outArg = ParseStringArg(args, "--out", string.Empty);

            if (!Space4XModuleBomCatalogV0Loader.TryLoadDefault(out var catalog, out var resolvedPath, out var error))
            {
                Debug.LogError($"[Space4XModuleBomV0] roll preview load failed: {error}");
                return;
            }

            var moduleFamilies = BuildModuleFamilyList(catalog);
            if (moduleFamilies.Count == 0)
            {
                Debug.LogError("[Space4XModuleBomV0] roll preview failed: catalog has no module families.");
                return;
            }

            if (count <= 0)
            {
                count = 200;
            }

            var outputPath = ResolveOutputPath(outArg, count);
            var generator = new Space4XModuleBomDeterministicGenerator(catalog);
            var builder = new StringBuilder(64 * 1024);

            builder.AppendLine("# Space4X Module Roll Preview");
            builder.AppendLine();
            builder.AppendLine($"seed: {seed}");
            builder.AppendLine($"count: {count}");
            builder.AppendLine($"catalog: {resolvedPath}");
            builder.AppendLine();
            builder.AppendLine("| rollId | name | family | mark | manufacturer | key stats | digest |");
            builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- |");

            for (var i = 0; i < count; i++)
            {
                var family = moduleFamilies[i % moduleFamilies.Count];
                var mark = 1 + (i % 3);
                var qualityTarget = 0.3f + (i % 7) * 0.1f;
                var rollSeed = unchecked(seed + (uint)(7919 * (i + 1)));

                if (!generator.RollModule(rollSeed, family.id, mark, qualityTarget, out var rollResult))
                {
                    builder.AppendLine($"| error-{i} | roll failed | {SanitizeCell(family.id)} | {mark} | n/a | n/a | n/a |");
                    continue;
                }

                var stats = BuildKeyStatSummary(catalog, family.id, rollResult.ManufacturerId, mark, qualityTarget);
                builder.Append("| ").Append(SanitizeCell(rollResult.RollId));
                builder.Append(" | ").Append(SanitizeCell(rollResult.DisplayName));
                builder.Append(" | ").Append(SanitizeCell(rollResult.ModuleFamilyId));
                builder.Append(" | ").Append(mark.ToString(CultureInfo.InvariantCulture));
                builder.Append(" | ").Append(SanitizeCell(rollResult.ManufacturerId));
                builder.Append(" | ").Append(SanitizeCell(stats));
                builder.Append(" | ").Append(rollResult.Digest.ToString("X8", CultureInfo.InvariantCulture));
                builder.AppendLine(" |");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? "Temp");
            File.WriteAllText(outputPath, builder.ToString());
            Debug.Log($"[Space4XModuleBomV0] wrote roll preview count={count} seed={seed} path={outputPath}");
        }

        private static string ResolveOutputPath(string outArg, int count)
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            if (string.IsNullOrWhiteSpace(outArg))
            {
                return Path.Combine(projectRoot, "Temp", "Reports", $"space4x_module_roll_preview_{count}.md");
            }

            if (Path.IsPathRooted(outArg))
            {
                return outArg;
            }

            var normalized = outArg.Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(projectRoot, normalized);
        }

        private static List<Space4XModuleFamilyDefinition> BuildModuleFamilyList(Space4XModuleBomCatalogV0 catalog)
        {
            var list = new List<Space4XModuleFamilyDefinition>(catalog.moduleFamilies?.Length ?? 0);
            var families = catalog.moduleFamilies ?? Array.Empty<Space4XModuleFamilyDefinition>();
            for (var i = 0; i < families.Length; i++)
            {
                var item = families[i];
                if (item == null || string.IsNullOrWhiteSpace(item.id))
                {
                    continue;
                }

                list.Add(item);
            }

            list.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            return list;
        }

        private static string BuildKeyStatSummary(Space4XModuleBomCatalogV0 catalog, string familyId, string manufacturerId, int mark, float qualityTarget)
        {
            var moduleFamily = FindModuleFamily(catalog, familyId);
            if (moduleFamily == null)
            {
                return "n/a";
            }

            var markStats = FindMarkStats(moduleFamily, mark);
            if (markStats == null || markStats.Length == 0)
            {
                return "n/a";
            }

            var multipliers = FindManufacturerMultipliers(catalog, manufacturerId);
            var qualityScalar = 0.92f + Mathf.Clamp01(qualityTarget) * 0.16f;
            var stats = new List<string>(markStats.Length);

            for (var i = 0; i < markStats.Length; i++)
            {
                var stat = markStats[i];
                if (stat == null || string.IsNullOrWhiteSpace(stat.key))
                {
                    continue;
                }

                var value = stat.value * qualityScalar;
                if (multipliers.TryGetValue(stat.key, out var multiplier))
                {
                    value *= multiplier;
                }

                stats.Add($"{stat.key}={value:0.00}");
            }

            if (stats.Count == 0)
            {
                return "n/a";
            }

            stats.Sort(StringComparer.OrdinalIgnoreCase);
            if (stats.Count > 3)
            {
                stats.RemoveRange(3, stats.Count - 3);
            }

            return string.Join("; ", stats);
        }

        private static Dictionary<string, float> FindManufacturerMultipliers(Space4XModuleBomCatalogV0 catalog, string manufacturerId)
        {
            var result = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            var manufacturers = catalog.manufacturers ?? Array.Empty<Space4XManufacturerDefinition>();
            for (var i = 0; i < manufacturers.Length; i++)
            {
                var manufacturer = manufacturers[i];
                if (manufacturer == null || !string.Equals(manufacturer.id, manufacturerId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var stats = manufacturer.statMultipliers ?? Array.Empty<Space4XStatValue>();
                for (var s = 0; s < stats.Length; s++)
                {
                    var stat = stats[s];
                    if (stat == null || string.IsNullOrWhiteSpace(stat.key))
                    {
                        continue;
                    }

                    result[stat.key] = stat.value;
                }

                break;
            }

            return result;
        }

        private static Space4XModuleFamilyDefinition FindModuleFamily(Space4XModuleBomCatalogV0 catalog, string familyId)
        {
            var families = catalog.moduleFamilies ?? Array.Empty<Space4XModuleFamilyDefinition>();
            for (var i = 0; i < families.Length; i++)
            {
                var family = families[i];
                if (family != null && string.Equals(family.id, familyId, StringComparison.OrdinalIgnoreCase))
                {
                    return family;
                }
            }

            return null;
        }

        private static Space4XStatValue[] FindMarkStats(Space4XModuleFamilyDefinition family, int mark)
        {
            var bands = family.baseStatsByMark ?? Array.Empty<Space4XMarkStatBand>();
            for (var i = 0; i < bands.Length; i++)
            {
                var band = bands[i];
                if (band != null && band.mark == mark)
                {
                    return band.stats ?? Array.Empty<Space4XStatValue>();
                }
            }

            return Array.Empty<Space4XStatValue>();
        }

        private static uint ParseUIntArg(string[] args, string key, uint fallback)
        {
            var value = ParseStringArg(args, key, string.Empty);
            return uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : fallback;
        }

        private static int ParseIntArg(string[] args, string key, int fallback)
        {
            var value = ParseStringArg(args, key, string.Empty);
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : fallback;
        }

        private static string ParseStringArg(string[] args, string key, string fallback)
        {
            if (args == null || args.Length == 0 || string.IsNullOrWhiteSpace(key))
            {
                return fallback;
            }

            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return fallback;
        }

        private static string SanitizeCell(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "-";
            }

            return value.Replace("|", "/").Replace("\r", " ").Replace("\n", " ");
        }
    }
}
#endif
