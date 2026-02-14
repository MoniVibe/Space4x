using System;
using System.Collections.Generic;
using UnityEngine;

namespace Space4X.Systems.Modules.Bom
{
    public readonly struct Space4XPartRollResult
    {
        public Space4XPartRollResult(string partId, string family, string manufacturer, string qualityTier, float qualityInput, uint rollHash)
        {
            PartId = partId;
            Family = family;
            Manufacturer = manufacturer;
            QualityTier = qualityTier;
            QualityInput = qualityInput;
            RollHash = rollHash;
        }

        public string PartId { get; }
        public string Family { get; }
        public string Manufacturer { get; }
        public string QualityTier { get; }
        public float QualityInput { get; }
        public uint RollHash { get; }
    }

    public readonly struct Space4XModulePartRoll
    {
        public Space4XModulePartRoll(string slotId, int quantityIndex, Space4XPartRollResult part)
        {
            SlotId = slotId;
            QuantityIndex = quantityIndex;
            Part = part;
        }

        public string SlotId { get; }
        public int QuantityIndex { get; }
        public Space4XPartRollResult Part { get; }
    }

    public sealed class Space4XModuleRollResult
    {
        public string RollId = string.Empty;
        public string DisplayName = string.Empty;
        public string ModuleFamilyId = string.Empty;
        public string ManufacturerId = string.Empty;
        public int Mark = 1;
        public float QualityTarget;
        public uint Digest;
        public Space4XModulePartRoll[] Parts = Array.Empty<Space4XModulePartRoll>();
    }

    public sealed class Space4XModuleBomDeterministicGenerator
    {
        private static readonly string[] QualityPrefixes = { "Rugged", "Field", "Prime" };
        private static readonly string[] NameSuffixes = { "Array", "Lance", "Spindle", "Bastion", "Crown", "Matrix" };

        private readonly Space4XModuleBomCatalogV0 _catalog;
        private readonly Dictionary<string, Space4XModuleFamilyDefinition> _moduleFamilies;
        private readonly Dictionary<string, List<Space4XPartDefinition>> _partsByFamily;

        public Space4XModuleBomDeterministicGenerator(Space4XModuleBomCatalogV0 catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _moduleFamilies = new Dictionary<string, Space4XModuleFamilyDefinition>(StringComparer.OrdinalIgnoreCase);
            _partsByFamily = new Dictionary<string, List<Space4XPartDefinition>>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < _catalog.moduleFamilies.Length; i++)
            {
                var module = _catalog.moduleFamilies[i];
                if (module == null || string.IsNullOrWhiteSpace(module.id))
                {
                    continue;
                }

                if (!_moduleFamilies.ContainsKey(module.id))
                {
                    _moduleFamilies.Add(module.id, module);
                }
            }

            for (var i = 0; i < _catalog.parts.Length; i++)
            {
                var part = _catalog.parts[i];
                if (part == null || string.IsNullOrWhiteSpace(part.family) || string.IsNullOrWhiteSpace(part.id))
                {
                    continue;
                }

                if (!_partsByFamily.TryGetValue(part.family, out var list))
                {
                    list = new List<Space4XPartDefinition>(8);
                    _partsByFamily.Add(part.family, list);
                }

                list.Add(part);
            }

            foreach (var entry in _partsByFamily)
            {
                entry.Value.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            }
        }

        public bool RollPart(uint seed, string family, string manufacturerBias, int mark, float qualityTarget, out Space4XPartRollResult result)
        {
            result = default;
            if (!_partsByFamily.TryGetValue(family, out var allParts) || allParts.Count == 0)
            {
                return false;
            }

            var candidates = new List<Space4XPartDefinition>(allParts.Count);
            for (var i = 0; i < allParts.Count; i++)
            {
                var part = allParts[i];
                if (mark < part.markMin || mark > part.markMax)
                {
                    continue;
                }

                candidates.Add(part);
            }

            if (candidates.Count == 0)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(manufacturerBias))
            {
                var preferred = new List<Space4XPartDefinition>(candidates.Count);
                for (var i = 0; i < candidates.Count; i++)
                {
                    if (string.Equals(candidates[i].manufacturer, manufacturerBias, StringComparison.OrdinalIgnoreCase))
                    {
                        preferred.Add(candidates[i]);
                    }
                }

                if (preferred.Count > 0)
                {
                    candidates = preferred;
                }
            }

            candidates.Sort((a, b) => string.CompareOrdinal(a.id, b.id));

            var rollHash = Mix(seed, family);
            rollHash = Mix(rollHash, (uint)mark);
            rollHash = Mix(rollHash, manufacturerBias);
            var pick = (int)(rollHash % (uint)candidates.Count);
            var selected = candidates[pick];

            var qualityHash = Mix(rollHash, selected.id);
            var qualityOffset = ((qualityHash & 0x7FFu) / 1024f - 1f) * 0.08f;
            var qualityInput = Mathf.Clamp01(qualityTarget + qualityOffset);
            var qualityTier = ResolveQualityTier(selected.qualityTierRules, qualityInput);

            result = new Space4XPartRollResult(
                selected.id,
                selected.family,
                selected.manufacturer,
                qualityTier,
                qualityInput,
                rollHash);
            return true;
        }

        public bool RollModule(uint seed, string moduleFamilyId, int mark, float qualityTarget, out Space4XModuleRollResult result)
        {
            result = null;
            if (!_moduleFamilies.TryGetValue(moduleFamilyId, out var moduleFamily))
            {
                return false;
            }

            var manufacturer = ResolveManufacturer(moduleFamily, seed, mark);
            var lines = CloneAndSortBom(moduleFamily.bomTemplate);
            var rolledParts = new List<Space4XModulePartRoll>(8);
            var digest = Mix(0x811C9DC5u, moduleFamily.id);
            digest = Mix(digest, manufacturer);
            digest = Mix(digest, (uint)mark);

            var partOrdinal = 0u;
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var quantity = Math.Max(1, line.quantity);
                for (var q = 0; q < quantity; q++)
                {
                    var partSeed = Mix(seed, partOrdinal + 0x9E3779B9u);
                    partSeed = Mix(partSeed, line.slotId);
                    if (!RollPart(partSeed, line.requiredFamily, manufacturer, mark, qualityTarget, out var part))
                    {
                        if (!RollPart(partSeed, line.requiredFamily, string.Empty, mark, qualityTarget, out part))
                        {
                            return false;
                        }
                    }

                    rolledParts.Add(new Space4XModulePartRoll(line.slotId, q, part));
                    digest = Mix(digest, line.slotId);
                    digest = Mix(digest, part.PartId);
                    digest = Mix(digest, Quantize(part.QualityInput));
                    partOrdinal++;
                }
            }

            var namePrefix = ResolvePrefix(qualityTarget);
            var suffixRoll = Mix(seed, moduleFamily.id);
            suffixRoll = Mix(suffixRoll, (uint)mark);
            var suffix = NameSuffixes[suffixRoll % (uint)NameSuffixes.Length];
            var modelName = string.IsNullOrWhiteSpace(moduleFamily.model) ? moduleFamily.id : moduleFamily.model;
            var displayName = $"{manufacturer} Mk{mark} {modelName} {namePrefix} {suffix}";
            var rollId = $"{manufacturer}-{mark}-{digest:X8}";

            result = new Space4XModuleRollResult
            {
                RollId = rollId,
                DisplayName = displayName,
                ModuleFamilyId = moduleFamily.id,
                ManufacturerId = manufacturer,
                Mark = mark,
                QualityTarget = qualityTarget,
                Digest = digest,
                Parts = rolledParts.ToArray()
            };
            return true;
        }

        public static uint ComputeCatalogDigest(Space4XModuleBomCatalogV0 catalog)
        {
            if (catalog == null)
            {
                return 0u;
            }

            var digest = 0x811C9DC5u;

            var parts = new List<Space4XPartDefinition>(catalog.parts ?? Array.Empty<Space4XPartDefinition>());
            parts.Sort((a, b) => string.CompareOrdinal(a?.id, b?.id));
            for (var i = 0; i < parts.Count; i++)
            {
                var part = parts[i];
                if (part == null)
                {
                    continue;
                }

                digest = Mix(digest, part.id);
                digest = Mix(digest, part.family);
                digest = Mix(digest, part.manufacturer);
                digest = Mix(digest, (uint)part.markMin);
                digest = Mix(digest, (uint)part.markMax);

                var stats = part.baseStats ?? Array.Empty<Space4XStatValue>();
                for (var s = 0; s < stats.Length; s++)
                {
                    digest = Mix(digest, stats[s]?.key);
                    digest = Mix(digest, Quantize(stats[s]?.value ?? 0f));
                }
            }

            var modules = new List<Space4XModuleFamilyDefinition>(catalog.moduleFamilies ?? Array.Empty<Space4XModuleFamilyDefinition>());
            modules.Sort((a, b) => string.CompareOrdinal(a?.id, b?.id));
            for (var i = 0; i < modules.Count; i++)
            {
                var module = modules[i];
                if (module == null)
                {
                    continue;
                }

                digest = Mix(digest, module.id);
                digest = Mix(digest, module.model);

                var bom = module.bomTemplate ?? Array.Empty<Space4XModuleBomLine>();
                for (var b = 0; b < bom.Length; b++)
                {
                    digest = Mix(digest, bom[b]?.slotId);
                    digest = Mix(digest, bom[b]?.requiredFamily);
                    digest = Mix(digest, (uint)(bom[b]?.quantity ?? 0));
                }
            }

            return digest;
        }

        private string ResolveManufacturer(Space4XModuleFamilyDefinition moduleFamily, uint seed, int mark)
        {
            var candidates = new List<string>(8);
            var allowed = moduleFamily.allowedManufacturers ?? Array.Empty<string>();
            for (var i = 0; i < allowed.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(allowed[i]))
                {
                    candidates.Add(allowed[i]);
                }
            }

            if (candidates.Count == 0)
            {
                for (var i = 0; i < _catalog.manufacturers.Length; i++)
                {
                    var item = _catalog.manufacturers[i];
                    if (item != null && !string.IsNullOrWhiteSpace(item.id))
                    {
                        candidates.Add(item.id);
                    }
                }
            }

            if (candidates.Count == 0)
            {
                return "Generic";
            }

            candidates.Sort(string.CompareOrdinal);
            var hash = Mix(seed, moduleFamily.id);
            hash = Mix(hash, (uint)mark);
            return candidates[(int)(hash % (uint)candidates.Count)];
        }

        private static Space4XModuleBomLine[] CloneAndSortBom(Space4XModuleBomLine[] bomTemplate)
        {
            if (bomTemplate == null || bomTemplate.Length == 0)
            {
                return Array.Empty<Space4XModuleBomLine>();
            }

            var clone = new Space4XModuleBomLine[bomTemplate.Length];
            Array.Copy(bomTemplate, clone, bomTemplate.Length);
            Array.Sort(clone, CompareBomLines);
            return clone;
        }

        private static int CompareBomLines(Space4XModuleBomLine x, Space4XModuleBomLine y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            if (x == null)
            {
                return -1;
            }

            if (y == null)
            {
                return 1;
            }

            var slot = string.CompareOrdinal(x.slotId, y.slotId);
            if (slot != 0)
            {
                return slot;
            }

            var family = string.CompareOrdinal(x.requiredFamily, y.requiredFamily);
            if (family != 0)
            {
                return family;
            }

            return x.quantity.CompareTo(y.quantity);
        }

        private static string ResolveQualityTier(Space4XQualityTierRule[] rules, float qualityInput)
        {
            if (rules == null || rules.Length == 0)
            {
                return "Standard";
            }

            for (var i = 0; i < rules.Length; i++)
            {
                var rule = rules[i];
                if (rule == null)
                {
                    continue;
                }

                if (qualityInput >= rule.min && qualityInput <= rule.max)
                {
                    return string.IsNullOrWhiteSpace(rule.tier) ? "Standard" : rule.tier;
                }
            }

            return string.IsNullOrWhiteSpace(rules[0].tier) ? "Standard" : rules[0].tier;
        }

        private static string ResolvePrefix(float qualityTarget)
        {
            if (qualityTarget >= 0.75f)
            {
                return QualityPrefixes[2];
            }

            if (qualityTarget >= 0.45f)
            {
                return QualityPrefixes[1];
            }

            return QualityPrefixes[0];
        }

        private static uint Quantize(float value)
        {
            var clamped = Mathf.Clamp(value, -10000f, 10000f);
            return (uint)Mathf.RoundToInt(clamped * 1000f);
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

        private static uint Mix(uint seed, string value)
        {
            unchecked
            {
                var hash = seed ^ 2166136261u;
                if (!string.IsNullOrEmpty(value))
                {
                    for (var i = 0; i < value.Length; i++)
                    {
                        hash ^= value[i];
                        hash *= 16777619u;
                    }
                }

                hash ^= hash >> 13;
                hash *= 16777619u;
                return hash;
            }
        }
    }
}
