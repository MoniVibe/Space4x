using System;
using System.Collections.Generic;
using System.IO;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Modules;
using PureDOTS.Systems;
using Space4X.Registry;
using Space4X.Runtime;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using RegistryModuleSpec = Space4X.Registry.ModuleSpec;

namespace Space4X.Systems.Modules
{
    public enum ModuleDerivedStatId : ushort
    {
        Damage = 0,
        FireRate = 1,
        Range = 2,
        EnergyCost = 3,
        HeatCost = 4,
        PowerOutput = 5,
        HeatCapacity = 6,
        HeatDissipation = 7,
        Thrust = 8,
        ShieldCapacity = 9,
        SensorRange = 10,
        Mass = 11,
        DroneCapacity = 12
    }

    public enum ModuleBlueprintOpKind : byte
    {
        None = 0,
        AddStat = 1,
        MulStat = 2,
        AddTag = 3,
        RemoveTag = 4,
        ConvertDamage = 5,
        ReplaceAttackFamily = 6,
        AddProc = 7
    }

    public struct ModuleBlueprintRef : IComponentData
    {
        public FixedString64Bytes ManufacturerId;
        public FixedString64Bytes BlueprintId;
        public uint StableHash;
    }

    [InternalBufferCapacity(6)]
    public struct ModuleBlueprintPartId : IBufferElementData
    {
        public FixedString64Bytes PartId;
        public FixedString64Bytes SlotType;
    }

    [InternalBufferCapacity(16)]
    public struct ModuleDerivedStat : IBufferElementData
    {
        public ModuleDerivedStatId StatId;
        public float Value;
    }

    [InternalBufferCapacity(16)]
    public struct ModuleDerivedTag : IBufferElementData
    {
        public FixedString64Bytes TagId;
        public float Value;
    }

    [InternalBufferCapacity(8)]
    public struct ModuleDerivedEffectOp : IBufferElementData
    {
        public ModuleBlueprintOpKind OpKind;
        public FixedString64Bytes EffectId;
        public float Chance;
        public float ProcCoefficient;
        public Space4XDamageType FromDamageType;
        public Space4XDamageType ToDamageType;
        public float ConversionPct;
        public WeaponFamily FromFamily;
        public WeaponFamily ToFamily;
    }

    public struct ModuleDerivedDigest : IComponentData
    {
        public uint Value;
    }

    public struct ModuleDerivedWeaponProfile : IComponentData
    {
        public float DamageScalar;
        public float FireRateScalar;
        public float RangeScalar;
        public float EnergyCostScalar;
        public float HeatCostScalar;
        public WeaponFamily AttackFamily;
        public Space4XDamageType DamageType;
        public WeaponDelivery Delivery;
    }

    public struct ModuleDerivedReactorProfile : IComponentData
    {
        public float PowerOutputMW;
        public float HeatCapacity;
        public float HeatDissipation;
        public float MassTons;
    }

    public struct ModuleDerivedHangarProfile : IComponentData
    {
        public float DroneCapacity;
        public WeaponFamily DroneAttackFamily;
    }

    public struct CarrierDerivedHangarProfile : IComponentData
    {
        public float DroneCapacity;
        public WeaponFamily DroneAttackFamily;
    }

    public struct ModuleBlueprintCatalogStatus : IComponentData
    {
        public byte Loaded;
        public byte HasErrors;
        public uint CatalogDigest;
        public ushort ValidationErrorCount;
    }

    [InternalBufferCapacity(8)]
    public struct ModuleBlueprintValidationError : IBufferElementData
    {
        public FixedString128Bytes Message;
    }

    [InternalBufferCapacity(12)]
    public struct ModuleBlueprintRunPerkOp : IBufferElementData
    {
        public ModuleBlueprintOpKind OpKind;
        public ModuleDerivedStatId StatId;
        public float Value;
        public FixedString64Bytes TagId;
        public Space4XDamageType FromDamageType;
        public Space4XDamageType ToDamageType;
        public WeaponFamily FromFamily;
        public WeaponFamily ToFamily;
        public FixedString64Bytes EffectId;
        public float Chance;
        public float ProcCoefficient;
    }

    [Serializable]
    internal sealed class ModuleBlueprintCatalogJson
    {
        public int schemaVersion = 1;
        public ModuleBlueprintSlotTypeJson[] slotTypes = Array.Empty<ModuleBlueprintSlotTypeJson>();
        public ModuleBlueprintManufacturerJson[] manufacturers = Array.Empty<ModuleBlueprintManufacturerJson>();
        public ModuleBlueprintPartJson[] parts = Array.Empty<ModuleBlueprintPartJson>();
        public ModuleBlueprintSpecJson[] blueprints = Array.Empty<ModuleBlueprintSpecJson>();
    }

    [Serializable]
    internal sealed class ModuleBlueprintSlotTypeJson
    {
        public string slotType = string.Empty;
        public bool allowMultiple;
    }

    [Serializable]
    internal sealed class ModuleBlueprintManufacturerJson
    {
        public string id = string.Empty;
        public string displayName = string.Empty;
        public string signatureRuleId = string.Empty;
        public ModuleBlueprintStatModJson[] statMods = Array.Empty<ModuleBlueprintStatModJson>();
        public ModuleBlueprintTagOpJson[] tagOps = Array.Empty<ModuleBlueprintTagOpJson>();
    }

    [Serializable]
    internal sealed class ModuleBlueprintPartJson
    {
        public string id = string.Empty;
        public string slotType = string.Empty;
        public ModuleBlueprintStatModJson[] statMods = Array.Empty<ModuleBlueprintStatModJson>();
        public ModuleBlueprintTagOpJson[] tagOps = Array.Empty<ModuleBlueprintTagOpJson>();
        public ModuleBlueprintEffectOpJson[] effectOps = Array.Empty<ModuleBlueprintEffectOpJson>();
    }

    [Serializable]
    internal sealed class ModuleBlueprintSpecJson
    {
        public string blueprintId = string.Empty;
        public string baseModuleId = string.Empty;
        public string manufacturerId = string.Empty;
        public string[] parts = Array.Empty<string>();
        public string rarity = string.Empty;
        public int tier;
    }

    [Serializable]
    internal sealed class ModuleBlueprintStatModJson
    {
        public string op = string.Empty;
        public string statId = string.Empty;
        public float value;
    }

    [Serializable]
    internal sealed class ModuleBlueprintTagOpJson
    {
        public string op = string.Empty;
        public string tagId = string.Empty;
        public float value;
    }

    [Serializable]
    internal sealed class ModuleBlueprintEffectOpJson
    {
        public string op = string.Empty;
        public string effectId = string.Empty;
        public string fromDamageType = string.Empty;
        public string toDamageType = string.Empty;
        public float pct;
        public string fromFamily = string.Empty;
        public string toFamily = string.Empty;
        public float chance;
        public float procCoef;
    }

    internal readonly struct ModuleBlueprintResolvedOp
    {
        public readonly ModuleBlueprintOpKind Kind;
        public readonly ModuleDerivedStatId StatId;
        public readonly float Value;
        public readonly FixedString64Bytes TagId;
        public readonly Space4XDamageType FromDamageType;
        public readonly Space4XDamageType ToDamageType;
        public readonly WeaponFamily FromFamily;
        public readonly WeaponFamily ToFamily;
        public readonly FixedString64Bytes EffectId;
        public readonly float Chance;
        public readonly float ProcCoefficient;

        public ModuleBlueprintResolvedOp(
            ModuleBlueprintOpKind kind,
            ModuleDerivedStatId statId,
            float value,
            in FixedString64Bytes tagId,
            Space4XDamageType fromDamageType,
            Space4XDamageType toDamageType,
            WeaponFamily fromFamily,
            WeaponFamily toFamily,
            in FixedString64Bytes effectId,
            float chance,
            float procCoefficient)
        {
            Kind = kind;
            StatId = statId;
            Value = value;
            TagId = tagId;
            FromDamageType = fromDamageType;
            ToDamageType = toDamageType;
            FromFamily = fromFamily;
            ToFamily = toFamily;
            EffectId = effectId;
            Chance = chance;
            ProcCoefficient = procCoefficient;
        }
    }

    internal sealed class ModuleBlueprintManufacturerRuntime
    {
        public string Id = string.Empty;
        public FixedString64Bytes IdFixed;
        public string DisplayName = string.Empty;
        public FixedString64Bytes SignatureRuleId;
        public readonly List<ModuleBlueprintResolvedOp> Ops = new List<ModuleBlueprintResolvedOp>(8);
    }

    internal sealed class ModuleBlueprintPartRuntime
    {
        public string Id = string.Empty;
        public FixedString64Bytes IdFixed;
        public string SlotType = string.Empty;
        public FixedString64Bytes SlotTypeFixed;
        public readonly List<ModuleBlueprintResolvedOp> Ops = new List<ModuleBlueprintResolvedOp>(12);
    }

    internal sealed class ModuleBlueprintSpecRuntime
    {
        public string BlueprintId = string.Empty;
        public FixedString64Bytes BlueprintIdFixed;
        public string BaseModuleId = string.Empty;
        public FixedString64Bytes BaseModuleIdFixed;
        public string ManufacturerId = string.Empty;
        public FixedString64Bytes ManufacturerIdFixed;
        public string[] Parts = Array.Empty<string>();
    }

    internal sealed class ModuleBlueprintCatalogRuntime
    {
        public readonly Dictionary<string, bool> SlotAllowMultiple = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, ModuleBlueprintManufacturerRuntime> Manufacturers = new Dictionary<string, ModuleBlueprintManufacturerRuntime>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, ModuleBlueprintPartRuntime> Parts = new Dictionary<string, ModuleBlueprintPartRuntime>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, ModuleBlueprintSpecRuntime> Blueprints = new Dictionary<string, ModuleBlueprintSpecRuntime>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, List<ModuleBlueprintSpecRuntime>> BlueprintsByBaseModule = new Dictionary<string, List<ModuleBlueprintSpecRuntime>>(StringComparer.OrdinalIgnoreCase);
        public readonly List<string> ValidationErrors = new List<string>(16);
        public uint CatalogDigest;
    }

    internal static partial class Space4XModuleBlueprintCatalogStore
    {
        public const string DefaultCatalogRelativePath = "Assets/Data/Catalogs/space4x_module_blueprint_catalog_v0.json";
        public const string DefaultCatalogResourcesPath = "Catalogs/space4x_module_blueprint_catalog_v0";

        private static ModuleBlueprintCatalogRuntime s_runtime;
        private static byte s_loaded;

        public static ModuleBlueprintCatalogRuntime Runtime => s_runtime;

        public static bool EnsureLoaded()
        {
            if (s_loaded != 0)
            {
                return s_runtime != null;
            }

            s_loaded = 1;
            var runtime = new ModuleBlueprintCatalogRuntime();

            if (!TryLoadCatalogJson(out var json, out var source))
            {
                runtime.ValidationErrors.Add($"module blueprint catalog missing: Resources/{DefaultCatalogResourcesPath}.json");
                BuildBuiltinFallback(runtime);
                s_runtime = runtime;
                return true;
            }

            try
            {
                var parsed = JsonUtility.FromJson<ModuleBlueprintCatalogJson>(json);
                if (parsed == null)
                {
                    runtime.ValidationErrors.Add($"module blueprint catalog parse returned null ({source})");
                    BuildBuiltinFallback(runtime);
                    s_runtime = runtime;
                    return true;
                }

                PopulateRuntime(parsed, runtime);
            }
            catch (Exception ex)
            {
                runtime.ValidationErrors.Add($"module blueprint catalog load failed: {ex.Message}");
                BuildBuiltinFallback(runtime);
            }

            if (runtime.Manufacturers.Count == 0 || runtime.Parts.Count == 0 || runtime.Blueprints.Count == 0)
            {
                runtime.ValidationErrors.Add("module blueprint catalog incomplete; using builtin fallback");
                BuildBuiltinFallback(runtime);
            }

            runtime.CatalogDigest = ComputeCatalogDigest(runtime);
            s_runtime = runtime;
            return true;
        }

        private static bool TryLoadCatalogJson(out string json, out string source)
        {
            var textAsset = Resources.Load<TextAsset>(DefaultCatalogResourcesPath);
            if (textAsset != null && !string.IsNullOrWhiteSpace(textAsset.text))
            {
                json = textAsset.text;
                source = $"Resources/{DefaultCatalogResourcesPath}";
                return true;
            }

#if UNITY_EDITOR
            var fallbackPath = ResolveEditorCatalogPath();
            if (File.Exists(fallbackPath))
            {
                json = File.ReadAllText(fallbackPath);
                source = fallbackPath;
                return true;
            }
#endif

            json = string.Empty;
            source = string.Empty;
            return false;
        }

#if UNITY_EDITOR
        private static string ResolveEditorCatalogPath()
        {
            var inProject = Path.Combine(Application.dataPath, "Data", "Catalogs", "space4x_module_blueprint_catalog_v0.json");
            if (File.Exists(inProject))
            {
                return inProject;
            }

            var root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(root, "Assets", "Data", "Catalogs", "space4x_module_blueprint_catalog_v0.json");
        }
#endif

        private static void PopulateRuntime(ModuleBlueprintCatalogJson source, ModuleBlueprintCatalogRuntime runtime)
        {
            if (source.slotTypes != null)
            {
                for (var i = 0; i < source.slotTypes.Length; i++)
                {
                    var slot = source.slotTypes[i];
                    var slotType = (slot?.slotType ?? string.Empty).Trim();
                    if (slotType.Length == 0)
                    {
                        continue;
                    }

                    runtime.SlotAllowMultiple[slotType] = slot.allowMultiple;
                }
            }

            if (source.manufacturers != null)
            {
                for (var i = 0; i < source.manufacturers.Length; i++)
                {
                    var item = source.manufacturers[i];
                    var id = (item?.id ?? string.Empty).Trim();
                    if (id.Length == 0)
                    {
                        runtime.ValidationErrors.Add($"manufacturer[{i}] missing id");
                        continue;
                    }

                    if (runtime.Manufacturers.ContainsKey(id))
                    {
                        runtime.ValidationErrors.Add($"duplicate manufacturer id '{id}'");
                        continue;
                    }

                    var manufacturer = new ModuleBlueprintManufacturerRuntime
                    {
                        Id = id,
                        IdFixed = new FixedString64Bytes(id),
                        DisplayName = item.displayName ?? string.Empty,
                        SignatureRuleId = new FixedString64Bytes(item.signatureRuleId ?? string.Empty)
                    };

                    AddStatOps(item.statMods, manufacturer.Ops, runtime.ValidationErrors, $"manufacturer '{id}'");
                    AddTagOps(item.tagOps, manufacturer.Ops, runtime.ValidationErrors, $"manufacturer '{id}'");
                    runtime.Manufacturers[id] = manufacturer;
                }
            }

            if (source.parts != null)
            {
                for (var i = 0; i < source.parts.Length; i++)
                {
                    var item = source.parts[i];
                    var id = (item?.id ?? string.Empty).Trim();
                    if (id.Length == 0)
                    {
                        runtime.ValidationErrors.Add($"part[{i}] missing id");
                        continue;
                    }

                    if (runtime.Parts.ContainsKey(id))
                    {
                        runtime.ValidationErrors.Add($"duplicate part id '{id}'");
                        continue;
                    }

                    var slotType = (item.slotType ?? string.Empty).Trim();
                    if (slotType.Length == 0)
                    {
                        runtime.ValidationErrors.Add($"part '{id}' missing slotType");
                        continue;
                    }

                    var part = new ModuleBlueprintPartRuntime
                    {
                        Id = id,
                        IdFixed = new FixedString64Bytes(id),
                        SlotType = slotType,
                        SlotTypeFixed = new FixedString64Bytes(slotType)
                    };

                    AddStatOps(item.statMods, part.Ops, runtime.ValidationErrors, $"part '{id}'");
                    AddTagOps(item.tagOps, part.Ops, runtime.ValidationErrors, $"part '{id}'");
                    AddEffectOps(item.effectOps, part.Ops, runtime.ValidationErrors, $"part '{id}'");
                    runtime.Parts[id] = part;
                }
            }

            if (source.blueprints != null)
            {
                for (var i = 0; i < source.blueprints.Length; i++)
                {
                    var item = source.blueprints[i];
                    var blueprintId = (item?.blueprintId ?? string.Empty).Trim();
                    if (blueprintId.Length == 0)
                    {
                        runtime.ValidationErrors.Add($"blueprint[{i}] missing blueprintId");
                        continue;
                    }

                    if (runtime.Blueprints.ContainsKey(blueprintId))
                    {
                        runtime.ValidationErrors.Add($"duplicate blueprintId '{blueprintId}'");
                        continue;
                    }

                    var baseModuleId = (item.baseModuleId ?? string.Empty).Trim();
                    var manufacturerId = (item.manufacturerId ?? string.Empty).Trim();
                    if (baseModuleId.Length == 0)
                    {
                        runtime.ValidationErrors.Add($"blueprint '{blueprintId}' missing baseModuleId");
                        continue;
                    }

                    if (manufacturerId.Length == 0)
                    {
                        runtime.ValidationErrors.Add($"blueprint '{blueprintId}' missing manufacturerId");
                        continue;
                    }

                    if (!runtime.Manufacturers.ContainsKey(manufacturerId))
                    {
                        runtime.ValidationErrors.Add($"blueprint '{blueprintId}' references missing manufacturer '{manufacturerId}'");
                    }

                    var parts = item.parts ?? Array.Empty<string>();
                    ValidateBlueprintParts(runtime, blueprintId, parts);

                    var blueprint = new ModuleBlueprintSpecRuntime
                    {
                        BlueprintId = blueprintId,
                        BlueprintIdFixed = new FixedString64Bytes(blueprintId),
                        BaseModuleId = baseModuleId,
                        BaseModuleIdFixed = new FixedString64Bytes(baseModuleId),
                        ManufacturerId = manufacturerId,
                        ManufacturerIdFixed = new FixedString64Bytes(manufacturerId),
                        Parts = parts
                    };

                    runtime.Blueprints[blueprintId] = blueprint;
                    if (!runtime.BlueprintsByBaseModule.TryGetValue(baseModuleId, out var list))
                    {
                        list = new List<ModuleBlueprintSpecRuntime>(2);
                        runtime.BlueprintsByBaseModule[baseModuleId] = list;
                    }
                    list.Add(blueprint);
                }
            }

            foreach (var pair in runtime.BlueprintsByBaseModule)
            {
                pair.Value.Sort((a, b) => string.Compare(a.BlueprintId, b.BlueprintId, StringComparison.Ordinal));
            }
        }

        private static void ValidateBlueprintParts(ModuleBlueprintCatalogRuntime runtime, string blueprintId, string[] parts)
        {
            var slotCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < parts.Length; i++)
            {
                var partId = (parts[i] ?? string.Empty).Trim();
                if (partId.Length == 0)
                {
                    continue;
                }

                if (!runtime.Parts.TryGetValue(partId, out var part))
                {
                    runtime.ValidationErrors.Add($"blueprint '{blueprintId}' references missing part '{partId}'");
                    continue;
                }

                slotCounts.TryGetValue(part.SlotType, out var count);
                count++;
                slotCounts[part.SlotType] = count;
            }

            foreach (var pair in slotCounts)
            {
                runtime.SlotAllowMultiple.TryGetValue(pair.Key, out var allowMultiple);
                if (!allowMultiple && pair.Value > 1)
                {
                    runtime.ValidationErrors.Add($"blueprint '{blueprintId}' has duplicate slotType '{pair.Key}' without allowMultiple");
                }
            }
        }

        private static void AddStatOps(ModuleBlueprintStatModJson[] source, List<ModuleBlueprintResolvedOp> target, List<string> errors, string owner)
        {
            if (source == null)
            {
                return;
            }

            for (var i = 0; i < source.Length; i++)
            {
                var op = source[i];
                var opId = (op?.op ?? string.Empty).Trim();
                var statId = ParseStatId(op?.statId ?? string.Empty);
                if (statId == null)
                {
                    errors.Add($"{owner} statMods[{i}] unknown statId '{op?.statId}'");
                    continue;
                }

                var kind = ParseStatOpKind(opId);
                if (kind == ModuleBlueprintOpKind.None)
                {
                    errors.Add($"{owner} statMods[{i}] unknown op '{opId}'");
                    continue;
                }

                target.Add(new ModuleBlueprintResolvedOp(kind, statId.Value, op.value, default, Space4XDamageType.Unknown, Space4XDamageType.Unknown, WeaponFamily.Unknown, WeaponFamily.Unknown, default, 0f, 0f));
            }
        }

        private static void AddTagOps(ModuleBlueprintTagOpJson[] source, List<ModuleBlueprintResolvedOp> target, List<string> errors, string owner)
        {
            if (source == null)
            {
                return;
            }

            for (var i = 0; i < source.Length; i++)
            {
                var op = source[i];
                var opId = (op?.op ?? string.Empty).Trim();
                var tag = new FixedString64Bytes(op?.tagId ?? string.Empty);
                if (tag.IsEmpty)
                {
                    errors.Add($"{owner} tagOps[{i}] missing tagId");
                    continue;
                }

                var kind = ParseTagOpKind(opId);
                if (kind == ModuleBlueprintOpKind.None)
                {
                    errors.Add($"{owner} tagOps[{i}] unknown op '{opId}'");
                    continue;
                }

                target.Add(new ModuleBlueprintResolvedOp(kind, ModuleDerivedStatId.Damage, op.value == 0f ? 1f : op.value, tag, Space4XDamageType.Unknown, Space4XDamageType.Unknown, WeaponFamily.Unknown, WeaponFamily.Unknown, default, 0f, 0f));
            }
        }

        private static void AddEffectOps(ModuleBlueprintEffectOpJson[] source, List<ModuleBlueprintResolvedOp> target, List<string> errors, string owner)
        {
            if (source == null)
            {
                return;
            }

            for (var i = 0; i < source.Length; i++)
            {
                var op = source[i];
                var opId = (op?.op ?? string.Empty).Trim();
                if (opId.Equals("ConvertDamage", StringComparison.OrdinalIgnoreCase))
                {
                    var fromDamageType = ParseDamageType(op?.fromDamageType);
                    var toDamageType = ParseDamageType(op?.toDamageType);
                    if (fromDamageType == Space4XDamageType.Unknown || toDamageType == Space4XDamageType.Unknown)
                    {
                        errors.Add($"{owner} effectOps[{i}] ConvertDamage requires fromDamageType/toDamageType");
                        continue;
                    }

                    target.Add(new ModuleBlueprintResolvedOp(
                        ModuleBlueprintOpKind.ConvertDamage,
                        ModuleDerivedStatId.Damage,
                        math.saturate(op.pct),
                        default,
                        fromDamageType,
                        toDamageType,
                        WeaponFamily.Unknown,
                        WeaponFamily.Unknown,
                        default,
                        0f,
                        0f));
                    continue;
                }

                if (opId.Equals("ReplaceAttackFamily", StringComparison.OrdinalIgnoreCase))
                {
                    var fromFamily = ParseWeaponFamily(op?.fromFamily);
                    var toFamily = ParseWeaponFamily(op?.toFamily);
                    if (toFamily == WeaponFamily.Unknown)
                    {
                        errors.Add($"{owner} effectOps[{i}] ReplaceAttackFamily requires toFamily");
                        continue;
                    }

                    target.Add(new ModuleBlueprintResolvedOp(
                        ModuleBlueprintOpKind.ReplaceAttackFamily,
                        ModuleDerivedStatId.Damage,
                        0f,
                        default,
                        Space4XDamageType.Unknown,
                        Space4XDamageType.Unknown,
                        fromFamily,
                        toFamily,
                        default,
                        0f,
                        0f));
                    continue;
                }

                if (opId.Equals("AddProc", StringComparison.OrdinalIgnoreCase))
                {
                    var effectId = new FixedString64Bytes(op.effectId ?? string.Empty);
                    if (effectId.IsEmpty)
                    {
                        errors.Add($"{owner} effectOps[{i}] AddProc requires effectId");
                        continue;
                    }

                    target.Add(new ModuleBlueprintResolvedOp(
                        ModuleBlueprintOpKind.AddProc,
                        ModuleDerivedStatId.Damage,
                        0f,
                        default,
                        Space4XDamageType.Unknown,
                        Space4XDamageType.Unknown,
                        WeaponFamily.Unknown,
                        WeaponFamily.Unknown,
                        effectId,
                        math.saturate(op.chance),
                        math.max(0f, op.procCoef)));
                    continue;
                }

                errors.Add($"{owner} effectOps[{i}] unknown op '{opId}'");
            }
        }

        private static ModuleDerivedStatId? ParseStatId(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            return raw.Trim() switch
            {
                "Damage" => ModuleDerivedStatId.Damage,
                "FireRate" => ModuleDerivedStatId.FireRate,
                "Range" => ModuleDerivedStatId.Range,
                "EnergyCost" => ModuleDerivedStatId.EnergyCost,
                "HeatCost" => ModuleDerivedStatId.HeatCost,
                "PowerOutput" => ModuleDerivedStatId.PowerOutput,
                "HeatCapacity" => ModuleDerivedStatId.HeatCapacity,
                "HeatDissipation" => ModuleDerivedStatId.HeatDissipation,
                "Thrust" => ModuleDerivedStatId.Thrust,
                "ShieldCapacity" => ModuleDerivedStatId.ShieldCapacity,
                "SensorRange" => ModuleDerivedStatId.SensorRange,
                "Mass" => ModuleDerivedStatId.Mass,
                "DroneCapacity" => ModuleDerivedStatId.DroneCapacity,
                _ => null
            };
        }

        private static ModuleBlueprintOpKind ParseStatOpKind(string raw)
        {
            if (raw.Equals("Add", StringComparison.OrdinalIgnoreCase) || raw.Equals("AddStat", StringComparison.OrdinalIgnoreCase))
            {
                return ModuleBlueprintOpKind.AddStat;
            }

            if (raw.Equals("Mul", StringComparison.OrdinalIgnoreCase) || raw.Equals("MulStat", StringComparison.OrdinalIgnoreCase))
            {
                return ModuleBlueprintOpKind.MulStat;
            }

            return ModuleBlueprintOpKind.None;
        }

        private static ModuleBlueprintOpKind ParseTagOpKind(string raw)
        {
            if (raw.Equals("Add", StringComparison.OrdinalIgnoreCase) || raw.Equals("AddTag", StringComparison.OrdinalIgnoreCase))
            {
                return ModuleBlueprintOpKind.AddTag;
            }

            if (raw.Equals("Remove", StringComparison.OrdinalIgnoreCase) || raw.Equals("RemoveTag", StringComparison.OrdinalIgnoreCase))
            {
                return ModuleBlueprintOpKind.RemoveTag;
            }

            return ModuleBlueprintOpKind.None;
        }

        private static Space4XDamageType ParseDamageType(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return Space4XDamageType.Unknown;
            }

            return raw.Trim() switch
            {
                "Energy" => Space4XDamageType.Energy,
                "Thermal" => Space4XDamageType.Thermal,
                "EM" => Space4XDamageType.EM,
                "Radiation" => Space4XDamageType.Radiation,
                "Kinetic" => Space4XDamageType.Kinetic,
                "Explosive" => Space4XDamageType.Explosive,
                "Caustic" => Space4XDamageType.Caustic,
                _ => Space4XDamageType.Unknown
            };
        }

        private static WeaponFamily ParseWeaponFamily(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return WeaponFamily.Unknown;
            }

            return raw.Trim() switch
            {
                "Energy" => WeaponFamily.Energy,
                "Kinetic" => WeaponFamily.Kinetic,
                "Explosive" => WeaponFamily.Explosive,
                _ => WeaponFamily.Unknown
            };
        }

        private static uint ComputeCatalogDigest(ModuleBlueprintCatalogRuntime runtime)
        {
            var hash = 2166136261u;
            foreach (var item in SortedKeys(runtime.Manufacturers.Keys))
            {
                AppendString(ref hash, item);
            }
            foreach (var item in SortedKeys(runtime.Parts.Keys))
            {
                AppendString(ref hash, item);
            }
            foreach (var item in SortedKeys(runtime.Blueprints.Keys))
            {
                AppendString(ref hash, item);
            }
            return hash;
        }

        private static List<string> SortedKeys(Dictionary<string, ModuleBlueprintManufacturerRuntime>.KeyCollection keys)
        {
            var result = new List<string>(keys.Count);
            foreach (var key in keys)
            {
                result.Add(key);
            }
            result.Sort(StringComparer.Ordinal);
            return result;
        }

        private static List<string> SortedKeys(Dictionary<string, ModuleBlueprintPartRuntime>.KeyCollection keys)
        {
            var result = new List<string>(keys.Count);
            foreach (var key in keys)
            {
                result.Add(key);
            }
            result.Sort(StringComparer.Ordinal);
            return result;
        }

        private static List<string> SortedKeys(Dictionary<string, ModuleBlueprintSpecRuntime>.KeyCollection keys)
        {
            var result = new List<string>(keys.Count);
            foreach (var key in keys)
            {
                result.Add(key);
            }
            result.Sort(StringComparer.Ordinal);
            return result;
        }

        private static void AppendString(ref uint hash, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                hash = (hash ^ 0u) * 16777619u;
                return;
            }

            for (var i = 0; i < value.Length; i++)
            {
                hash = (hash ^ value[i]) * 16777619u;
            }

            hash = (hash ^ 0u) * 16777619u;
        }

        private static void BuildBuiltinFallback(ModuleBlueprintCatalogRuntime runtime)
        {
            runtime.SlotAllowMultiple.Clear();
            runtime.Manufacturers.Clear();
            runtime.Parts.Clear();
            runtime.Blueprints.Clear();
            runtime.BlueprintsByBaseModule.Clear();

            runtime.SlotAllowMultiple["Core"] = false;
            runtime.SlotAllowMultiple["Output"] = false;
            runtime.SlotAllowMultiple["Cooling"] = false;
            runtime.SlotAllowMultiple["Guidance"] = false;
            runtime.SlotAllowMultiple["Utility"] = true;

            var baseline = new ModuleBlueprintManufacturerRuntime
            {
                Id = "baseline",
                IdFixed = new FixedString64Bytes("baseline"),
                DisplayName = "Baseline Foundry"
            };
            baseline.Ops.Add(new ModuleBlueprintResolvedOp(
                ModuleBlueprintOpKind.AddTag,
                ModuleDerivedStatId.Damage,
                1f,
                new FixedString64Bytes("manufacturer.baseline"),
                Space4XDamageType.Unknown,
                Space4XDamageType.Unknown,
                WeaponFamily.Unknown,
                WeaponFamily.Unknown,
                default,
                0f,
                0f));
            runtime.Manufacturers[baseline.Id] = baseline;

            var prism = new ModuleBlueprintManufacturerRuntime
            {
                Id = "prismworks",
                IdFixed = new FixedString64Bytes("prismworks"),
                DisplayName = "Prismworks"
            };
            prism.Ops.Add(new ModuleBlueprintResolvedOp(ModuleBlueprintOpKind.MulStat, ModuleDerivedStatId.Damage, 1.08f, default, Space4XDamageType.Unknown, Space4XDamageType.Unknown, WeaponFamily.Unknown, WeaponFamily.Unknown, default, 0f, 0f));
            prism.Ops.Add(new ModuleBlueprintResolvedOp(ModuleBlueprintOpKind.MulStat, ModuleDerivedStatId.Range, 1.1f, default, Space4XDamageType.Unknown, Space4XDamageType.Unknown, WeaponFamily.Unknown, WeaponFamily.Unknown, default, 0f, 0f));
            prism.Ops.Add(new ModuleBlueprintResolvedOp(ModuleBlueprintOpKind.AddTag, ModuleDerivedStatId.Damage, 1f, new FixedString64Bytes("manufacturer.prismworks"), Space4XDamageType.Unknown, Space4XDamageType.Unknown, WeaponFamily.Unknown, WeaponFamily.Unknown, default, 0f, 0f));
            runtime.Manufacturers[prism.Id] = prism;

            AddFallbackPart(runtime, "core_a", "Core", new[]
            {
                new ModuleBlueprintResolvedOp(ModuleBlueprintOpKind.AddStat, ModuleDerivedStatId.HeatCapacity, 8f, default, Space4XDamageType.Unknown, Space4XDamageType.Unknown, WeaponFamily.Unknown, WeaponFamily.Unknown, default, 0f, 0f)
            });
            AddFallbackPart(runtime, "core_b", "Core", new[]
            {
                new ModuleBlueprintResolvedOp(ModuleBlueprintOpKind.AddStat, ModuleDerivedStatId.Damage, 2f, default, Space4XDamageType.Unknown, Space4XDamageType.Unknown, WeaponFamily.Unknown, WeaponFamily.Unknown, default, 0f, 0f)
            });
            AddFallbackPart(runtime, "lens_beam_bias", "Output", new[]
            {
                new ModuleBlueprintResolvedOp(ModuleBlueprintOpKind.MulStat, ModuleDerivedStatId.Damage, 1.15f, default, Space4XDamageType.Unknown, Space4XDamageType.Unknown, WeaponFamily.Unknown, WeaponFamily.Unknown, default, 0f, 0f),
                new ModuleBlueprintResolvedOp(ModuleBlueprintOpKind.ReplaceAttackFamily, ModuleDerivedStatId.Damage, 0f, default, Space4XDamageType.Unknown, Space4XDamageType.Unknown, WeaponFamily.Unknown, WeaponFamily.Energy, default, 0f, 0f),
                new ModuleBlueprintResolvedOp(ModuleBlueprintOpKind.AddTag, ModuleDerivedStatId.Damage, 1f, new FixedString64Bytes("attack.beam"), Space4XDamageType.Unknown, Space4XDamageType.Unknown, WeaponFamily.Unknown, WeaponFamily.Unknown, default, 0f, 0f)
            });
            AddFallbackPart(runtime, "barrel_kinetic_bias", "Output", new[]
            {
                new ModuleBlueprintResolvedOp(ModuleBlueprintOpKind.MulStat, ModuleDerivedStatId.Damage, 1.1f, default, Space4XDamageType.Unknown, Space4XDamageType.Unknown, WeaponFamily.Unknown, WeaponFamily.Unknown, default, 0f, 0f),
                new ModuleBlueprintResolvedOp(ModuleBlueprintOpKind.AddTag, ModuleDerivedStatId.Damage, 1f, new FixedString64Bytes("damage.kinetic"), Space4XDamageType.Unknown, Space4XDamageType.Unknown, WeaponFamily.Unknown, WeaponFamily.Unknown, default, 0f, 0f)
            });
            AddFallbackPart(runtime, "cooling_stable", "Cooling", new[]
            {
                new ModuleBlueprintResolvedOp(ModuleBlueprintOpKind.MulStat, ModuleDerivedStatId.HeatCost, 0.85f, default, Space4XDamageType.Unknown, Space4XDamageType.Unknown, WeaponFamily.Unknown, WeaponFamily.Unknown, default, 0f, 0f),
                new ModuleBlueprintResolvedOp(ModuleBlueprintOpKind.AddStat, ModuleDerivedStatId.HeatDissipation, 6f, default, Space4XDamageType.Unknown, Space4XDamageType.Unknown, WeaponFamily.Unknown, WeaponFamily.Unknown, default, 0f, 0f)
            });
            AddFallbackPart(runtime, "guidance_drone_link", "Guidance", new[]
            {
                new ModuleBlueprintResolvedOp(ModuleBlueprintOpKind.AddStat, ModuleDerivedStatId.DroneCapacity, 2f, default, Space4XDamageType.Unknown, Space4XDamageType.Unknown, WeaponFamily.Unknown, WeaponFamily.Unknown, default, 0f, 0f),
                new ModuleBlueprintResolvedOp(ModuleBlueprintOpKind.ReplaceAttackFamily, ModuleDerivedStatId.Damage, 0f, default, Space4XDamageType.Unknown, Space4XDamageType.Unknown, WeaponFamily.Unknown, WeaponFamily.Energy, default, 0f, 0f),
                new ModuleBlueprintResolvedOp(ModuleBlueprintOpKind.AddTag, ModuleDerivedStatId.Damage, 1f, new FixedString64Bytes("drone.linked"), Space4XDamageType.Unknown, Space4XDamageType.Unknown, WeaponFamily.Unknown, WeaponFamily.Unknown, default, 0f, 0f)
            });

            AddFallbackBlueprint(runtime, "blueprint.laser.prismworks", "laser-s-1", "prismworks", new[] { "core_a", "lens_beam_bias", "cooling_stable" });
            AddFallbackBlueprint(runtime, "blueprint.kinetic.baseline", "pd-s-1", "baseline", new[] { "core_b", "barrel_kinetic_bias", "cooling_stable" });
            AddFallbackBlueprint(runtime, "blueprint.hangar.prismworks", "hangar-s-1", "prismworks", new[] { "core_a", "guidance_drone_link", "cooling_stable" });
        }

        private static void AddFallbackPart(ModuleBlueprintCatalogRuntime runtime, string id, string slotType, ModuleBlueprintResolvedOp[] ops)
        {
            var part = new ModuleBlueprintPartRuntime
            {
                Id = id,
                IdFixed = new FixedString64Bytes(id),
                SlotType = slotType,
                SlotTypeFixed = new FixedString64Bytes(slotType)
            };

            part.Ops.AddRange(ops);
            runtime.Parts[id] = part;
        }

        private static void AddFallbackBlueprint(ModuleBlueprintCatalogRuntime runtime, string blueprintId, string baseModuleId, string manufacturerId, string[] parts)
        {
            var blueprint = new ModuleBlueprintSpecRuntime
            {
                BlueprintId = blueprintId,
                BlueprintIdFixed = new FixedString64Bytes(blueprintId),
                BaseModuleId = baseModuleId,
                BaseModuleIdFixed = new FixedString64Bytes(baseModuleId),
                ManufacturerId = manufacturerId,
                ManufacturerIdFixed = new FixedString64Bytes(manufacturerId),
                Parts = parts
            };
            runtime.Blueprints[blueprintId] = blueprint;
            if (!runtime.BlueprintsByBaseModule.TryGetValue(baseModuleId, out var list))
            {
                list = new List<ModuleBlueprintSpecRuntime>(2);
                runtime.BlueprintsByBaseModule[baseModuleId] = list;
            }

            list.Add(blueprint);
            list.Sort((a, b) => string.Compare(a.BlueprintId, b.BlueprintId, StringComparison.Ordinal));
        }
    }

    public struct ModuleBlueprintResolveResult
    {
        public float Damage;
        public float FireRate;
        public float Range;
        public float EnergyCost;
        public float HeatCost;
        public float PowerOutput;
        public float HeatCapacity;
        public float HeatDissipation;
        public float Mass;
        public float DroneCapacity;
        public WeaponFamily AttackFamily;
        public Space4XDamageType DamageType;
        public WeaponDelivery Delivery;
        public uint Digest;
    }

    public static class Space4XModuleBlueprintResolver
    {
        private struct DamageConversionRule
        {
            public Space4XDamageType From;
            public Space4XDamageType To;
            public float Pct;
        }

        private struct ResolverAccumulator
        {
            public float Damage;
            public float FireRate;
            public float Range;
            public float EnergyCost;
            public float HeatCost;
            public float PowerOutput;
            public float HeatCapacity;
            public float HeatDissipation;
            public float Mass;
            public float DroneCapacity;
            public WeaponFamily AttackFamily;
            public Space4XDamageType DamageType;
            public WeaponDelivery Delivery;
            public float EnergyDamage;
            public float ThermalDamage;
            public float EMDamage;
            public float RadiationDamage;
            public float KineticDamage;
            public float ExplosiveDamage;
            public float CausticDamage;
            public byte FamilyExplicit;
        }

        private sealed class PartSortComparer : IComparer<ModuleBlueprintPartRuntime>
        {
            public static readonly PartSortComparer Instance = new PartSortComparer();

            public int Compare(ModuleBlueprintPartRuntime x, ModuleBlueprintPartRuntime y)
            {
                var slot = string.Compare(x?.SlotType, y?.SlotType, StringComparison.Ordinal);
                if (slot != 0)
                {
                    return slot;
                }

                return string.Compare(x?.Id, y?.Id, StringComparison.Ordinal);
            }
        }

        public static ModuleBlueprintResolveResult Resolve(
            in RegistryModuleSpec baseSpec,
            in ModuleBlueprintRef blueprintRef,
            DynamicBuffer<ModuleBlueprintPartId> partBuffer,
            DynamicBuffer<ModuleDerivedTag> tagsOut,
            DynamicBuffer<ModuleDerivedEffectOp> effectsOut,
            NativeArray<ModuleBlueprintRunPerkOp> runPerks)
        {
            Space4XModuleBlueprintCatalogStore.EnsureLoaded();
            var runtime = Space4XModuleBlueprintCatalogStore.Runtime;

            var acc = CreateBaseAccumulator(baseSpec);
            var conversions = new List<DamageConversionRule>(8);
            var tags = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

            if (!blueprintRef.ManufacturerId.IsEmpty && runtime.Manufacturers.TryGetValue(blueprintRef.ManufacturerId.ToString(), out var explicitManufacturer))
            {
                ApplyOps(explicitManufacturer.Ops, ref acc, conversions, tags, effectsOut);
            }
            else if (!blueprintRef.BlueprintId.IsEmpty && runtime.Blueprints.TryGetValue(blueprintRef.BlueprintId.ToString(), out var blueprintFromRef))
            {
                if (runtime.Manufacturers.TryGetValue(blueprintFromRef.ManufacturerId, out var blueprintManufacturer))
                {
                    ApplyOps(blueprintManufacturer.Ops, ref acc, conversions, tags, effectsOut);
                }
            }

            var resolvedParts = new List<ModuleBlueprintPartRuntime>(partBuffer.Length);
            for (var i = 0; i < partBuffer.Length; i++)
            {
                var partId = partBuffer[i].PartId.ToString();
                if (runtime.Parts.TryGetValue(partId, out var part))
                {
                    resolvedParts.Add(part);
                }
            }

            resolvedParts.Sort(PartSortComparer.Instance);
            for (var i = 0; i < resolvedParts.Count; i++)
            {
                ApplyOps(resolvedParts[i].Ops, ref acc, conversions, tags, effectsOut);
            }

            if (runPerks.IsCreated)
            {
                for (var i = 0; i < runPerks.Length; i++)
                {
                    ApplyRunPerk(runPerks[i], ref acc, conversions, tags, effectsOut);
                }
            }

            ApplyDamageConversions(ref acc, conversions);
            PopulateTags(tagsOut, tags);

            var result = BuildResult(acc, baseSpec);
            result.Digest = ComputeDigest(baseSpec.Id, blueprintRef, resolvedParts, result, tagsOut, effectsOut);
            return result;
        }

        public static float ComputeScaledConversionTotal(float pctA, float pctB)
        {
            var total = math.max(0f, pctA) + math.max(0f, pctB);
            if (total <= 1f)
            {
                return total;
            }

            var scale = 1f / total;
            return math.max(0f, pctA) * scale + math.max(0f, pctB) * scale;
        }

        private static ResolverAccumulator CreateBaseAccumulator(in RegistryModuleSpec spec)
        {
            var acc = new ResolverAccumulator
            {
                Damage = math.max(1f, spec.OffenseRating * 10f),
                FireRate = 1f,
                Range = 1f,
                EnergyCost = math.max(0f, spec.PowerDrawMW),
                HeatCost = 1f,
                PowerOutput = spec.Class == ModuleClass.Reactor ? math.max(0f, -spec.PowerDrawMW) : 0f,
                HeatCapacity = spec.Class == ModuleClass.Reactor ? 100f + 40f * (int)spec.RequiredSize : 0f,
                HeatDissipation = spec.Class == ModuleClass.Reactor ? 5f + 3f * (int)spec.RequiredSize : 0f,
                Mass = math.max(0.1f, spec.MassTons),
                DroneCapacity = spec.Class == ModuleClass.Hangar ? math.max(1f, spec.FunctionCapacity) : 0f,
                AttackFamily = WeaponFamily.Unknown,
                DamageType = Space4XDamageType.Unknown,
                Delivery = WeaponDelivery.Unknown,
                FamilyExplicit = 0
            };

            switch (spec.Class)
            {
                case ModuleClass.Laser:
                    acc.AttackFamily = WeaponFamily.Energy;
                    acc.DamageType = Space4XDamageType.Energy;
                    acc.Delivery = WeaponDelivery.Beam;
                    acc.EnergyDamage = acc.Damage;
                    break;
                case ModuleClass.Kinetic:
                case ModuleClass.PointDefense:
                    acc.AttackFamily = WeaponFamily.Kinetic;
                    acc.DamageType = Space4XDamageType.Kinetic;
                    acc.Delivery = WeaponDelivery.Slug;
                    acc.KineticDamage = acc.Damage;
                    break;
                case ModuleClass.Missile:
                    acc.AttackFamily = WeaponFamily.Explosive;
                    acc.DamageType = Space4XDamageType.Explosive;
                    acc.Delivery = WeaponDelivery.Guided;
                    acc.ExplosiveDamage = acc.Damage;
                    break;
                case ModuleClass.Hangar:
                    acc.AttackFamily = WeaponFamily.Kinetic;
                    acc.DamageType = Space4XDamageType.Kinetic;
                    acc.Delivery = WeaponDelivery.Slug;
                    break;
            }

            return acc;
        }

        private static void ApplyOps(
            List<ModuleBlueprintResolvedOp> ops,
            ref ResolverAccumulator acc,
            List<DamageConversionRule> conversions,
            Dictionary<string, float> tags,
            DynamicBuffer<ModuleDerivedEffectOp> effectsOut)
        {
            for (var i = 0; i < ops.Count; i++)
            {
                ApplyOp(ops[i], ref acc, conversions, tags, effectsOut);
            }
        }

        private static void ApplyRunPerk(
            in ModuleBlueprintRunPerkOp op,
            ref ResolverAccumulator acc,
            List<DamageConversionRule> conversions,
            Dictionary<string, float> tags,
            DynamicBuffer<ModuleDerivedEffectOp> effectsOut)
        {
            var resolved = new ModuleBlueprintResolvedOp(
                op.OpKind,
                op.StatId,
                op.Value,
                op.TagId,
                op.FromDamageType,
                op.ToDamageType,
                op.FromFamily,
                op.ToFamily,
                op.EffectId,
                op.Chance,
                op.ProcCoefficient);

            ApplyOp(resolved, ref acc, conversions, tags, effectsOut);
        }

        private static void ApplyOp(
            in ModuleBlueprintResolvedOp op,
            ref ResolverAccumulator acc,
            List<DamageConversionRule> conversions,
            Dictionary<string, float> tags,
            DynamicBuffer<ModuleDerivedEffectOp> effectsOut)
        {
            switch (op.Kind)
            {
                case ModuleBlueprintOpKind.AddStat:
                    AddStat(ref acc, op.StatId, op.Value);
                    break;
                case ModuleBlueprintOpKind.MulStat:
                    MulStat(ref acc, op.StatId, op.Value);
                    break;
                case ModuleBlueprintOpKind.AddTag:
                    tags[op.TagId.ToString()] = op.Value;
                    break;
                case ModuleBlueprintOpKind.RemoveTag:
                    tags.Remove(op.TagId.ToString());
                    break;
                case ModuleBlueprintOpKind.ConvertDamage:
                    if (op.FromDamageType != Space4XDamageType.Unknown && op.ToDamageType != Space4XDamageType.Unknown && op.Value > 0f)
                    {
                        conversions.Add(new DamageConversionRule
                        {
                            From = op.FromDamageType,
                            To = op.ToDamageType,
                            Pct = math.saturate(op.Value)
                        });

                        effectsOut.Add(new ModuleDerivedEffectOp
                        {
                            OpKind = ModuleBlueprintOpKind.ConvertDamage,
                            FromDamageType = op.FromDamageType,
                            ToDamageType = op.ToDamageType,
                            ConversionPct = math.saturate(op.Value)
                        });
                    }
                    break;
                case ModuleBlueprintOpKind.ReplaceAttackFamily:
                    if (op.ToFamily != WeaponFamily.Unknown && (op.FromFamily == WeaponFamily.Unknown || op.FromFamily == acc.AttackFamily))
                    {
                        acc.AttackFamily = op.ToFamily;
                        acc.FamilyExplicit = 1;
                        acc.Delivery = ResolveDeliveryForFamily(acc.AttackFamily);
                        effectsOut.Add(new ModuleDerivedEffectOp
                        {
                            OpKind = ModuleBlueprintOpKind.ReplaceAttackFamily,
                            FromFamily = op.FromFamily,
                            ToFamily = op.ToFamily
                        });
                    }
                    break;
                case ModuleBlueprintOpKind.AddProc:
                    if (!op.EffectId.IsEmpty)
                    {
                        effectsOut.Add(new ModuleDerivedEffectOp
                        {
                            OpKind = ModuleBlueprintOpKind.AddProc,
                            EffectId = op.EffectId,
                            Chance = math.saturate(op.Chance),
                            ProcCoefficient = math.max(0f, op.ProcCoefficient)
                        });
                    }
                    break;
            }
        }

        private static void ApplyDamageConversions(ref ResolverAccumulator acc, List<DamageConversionRule> conversions)
        {
            if (conversions.Count == 0)
            {
                return;
            }

            var sumByFrom = new float[8];
            for (var i = 0; i < conversions.Count; i++)
            {
                var from = (int)conversions[i].From;
                if (from >= 0 && from < sumByFrom.Length)
                {
                    sumByFrom[from] += math.saturate(conversions[i].Pct);
                }
            }

            var scaled = new DamageConversionRule[conversions.Count];
            for (var i = 0; i < conversions.Count; i++)
            {
                var rule = conversions[i];
                var from = (int)rule.From;
                var total = from >= 0 && from < sumByFrom.Length ? sumByFrom[from] : 0f;
                var pct = math.saturate(rule.Pct);
                if (total > 1f)
                {
                    pct *= 1f / total;
                }

                scaled[i] = new DamageConversionRule { From = rule.From, To = rule.To, Pct = pct };
            }

            var original = new float[8];
            original[(int)Space4XDamageType.Energy] = acc.EnergyDamage;
            original[(int)Space4XDamageType.Thermal] = acc.ThermalDamage;
            original[(int)Space4XDamageType.EM] = acc.EMDamage;
            original[(int)Space4XDamageType.Radiation] = acc.RadiationDamage;
            original[(int)Space4XDamageType.Kinetic] = acc.KineticDamage;
            original[(int)Space4XDamageType.Explosive] = acc.ExplosiveDamage;
            original[(int)Space4XDamageType.Caustic] = acc.CausticDamage;

            var next = new float[8];
            Array.Copy(original, next, original.Length);

            for (var fromType = 0; fromType < original.Length; fromType++)
            {
                var fromAmount = original[fromType];
                if (fromAmount <= 0f)
                {
                    continue;
                }

                var converted = 0f;
                for (var i = 0; i < scaled.Length; i++)
                {
                    if ((int)scaled[i].From != fromType)
                    {
                        continue;
                    }

                    var amount = fromAmount * math.saturate(scaled[i].Pct);
                    converted += amount;
                    var to = (int)scaled[i].To;
                    if (to >= 0 && to < next.Length)
                    {
                        next[to] += amount;
                    }
                }

                next[fromType] = math.max(0f, next[fromType] - math.min(fromAmount, converted));
            }

            acc.EnergyDamage = next[(int)Space4XDamageType.Energy];
            acc.ThermalDamage = next[(int)Space4XDamageType.Thermal];
            acc.EMDamage = next[(int)Space4XDamageType.EM];
            acc.RadiationDamage = next[(int)Space4XDamageType.Radiation];
            acc.KineticDamage = next[(int)Space4XDamageType.Kinetic];
            acc.ExplosiveDamage = next[(int)Space4XDamageType.Explosive];
            acc.CausticDamage = next[(int)Space4XDamageType.Caustic];

            acc.Damage = 0f;
            for (var i = 0; i < next.Length; i++)
            {
                acc.Damage += math.max(0f, next[i]);
            }

            acc.DamageType = ResolveDominantDamageType(acc);
            if (acc.FamilyExplicit == 0)
            {
                acc.AttackFamily = ResolveFamilyFromDamage(acc.DamageType, acc.AttackFamily);
                acc.Delivery = ResolveDeliveryForFamily(acc.AttackFamily);
            }
        }

        private static Space4XDamageType ResolveDominantDamageType(in ResolverAccumulator acc)
        {
            var best = 0f;
            var type = Space4XDamageType.Unknown;

            TryPromote(ref best, ref type, Space4XDamageType.Energy, acc.EnergyDamage);
            TryPromote(ref best, ref type, Space4XDamageType.Thermal, acc.ThermalDamage);
            TryPromote(ref best, ref type, Space4XDamageType.EM, acc.EMDamage);
            TryPromote(ref best, ref type, Space4XDamageType.Radiation, acc.RadiationDamage);
            TryPromote(ref best, ref type, Space4XDamageType.Kinetic, acc.KineticDamage);
            TryPromote(ref best, ref type, Space4XDamageType.Explosive, acc.ExplosiveDamage);
            TryPromote(ref best, ref type, Space4XDamageType.Caustic, acc.CausticDamage);

            return type;
        }

        private static void TryPromote(ref float best, ref Space4XDamageType type, Space4XDamageType candidateType, float value)
        {
            if (value > best)
            {
                best = value;
                type = candidateType;
            }
        }

        private static ModuleBlueprintResolveResult BuildResult(in ResolverAccumulator acc, in RegistryModuleSpec baseSpec)
        {
            return new ModuleBlueprintResolveResult
            {
                Damage = math.max(0.01f, acc.Damage),
                FireRate = math.max(0.01f, acc.FireRate),
                Range = math.max(0.1f, acc.Range),
                EnergyCost = math.max(0f, acc.EnergyCost),
                HeatCost = math.max(0f, acc.HeatCost),
                PowerOutput = math.max(0f, acc.PowerOutput),
                HeatCapacity = math.max(0f, acc.HeatCapacity),
                HeatDissipation = math.max(0f, acc.HeatDissipation),
                Mass = math.max(0.01f, acc.Mass),
                DroneCapacity = math.max(0f, acc.DroneCapacity),
                AttackFamily = acc.AttackFamily,
                DamageType = acc.DamageType,
                Delivery = acc.Delivery
            };
        }

        private static uint ComputeDigest(
            in FixedString64Bytes moduleId,
            in ModuleBlueprintRef blueprintRef,
            List<ModuleBlueprintPartRuntime> sortedParts,
            in ModuleBlueprintResolveResult result,
            DynamicBuffer<ModuleDerivedTag> tags,
            DynamicBuffer<ModuleDerivedEffectOp> effects)
        {
            var hash = 2166136261u;
            AppendFixedString(ref hash, moduleId);
            AppendFixedString(ref hash, blueprintRef.ManufacturerId);
            AppendFixedString(ref hash, blueprintRef.BlueprintId);
            AppendInt(ref hash, (int)blueprintRef.StableHash);

            for (var i = 0; i < sortedParts.Count; i++)
            {
                AppendFixedString(ref hash, sortedParts[i].SlotTypeFixed);
                AppendFixedString(ref hash, sortedParts[i].IdFixed);
            }

            AppendQuantized(ref hash, result.Damage);
            AppendQuantized(ref hash, result.FireRate);
            AppendQuantized(ref hash, result.Range);
            AppendQuantized(ref hash, result.EnergyCost);
            AppendQuantized(ref hash, result.HeatCost);
            AppendQuantized(ref hash, result.PowerOutput);
            AppendQuantized(ref hash, result.HeatCapacity);
            AppendQuantized(ref hash, result.HeatDissipation);
            AppendQuantized(ref hash, result.Mass);
            AppendQuantized(ref hash, result.DroneCapacity);
            AppendInt(ref hash, (int)result.AttackFamily);
            AppendInt(ref hash, (int)result.DamageType);
            AppendInt(ref hash, (int)result.Delivery);

            var tagList = new List<ModuleDerivedTag>(tags.Length);
            for (var i = 0; i < tags.Length; i++)
            {
                tagList.Add(tags[i]);
            }
            tagList.Sort((a, b) => string.Compare(a.TagId.ToString(), b.TagId.ToString(), StringComparison.Ordinal));
            for (var i = 0; i < tagList.Count; i++)
            {
                AppendFixedString(ref hash, tagList[i].TagId);
                AppendQuantized(ref hash, tagList[i].Value);
            }

            var effectList = new List<ModuleDerivedEffectOp>(effects.Length);
            for (var i = 0; i < effects.Length; i++)
            {
                effectList.Add(effects[i]);
            }
            effectList.Sort((a, b) =>
            {
                var kind = a.OpKind.CompareTo(b.OpKind);
                if (kind != 0)
                {
                    return kind;
                }

                return string.Compare(a.EffectId.ToString(), b.EffectId.ToString(), StringComparison.Ordinal);
            });

            for (var i = 0; i < effectList.Count; i++)
            {
                var effect = effectList[i];
                AppendInt(ref hash, (int)effect.OpKind);
                AppendFixedString(ref hash, effect.EffectId);
                AppendQuantized(ref hash, effect.Chance);
                AppendQuantized(ref hash, effect.ProcCoefficient);
                AppendInt(ref hash, (int)effect.FromDamageType);
                AppendInt(ref hash, (int)effect.ToDamageType);
                AppendQuantized(ref hash, effect.ConversionPct);
                AppendInt(ref hash, (int)effect.FromFamily);
                AppendInt(ref hash, (int)effect.ToFamily);
            }

            return hash;
        }

        private static void PopulateTags(DynamicBuffer<ModuleDerivedTag> tagsOut, Dictionary<string, float> tags)
        {
            tagsOut.Clear();
            var keys = new List<string>(tags.Keys);
            keys.Sort(StringComparer.Ordinal);
            for (var i = 0; i < keys.Count; i++)
            {
                var key = keys[i];
                tagsOut.Add(new ModuleDerivedTag
                {
                    TagId = new FixedString64Bytes(key),
                    Value = tags[key]
                });
            }
        }

        private static WeaponFamily ResolveFamilyFromDamage(Space4XDamageType damageType, WeaponFamily fallback)
        {
            return damageType switch
            {
                Space4XDamageType.Energy => WeaponFamily.Energy,
                Space4XDamageType.Thermal => WeaponFamily.Energy,
                Space4XDamageType.EM => WeaponFamily.Energy,
                Space4XDamageType.Radiation => WeaponFamily.Energy,
                Space4XDamageType.Caustic => WeaponFamily.Energy,
                Space4XDamageType.Kinetic => WeaponFamily.Kinetic,
                Space4XDamageType.Explosive => WeaponFamily.Explosive,
                _ => fallback
            };
        }

        private static WeaponDelivery ResolveDeliveryForFamily(WeaponFamily family)
        {
            return family switch
            {
                WeaponFamily.Energy => WeaponDelivery.Beam,
                WeaponFamily.Kinetic => WeaponDelivery.Slug,
                WeaponFamily.Explosive => WeaponDelivery.Guided,
                _ => WeaponDelivery.Unknown
            };
        }

        private static void AddStat(ref ResolverAccumulator acc, ModuleDerivedStatId id, float value)
        {
            switch (id)
            {
                case ModuleDerivedStatId.Damage:
                    acc.Damage += value;
                    break;
                case ModuleDerivedStatId.FireRate:
                    acc.FireRate += value;
                    break;
                case ModuleDerivedStatId.Range:
                    acc.Range += value;
                    break;
                case ModuleDerivedStatId.EnergyCost:
                    acc.EnergyCost += value;
                    break;
                case ModuleDerivedStatId.HeatCost:
                    acc.HeatCost += value;
                    break;
                case ModuleDerivedStatId.PowerOutput:
                    acc.PowerOutput += value;
                    break;
                case ModuleDerivedStatId.HeatCapacity:
                    acc.HeatCapacity += value;
                    break;
                case ModuleDerivedStatId.HeatDissipation:
                    acc.HeatDissipation += value;
                    break;
                case ModuleDerivedStatId.Mass:
                    acc.Mass += value;
                    break;
                case ModuleDerivedStatId.DroneCapacity:
                    acc.DroneCapacity += value;
                    break;
            }
        }

        private static void MulStat(ref ResolverAccumulator acc, ModuleDerivedStatId id, float value)
        {
            switch (id)
            {
                case ModuleDerivedStatId.Damage:
                    acc.Damage *= value;
                    break;
                case ModuleDerivedStatId.FireRate:
                    acc.FireRate *= value;
                    break;
                case ModuleDerivedStatId.Range:
                    acc.Range *= value;
                    break;
                case ModuleDerivedStatId.EnergyCost:
                    acc.EnergyCost *= value;
                    break;
                case ModuleDerivedStatId.HeatCost:
                    acc.HeatCost *= value;
                    break;
                case ModuleDerivedStatId.PowerOutput:
                    acc.PowerOutput *= value;
                    break;
                case ModuleDerivedStatId.HeatCapacity:
                    acc.HeatCapacity *= value;
                    break;
                case ModuleDerivedStatId.HeatDissipation:
                    acc.HeatDissipation *= value;
                    break;
                case ModuleDerivedStatId.Mass:
                    acc.Mass *= value;
                    break;
                case ModuleDerivedStatId.DroneCapacity:
                    acc.DroneCapacity *= value;
                    break;
            }
        }

        private static void AppendFixedString(ref uint hash, in FixedString64Bytes value)
        {
            for (var i = 0; i < value.Length; i++)
            {
                hash = (hash ^ value[i]) * 16777619u;
            }
            hash = (hash ^ 0u) * 16777619u;
        }

        private static void AppendInt(ref uint hash, int value)
        {
            unchecked
            {
                hash = (hash ^ (byte)(value & 0xFF)) * 16777619u;
                hash = (hash ^ (byte)((value >> 8) & 0xFF)) * 16777619u;
                hash = (hash ^ (byte)((value >> 16) & 0xFF)) * 16777619u;
                hash = (hash ^ (byte)((value >> 24) & 0xFF)) * 16777619u;
            }
        }

        private static void AppendQuantized(ref uint hash, float value)
        {
            var quantized = (int)math.round(value * 1000f);
            AppendInt(ref hash, quantized);
        }
    }

    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(ModuleCatalogBootstrapSystem))]
    public partial struct Space4XModuleBlueprintCatalogBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.TryGetSingletonEntity<ModuleBlueprintCatalogStatus>(out _))
            {
                state.Enabled = false;
                return;
            }

            Space4XModuleBlueprintCatalogStore.EnsureLoaded();
            var runtime = Space4XModuleBlueprintCatalogStore.Runtime;

            var statusEntity = state.EntityManager.CreateEntity(typeof(ModuleBlueprintCatalogStatus));
            state.EntityManager.SetComponentData(statusEntity, new ModuleBlueprintCatalogStatus
            {
                Loaded = 1,
                HasErrors = (byte)(runtime.ValidationErrors.Count > 0 ? 1 : 0),
                CatalogDigest = runtime.CatalogDigest,
                ValidationErrorCount = (ushort)math.min(ushort.MaxValue, runtime.ValidationErrors.Count)
            });

            var errors = state.EntityManager.AddBuffer<ModuleBlueprintValidationError>(statusEntity);
            for (var i = 0; i < runtime.ValidationErrors.Count; i++)
            {
                errors.Add(new ModuleBlueprintValidationError
                {
                    Message = new FixedString128Bytes(runtime.ValidationErrors[i])
                });
            }

            ValidateBlueprintModuleIds(ref state, runtime, errors);
            state.Enabled = false;
        }

        private void ValidateBlueprintModuleIds(ref SystemState state, ModuleBlueprintCatalogRuntime runtime, DynamicBuffer<ModuleBlueprintValidationError> errors)
        {
            if (!SystemAPI.TryGetSingleton<ModuleCatalogSingleton>(out var catalogSingleton) || !catalogSingleton.Catalog.IsCreated)
            {
                return;
            }

            ref var modules = ref catalogSingleton.Catalog.Value.Modules;
            foreach (var pair in runtime.Blueprints)
            {
                var exists = false;
                for (var i = 0; i < modules.Length; i++)
                {
                    if (modules[i].Id.Equals(pair.Value.BaseModuleIdFixed))
                    {
                        exists = true;
                        break;
                    }
                }

                if (exists)
                {
                    continue;
                }

                errors.Add(new ModuleBlueprintValidationError
                {
                    Message = new FixedString128Bytes($"blueprint '{pair.Key}' missing base module '{pair.Value.BaseModuleId}'")
                });
            }
        }
    }

    [UpdateInGroup(typeof(AISystemGroup))]
    [UpdateAfter(typeof(Space4XModuleCatalogStampSystem))]
    public partial struct Space4XModuleBlueprintAutoAssignSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ModuleTypeId>();
            state.RequireForUpdate<ModuleCatalogSingleton>();
            state.RequireForUpdate<ModuleBlueprintCatalogStatus>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var runtime = Space4XModuleBlueprintCatalogStore.Runtime;
            if (runtime == null)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<ModuleCatalogSingleton>(out var catalogSingleton) || !catalogSingleton.Catalog.IsCreated)
            {
                return;
            }

            ref var modules = ref catalogSingleton.Catalog.Value.Modules;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (moduleType, entity) in SystemAPI.Query<RefRO<ModuleTypeId>>().WithEntityAccess())
            {
                if (state.EntityManager.HasComponent<ModuleBlueprintRef>(entity))
                {
                    if (!state.EntityManager.HasBuffer<ModuleBlueprintPartId>(entity))
                    {
                        ecb.AddBuffer<ModuleBlueprintPartId>(entity);
                    }
                    continue;
                }

                var moduleId = moduleType.ValueRO.Value.ToString();
                RegistryModuleSpec spec = default;
                var hasSpec = false;
                for (var i = 0; i < modules.Length; i++)
                {
                    if (!modules[i].Id.Equals(moduleType.ValueRO.Value))
                    {
                        continue;
                    }

                    spec = modules[i];
                    hasSpec = true;
                    break;
                }

                runtime.BlueprintsByBaseModule.TryGetValue(moduleId, out var candidates);
                ModuleBlueprintSpecRuntime chosen = null;
                if (candidates != null && candidates.Count > 0)
                {
                    chosen = candidates[0];
                }

                var manufacturer = chosen?.ManufacturerId ?? string.Empty;
                if (manufacturer.Length == 0 && hasSpec && !spec.ManufacturerId.IsEmpty)
                {
                    manufacturer = spec.ManufacturerId.ToString();
                }

                if (manufacturer.Length == 0)
                {
                    manufacturer = "baseline";
                }

                if (!runtime.Manufacturers.ContainsKey(manufacturer))
                {
                    manufacturer = "baseline";
                }

                var blueprintId = chosen?.BlueprintId ?? string.Empty;
                var reference = new ModuleBlueprintRef
                {
                    ManufacturerId = new FixedString64Bytes(manufacturer),
                    BlueprintId = new FixedString64Bytes(blueprintId),
                    StableHash = math.hash(new uint2(
                        StableStringHash(manufacturer),
                        StableStringHash(blueprintId)))
                };
                ecb.AddComponent(entity, reference);

                var buffer = ecb.AddBuffer<ModuleBlueprintPartId>(entity);
                if (chosen != null && chosen.Parts != null)
                {
                    for (var i = 0; i < chosen.Parts.Length; i++)
                    {
                        var partId = (chosen.Parts[i] ?? string.Empty).Trim();
                        if (partId.Length == 0 || !runtime.Parts.TryGetValue(partId, out var partRuntime))
                        {
                            continue;
                        }

                        buffer.Add(new ModuleBlueprintPartId
                        {
                            PartId = partRuntime.IdFixed,
                            SlotType = partRuntime.SlotTypeFixed
                        });
                    }
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private static uint StableStringHash(string value)
        {
            var hash = 2166136261u;
            if (!string.IsNullOrEmpty(value))
            {
                for (var i = 0; i < value.Length; i++)
                {
                    hash = (hash ^ value[i]) * 16777619u;
                }
            }

            return (hash ^ 0u) * 16777619u;
        }
    }

    [UpdateInGroup(typeof(AISystemGroup))]
    [UpdateAfter(typeof(Space4XModuleBlueprintAutoAssignSystem))]
    public partial struct Space4XModuleDerivedStatsSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ModuleTypeId>();
            state.RequireForUpdate<ModuleCatalogSingleton>();
            state.RequireForUpdate<ModuleBlueprintCatalogStatus>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<ModuleCatalogSingleton>(out var catalogSingleton) || !catalogSingleton.Catalog.IsCreated)
            {
                return;
            }

            ref var modules = ref catalogSingleton.Catalog.Value.Modules;

            var runPerks = default(NativeArray<ModuleBlueprintRunPerkOp>);
            if (SystemAPI.TryGetSingletonBuffer<ModuleBlueprintRunPerkOp>(out var runPerkBuffer))
            {
                runPerks = runPerkBuffer.AsNativeArray();
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            try
            {
                var queuedStructuralChanges = false;
                foreach (var (_, entity) in SystemAPI.Query<RefRO<ModuleTypeId>>().WithAll<ModuleBlueprintRef, ModuleBlueprintPartId>().WithEntityAccess())
                {
                    if (!state.EntityManager.HasBuffer<ModuleDerivedStat>(entity))
                    {
                        ecb.AddBuffer<ModuleDerivedStat>(entity);
                        queuedStructuralChanges = true;
                    }

                    if (!state.EntityManager.HasBuffer<ModuleDerivedTag>(entity))
                    {
                        ecb.AddBuffer<ModuleDerivedTag>(entity);
                        queuedStructuralChanges = true;
                    }

                    if (!state.EntityManager.HasBuffer<ModuleDerivedEffectOp>(entity))
                    {
                        ecb.AddBuffer<ModuleDerivedEffectOp>(entity);
                        queuedStructuralChanges = true;
                    }

                    if (!state.EntityManager.HasComponent<ModuleDerivedWeaponProfile>(entity))
                    {
                        ecb.AddComponent(entity, default(ModuleDerivedWeaponProfile));
                        queuedStructuralChanges = true;
                    }

                    if (!state.EntityManager.HasComponent<ModuleDerivedReactorProfile>(entity))
                    {
                        ecb.AddComponent(entity, default(ModuleDerivedReactorProfile));
                        queuedStructuralChanges = true;
                    }

                    if (!state.EntityManager.HasComponent<ModuleDerivedHangarProfile>(entity))
                    {
                        ecb.AddComponent(entity, default(ModuleDerivedHangarProfile));
                        queuedStructuralChanges = true;
                    }

                    if (!state.EntityManager.HasComponent<ModuleDerivedDigest>(entity))
                    {
                        ecb.AddComponent(entity, default(ModuleDerivedDigest));
                        queuedStructuralChanges = true;
                    }
                }

                if (queuedStructuralChanges)
                {
                    ecb.Playback(state.EntityManager);
                }
            }
            finally
            {
                ecb.Dispose();
            }

            foreach (var (moduleType, blueprintRef, parts, entity) in SystemAPI.Query<RefRO<ModuleTypeId>, RefRO<ModuleBlueprintRef>, DynamicBuffer<ModuleBlueprintPartId>>().WithEntityAccess())
            {
                if (!TryGetModuleSpec(ref modules, moduleType.ValueRO.Value, out var spec))
                {
                    continue;
                }

                if (!state.EntityManager.HasBuffer<ModuleDerivedStat>(entity) ||
                    !state.EntityManager.HasBuffer<ModuleDerivedTag>(entity) ||
                    !state.EntityManager.HasBuffer<ModuleDerivedEffectOp>(entity) ||
                    !state.EntityManager.HasComponent<ModuleDerivedWeaponProfile>(entity) ||
                    !state.EntityManager.HasComponent<ModuleDerivedReactorProfile>(entity) ||
                    !state.EntityManager.HasComponent<ModuleDerivedHangarProfile>(entity) ||
                    !state.EntityManager.HasComponent<ModuleDerivedDigest>(entity))
                {
                    continue;
                }

                var stats = state.EntityManager.GetBuffer<ModuleDerivedStat>(entity);
                var tags = state.EntityManager.GetBuffer<ModuleDerivedTag>(entity);
                var effects = state.EntityManager.GetBuffer<ModuleDerivedEffectOp>(entity);
                effects.Clear();

                var result = Space4XModuleBlueprintResolver.Resolve(spec, blueprintRef.ValueRO, parts, tags, effects, runPerks);

                stats.Clear();
                AddStat(stats, ModuleDerivedStatId.Damage, result.Damage);
                AddStat(stats, ModuleDerivedStatId.FireRate, result.FireRate);
                AddStat(stats, ModuleDerivedStatId.Range, result.Range);
                AddStat(stats, ModuleDerivedStatId.EnergyCost, result.EnergyCost);
                AddStat(stats, ModuleDerivedStatId.HeatCost, result.HeatCost);
                AddStat(stats, ModuleDerivedStatId.PowerOutput, result.PowerOutput);
                AddStat(stats, ModuleDerivedStatId.HeatCapacity, result.HeatCapacity);
                AddStat(stats, ModuleDerivedStatId.HeatDissipation, result.HeatDissipation);
                AddStat(stats, ModuleDerivedStatId.Mass, result.Mass);
                AddStat(stats, ModuleDerivedStatId.DroneCapacity, result.DroneCapacity);

                var baseDamage = math.max(1f, spec.OffenseRating * 10f);
                var baseFireRate = 1f;
                var baseRange = 1f;
                var baseEnergy = math.max(0.01f, math.max(0f, spec.PowerDrawMW));
                var baseHeat = 1f;

                var weaponProfile = new ModuleDerivedWeaponProfile
                {
                    DamageScalar = result.Damage / baseDamage,
                    FireRateScalar = result.FireRate / baseFireRate,
                    RangeScalar = result.Range / baseRange,
                    EnergyCostScalar = result.EnergyCost / baseEnergy,
                    HeatCostScalar = result.HeatCost / baseHeat,
                    AttackFamily = result.AttackFamily,
                    DamageType = result.DamageType,
                    Delivery = result.Delivery
                };
                state.EntityManager.SetComponentData(entity, weaponProfile);

                var reactorProfile = new ModuleDerivedReactorProfile
                {
                    PowerOutputMW = result.PowerOutput,
                    HeatCapacity = result.HeatCapacity,
                    HeatDissipation = result.HeatDissipation,
                    MassTons = result.Mass
                };
                state.EntityManager.SetComponentData(entity, reactorProfile);

                var hangarProfile = new ModuleDerivedHangarProfile
                {
                    DroneCapacity = result.DroneCapacity,
                    DroneAttackFamily = result.AttackFamily == WeaponFamily.Unknown ? WeaponFamily.Kinetic : result.AttackFamily
                };
                state.EntityManager.SetComponentData(entity, hangarProfile);

                state.EntityManager.SetComponentData(entity, new ModuleDerivedDigest { Value = result.Digest });
            }

            if (runPerks.IsCreated)
            {
                runPerks.Dispose();
            }
        }

        private static bool TryGetModuleSpec(ref BlobArray<RegistryModuleSpec> modules, in FixedString64Bytes moduleId, out RegistryModuleSpec spec)
        {
            spec = default;
            for (var i = 0; i < modules.Length; i++)
            {
                if (modules[i].Id.Equals(moduleId))
                {
                    spec = modules[i];
                    return true;
                }
            }

            return false;
        }

        private static void AddStat(DynamicBuffer<ModuleDerivedStat> stats, ModuleDerivedStatId statId, float value)
        {
            stats.Add(new ModuleDerivedStat
            {
                StatId = statId,
                Value = value
            });
        }
    }

    [UpdateInGroup(typeof(AISystemGroup))]
    [UpdateAfter(typeof(Space4XModuleAttachmentSyncSystem))]
    [UpdateAfter(typeof(Space4XModuleDerivedStatsSystem))]
    public partial struct Space4XCarrierDerivedReactorAggregationSystem : ISystem
    {
        private ComponentLookup<ModuleDerivedReactorProfile> _moduleReactorLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ModuleAttachment>();
            _moduleReactorLookup = state.GetComponentLookup<ModuleDerivedReactorProfile>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            _moduleReactorLookup.Update(ref state);

            foreach (var (modules, owner) in SystemAPI.Query<DynamicBuffer<ModuleAttachment>>().WithEntityAccess())
            {
                var output = 0f;
                var heatCapacity = 0f;
                var heatDissipation = 0f;
                var mass = 0f;

                for (var i = 0; i < modules.Length; i++)
                {
                    var module = modules[i].Module;
                    if (module == Entity.Null || !_moduleReactorLookup.HasComponent(module))
                    {
                        continue;
                    }

                    var reactor = _moduleReactorLookup[module];
                    if (reactor.PowerOutputMW <= 0f)
                    {
                        continue;
                    }

                    output += reactor.PowerOutputMW;
                    heatCapacity += math.max(0f, reactor.HeatCapacity);
                    heatDissipation += math.max(0f, reactor.HeatDissipation);
                    mass += math.max(0f, reactor.MassTons);
                }

                if (output <= 0f)
                {
                    continue;
                }

                var efficiency = heatCapacity > 0f ? math.saturate(heatDissipation / heatCapacity) : 0.8f;
                var reactorSpec = new ShipReactorSpec
                {
                    Type = Space4XReactorType.FusionStandard,
                    OutputMW = output,
                    Efficiency = math.max(0.1f, efficiency),
                    IdleDrawMW = math.max(1f, mass * 0.02f),
                    HotRestartSeconds = 2.5f,
                    ColdRestartSeconds = 8f
                };

                if (state.EntityManager.HasComponent<ShipReactorSpec>(owner))
                {
                    state.EntityManager.SetComponentData(owner, reactorSpec);
                }
                else
                {
                    state.EntityManager.AddComponentData(owner, reactorSpec);
                }
            }
        }
    }

    [UpdateInGroup(typeof(AISystemGroup))]
    [UpdateAfter(typeof(Space4XModuleAttachmentSyncSystem))]
    [UpdateAfter(typeof(Space4XModuleDerivedStatsSystem))]
    public partial struct Space4XCarrierDerivedHangarAggregationSystem : ISystem
    {
        private ComponentLookup<ModuleDerivedHangarProfile> _moduleHangarLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ModuleAttachment>();
            _moduleHangarLookup = state.GetComponentLookup<ModuleDerivedHangarProfile>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            _moduleHangarLookup.Update(ref state);

            foreach (var (modules, owner) in SystemAPI.Query<DynamicBuffer<ModuleAttachment>>().WithEntityAccess())
            {
                var capacity = 0f;
                var energyBias = 0;
                var kineticBias = 0;
                var explosiveBias = 0;

                for (var i = 0; i < modules.Length; i++)
                {
                    var module = modules[i].Module;
                    if (module == Entity.Null || !_moduleHangarLookup.HasComponent(module))
                    {
                        continue;
                    }

                    var hangar = _moduleHangarLookup[module];
                    if (hangar.DroneCapacity <= 0f)
                    {
                        continue;
                    }

                    capacity += hangar.DroneCapacity;
                    switch (hangar.DroneAttackFamily)
                    {
                        case WeaponFamily.Energy:
                            energyBias++;
                            break;
                        case WeaponFamily.Kinetic:
                            kineticBias++;
                            break;
                        case WeaponFamily.Explosive:
                            explosiveBias++;
                            break;
                    }
                }

                if (capacity <= 0f)
                {
                    continue;
                }

                var family = WeaponFamily.Kinetic;
                if (energyBias >= kineticBias && energyBias >= explosiveBias)
                {
                    family = WeaponFamily.Energy;
                }
                else if (explosiveBias > kineticBias)
                {
                    family = WeaponFamily.Explosive;
                }

                var profile = new CarrierDerivedHangarProfile
                {
                    DroneCapacity = capacity,
                    DroneAttackFamily = family
                };

                if (state.EntityManager.HasComponent<CarrierDerivedHangarProfile>(owner))
                {
                    state.EntityManager.SetComponentData(owner, profile);
                }
                else
                {
                    state.EntityManager.AddComponentData(owner, profile);
                }

                var hangarCapacity = new HangarCapacity { Capacity = capacity };
                if (state.EntityManager.HasComponent<HangarCapacity>(owner))
                {
                    state.EntityManager.SetComponentData(owner, hangarCapacity);
                }
                else
                {
                    state.EntityManager.AddComponentData(owner, hangarCapacity);
                }
            }
        }
    }

    [UpdateInGroup(typeof(AISystemGroup))]
    [UpdateAfter(typeof(Space4XCarrierDerivedHangarAggregationSystem))]
    public partial struct Space4XStrikeCraftDerivedHangarWeaponSystem : ISystem
    {
        private ComponentLookup<CarrierDerivedHangarProfile> _hangarLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StrikeCraftProfile>();
            _hangarLookup = state.GetComponentLookup<CarrierDerivedHangarProfile>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            _hangarLookup.Update(ref state);

            foreach (var (profile, config, mounts) in SystemAPI.Query<RefRO<StrikeCraftProfile>, RefRW<AttackRunConfig>, DynamicBuffer<WeaponMount>>())
            {
                var carrier = profile.ValueRO.Carrier;
                if (carrier == Entity.Null || !_hangarLookup.HasComponent(carrier))
                {
                    continue;
                }

                var hangar = _hangarLookup[carrier];
                var damageType = hangar.DroneAttackFamily switch
                {
                    WeaponFamily.Energy => Space4XDamageType.Energy,
                    WeaponFamily.Explosive => Space4XDamageType.Explosive,
                    _ => Space4XDamageType.Kinetic
                };

                config.ValueRW.DeliveryType = hangar.DroneAttackFamily switch
                {
                    WeaponFamily.Energy => WeaponDeliveryType.Strafe,
                    WeaponFamily.Explosive => WeaponDeliveryType.MissileSalvo,
                    _ => WeaponDeliveryType.Strafe
                };

                var delivery = hangar.DroneAttackFamily switch
                {
                    WeaponFamily.Energy => WeaponDelivery.Beam,
                    WeaponFamily.Explosive => WeaponDelivery.Guided,
                    _ => WeaponDelivery.Slug
                };

                var mountsBuffer = mounts;
                for (var i = 0; i < mountsBuffer.Length; i++)
                {
                    var mount = mountsBuffer[i];
                    var weapon = mount.Weapon;
                    weapon.Family = hangar.DroneAttackFamily;
                    weapon.DamageType = damageType;
                    weapon.Delivery = delivery;
                    mount.Weapon = weapon;
                    mountsBuffer[i] = mount;
                }
            }
        }
    }
}
