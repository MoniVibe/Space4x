using System;
using System.IO;
using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UDebug = UnityEngine.Debug;

namespace Space4X.SimServer
{
    [Serializable]
    internal sealed class Space4XSimServerUserConfigFile
    {
        public SimConfigOverrides sim;
        public ResourceDistributionOverrides resourceDistribution;
        public BusinessFleetOverrides businessFleet;
    }

    [Serializable]
    internal sealed class SimConfigOverrides
    {
        public int seed = -1;
        public int factionCount = -1;
        public int systemsPerFaction = -1;
        public int resourcesPerSystem = -1;
        public float startRadius = -1f;
        public float systemSpacing = -1f;
        public float resourceBaseUnits = -1f;
        public float resourceRichnessGradient = -1f;
        public float resourceBiasChance = -1f;
        public float techDiffusionDurationSeconds = -1f;
        public float targetTicksPerSecond = -1f;
        public int httpPort = -1;
        public float autosaveSeconds = -1f;
        public float foodPerPopPerSecond = -1f;
        public float waterPerPopPerSecond = -1f;
        public float fuelPerPopPerSecond = -1f;
        public float suppliesConsumptionPerPopPerSecond = -1f;
    }

    [Serializable]
    internal sealed class ResourceDistributionOverrides
    {
        public float biasChance = -1f;
        public ResourceWeightOverrides[] weights;
    }

    [Serializable]
    internal sealed class ResourceWeightOverrides
    {
        public string type;
        public float innerWeight = -1f;
        public float midWeight = -1f;
        public float outerWeight = -1f;
        public float logisticsWeight = -1f;
        public float nebulaWeight = -1f;
        public float ancientCoreWeight = -1f;
        public float blackHoleWeight = -1f;
        public float neutronWeight = -1f;
        public float hazardWeight = -1f;
        public float superResourceWeight = -1f;
        public float ruinsWeight = -1f;
        public float gateWeight = -1f;
    }

    [Serializable]
    internal sealed class BusinessFleetOverrides
    {
        public string defaultHullId;
        public BusinessHullOverride[] hulls;
    }

    [Serializable]
    internal sealed class BusinessHullOverride
    {
        public string businessKind;
        public string hullId;
    }

    internal static class Space4XSimServerUserConfig
    {
        internal static void EnsureDefaultConfigFileExists()
        {
            var path = Space4XSimServerPaths.ConfigFile;
            if (string.IsNullOrWhiteSpace(path) || File.Exists(path))
            {
                return;
            }

            try
            {
                Space4XSimServerPaths.EnsureDirectories();
                var json = "{\n"
                    + "  \"notes\": \"Set any value to override. Use -1 to keep defaults.\",\n"
                    + "  \"sim\": {\n"
                    + "    \"seed\": -1,\n"
                    + "    \"factionCount\": -1,\n"
                    + "    \"systemsPerFaction\": -1,\n"
                    + "    \"resourcesPerSystem\": -1,\n"
                    + "    \"startRadius\": -1,\n"
                    + "    \"systemSpacing\": -1,\n"
                    + "    \"resourceBaseUnits\": -1,\n"
                    + "    \"resourceRichnessGradient\": -1,\n"
                    + "    \"resourceBiasChance\": -1,\n"
                    + "    \"techDiffusionDurationSeconds\": -1,\n"
                    + "    \"targetTicksPerSecond\": -1,\n"
                    + "    \"httpPort\": -1,\n"
                    + "    \"autosaveSeconds\": -1,\n"
                    + "    \"foodPerPopPerSecond\": -1,\n"
                    + "    \"waterPerPopPerSecond\": -1,\n"
                    + "    \"fuelPerPopPerSecond\": -1,\n"
                    + "    \"suppliesConsumptionPerPopPerSecond\": -1\n"
                    + "  },\n"
                    + "  \"resourceDistribution\": {\n"
                    + "    \"biasChance\": -1,\n"
                    + "    \"weights\": []\n"
                    + "  },\n"
                    + "  \"businessFleet\": {\n"
                    + "    \"defaultHullId\": \"lcv-sparrow\",\n"
                    + "    \"hulls\": [\n"
                    + "      { \"businessKind\": \"MarketHub\", \"hullId\": \"cv-mule\" },\n"
                    + "      { \"businessKind\": \"Shipwright\", \"hullId\": \"cv-mule\" },\n"
                    + "      { \"businessKind\": \"DeepCoreSyndicate\", \"hullId\": \"cv-mule\" }\n"
                    + "    ]\n"
                    + "  }\n"
                    + "}\n";
                File.WriteAllText(path, json);
                UDebug.Log($"[Space4XSimServer] Wrote default user config to '{path}'.");
            }
            catch (Exception ex)
            {
                UDebug.LogWarning($"[Space4XSimServer] Failed to write default user config '{path}': {ex.Message}");
            }
        }

        internal static bool TryLoad(out Space4XSimServerUserConfigFile config)
        {
            config = null;
            var path = Space4XSimServerPaths.ConfigFile;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return false;
            }

            try
            {
                var json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return false;
                }

                var instance = new Space4XSimServerUserConfigFile
                {
                    sim = new SimConfigOverrides(),
                    resourceDistribution = new ResourceDistributionOverrides(),
                    businessFleet = new BusinessFleetOverrides()
                };
                JsonUtility.FromJsonOverwrite(json, instance);
                ValidateConfig(instance, path);
                config = instance;
                return true;
            }
            catch (Exception ex)
            {
                UDebug.LogWarning($"[Space4XSimServer] Failed to load user config '{path}': {ex.Message}");
                return false;
            }
        }

        internal static void ApplyOverrides(ref Space4XSimServerConfig config, Space4XSimServerUserConfigFile file)
        {
            if (file?.sim == null)
            {
                return;
            }

            var sim = file.sim;
            if (sim.seed >= 0) config.Seed = (uint)sim.seed;
            if (sim.factionCount >= 0) config.FactionCount = (ushort)sim.factionCount;
            if (sim.systemsPerFaction >= 0) config.SystemsPerFaction = (ushort)sim.systemsPerFaction;
            if (sim.resourcesPerSystem >= 0) config.ResourcesPerSystem = (ushort)sim.resourcesPerSystem;
            if (sim.startRadius >= 0f) config.StartRadius = sim.startRadius;
            if (sim.systemSpacing >= 0f) config.SystemSpacing = sim.systemSpacing;
            if (sim.resourceBaseUnits >= 0f) config.ResourceBaseUnits = sim.resourceBaseUnits;
            if (sim.resourceRichnessGradient >= 0f) config.ResourceRichnessGradient = sim.resourceRichnessGradient;
            if (sim.resourceBiasChance >= 0f) config.ResourceBiasChance = sim.resourceBiasChance;
            if (sim.techDiffusionDurationSeconds >= 0f) config.TechDiffusionDurationSeconds = sim.techDiffusionDurationSeconds;
            if (sim.targetTicksPerSecond >= 0f) config.TargetTicksPerSecond = sim.targetTicksPerSecond;
            if (sim.httpPort >= 0) config.HttpPort = (ushort)sim.httpPort;
            if (sim.autosaveSeconds >= 0f) config.AutosaveSeconds = sim.autosaveSeconds;
            if (sim.foodPerPopPerSecond >= 0f) config.FoodPerPopPerSecond = sim.foodPerPopPerSecond;
            if (sim.waterPerPopPerSecond >= 0f) config.WaterPerPopPerSecond = sim.waterPerPopPerSecond;
            if (sim.fuelPerPopPerSecond >= 0f) config.FuelPerPopPerSecond = sim.fuelPerPopPerSecond;
            if (sim.suppliesConsumptionPerPopPerSecond >= 0f) config.SuppliesConsumptionPerPopPerSecond = sim.suppliesConsumptionPerPopPerSecond;
        }

        internal static void ApplyResourceDistributionOverrides(
            ref Space4XResourceDistributionConfig config,
            ref DynamicBuffer<Space4XResourceWeightEntry> weights,
            Space4XSimServerUserConfigFile file)
        {
            if (file?.resourceDistribution == null)
            {
                return;
            }

            var dist = file.resourceDistribution;
            if (dist.biasChance >= 0f)
            {
                config.BiasChance = math.saturate(dist.biasChance);
            }

            if (dist.weights == null || dist.weights.Length == 0)
            {
                return;
            }

            for (int i = 0; i < dist.weights.Length; i++)
            {
                var entry = dist.weights[i];
                if (!TryParseResourceType(entry.type, out var type))
                {
                    continue;
                }

                var index = (int)type;
                if (index < 0 || index >= weights.Length)
                {
                    continue;
                }

                var weightEntry = weights[index];
                if (entry.innerWeight >= 0f) weightEntry.InnerWeight = entry.innerWeight;
                if (entry.midWeight >= 0f) weightEntry.MidWeight = entry.midWeight;
                if (entry.outerWeight >= 0f) weightEntry.OuterWeight = entry.outerWeight;
                if (entry.logisticsWeight >= 0f) weightEntry.LogisticsWeight = entry.logisticsWeight;
                if (entry.nebulaWeight >= 0f) weightEntry.NebulaWeight = entry.nebulaWeight;
                if (entry.ancientCoreWeight >= 0f) weightEntry.AncientCoreWeight = entry.ancientCoreWeight;
                if (entry.blackHoleWeight >= 0f) weightEntry.BlackHoleWeight = entry.blackHoleWeight;
                if (entry.neutronWeight >= 0f) weightEntry.NeutronWeight = entry.neutronWeight;
                if (entry.hazardWeight >= 0f) weightEntry.HazardWeight = entry.hazardWeight;
                if (entry.superResourceWeight >= 0f) weightEntry.SuperResourceWeight = entry.superResourceWeight;
                if (entry.ruinsWeight >= 0f) weightEntry.RuinsWeight = entry.ruinsWeight;
                if (entry.gateWeight >= 0f) weightEntry.GateWeight = entry.gateWeight;
                weights[index] = weightEntry;
            }
        }

        internal static void ApplyBusinessFleetOverrides(
            ref Space4XBusinessFleetSeedConfig config,
            ref DynamicBuffer<Space4XBusinessFleetSeedOverride> overrides,
            Space4XSimServerUserConfigFile file)
        {
            if (file?.businessFleet == null)
            {
                return;
            }

            var fleet = file.businessFleet;
            if (!string.IsNullOrWhiteSpace(fleet.defaultHullId))
            {
                config.DefaultHullId = new FixedString64Bytes(fleet.defaultHullId);
            }

            if (fleet.hulls == null || fleet.hulls.Length == 0)
            {
                return;
            }

            for (int i = 0; i < fleet.hulls.Length; i++)
            {
                var entry = fleet.hulls[i];
                if (!TryParseBusinessKind(entry.businessKind, out var kind))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entry.hullId))
                {
                    continue;
                }

                var found = false;
                for (int j = 0; j < overrides.Length; j++)
                {
                    if (overrides[j].BusinessKind == kind)
                    {
                        overrides[j] = new Space4XBusinessFleetSeedOverride
                        {
                            BusinessKind = kind,
                            HullId = new FixedString64Bytes(entry.hullId)
                        };
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    overrides.Add(new Space4XBusinessFleetSeedOverride
                    {
                        BusinessKind = kind,
                        HullId = new FixedString64Bytes(entry.hullId)
                    });
                }
            }
        }

        private static bool TryParseResourceType(string value, out ResourceType type)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                type = default;
                return false;
            }

            return Enum.TryParse(value, true, out type);
        }

        private static bool TryParseBusinessKind(string value, out Space4XBusinessKind kind)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                kind = default;
                return false;
            }

            return Enum.TryParse(value, true, out kind);
        }

        private static void ValidateConfig(Space4XSimServerUserConfigFile config, string path)
        {
            if (config == null)
            {
                return;
            }

            if (config.sim != null)
            {
                ValidateFloat(config.sim.startRadius, "sim.startRadius", path);
                ValidateFloat(config.sim.systemSpacing, "sim.systemSpacing", path);
                ValidateFloat(config.sim.resourceBaseUnits, "sim.resourceBaseUnits", path);
                ValidateFloat(config.sim.resourceRichnessGradient, "sim.resourceRichnessGradient", path);
                ValidateFloat(config.sim.resourceBiasChance, "sim.resourceBiasChance", path);
                ValidateFloat(config.sim.techDiffusionDurationSeconds, "sim.techDiffusionDurationSeconds", path);
                ValidateFloat(config.sim.targetTicksPerSecond, "sim.targetTicksPerSecond", path);
                ValidateFloat(config.sim.autosaveSeconds, "sim.autosaveSeconds", path);
                ValidateFloat(config.sim.foodPerPopPerSecond, "sim.foodPerPopPerSecond", path);
                ValidateFloat(config.sim.waterPerPopPerSecond, "sim.waterPerPopPerSecond", path);
                ValidateFloat(config.sim.fuelPerPopPerSecond, "sim.fuelPerPopPerSecond", path);
                ValidateFloat(config.sim.suppliesConsumptionPerPopPerSecond, "sim.suppliesConsumptionPerPopPerSecond", path);
            }

            var dist = config.resourceDistribution;
            if (dist != null)
            {
                ValidateFloat(dist.biasChance, "resourceDistribution.biasChance", path);
                if (dist.weights != null)
                {
                    for (int i = 0; i < dist.weights.Length; i++)
                    {
                        var entry = dist.weights[i];
                        if (!string.IsNullOrWhiteSpace(entry.type) && !TryParseResourceType(entry.type, out _))
                        {
                            UDebug.LogWarning($"[Space4XSimServer] Unknown resource type '{entry.type}' in {path} (resourceDistribution.weights[{i}]).");
                        }

                        ValidateFloat(entry.innerWeight, $"resourceDistribution.weights[{i}].innerWeight", path);
                        ValidateFloat(entry.midWeight, $"resourceDistribution.weights[{i}].midWeight", path);
                        ValidateFloat(entry.outerWeight, $"resourceDistribution.weights[{i}].outerWeight", path);
                        ValidateFloat(entry.logisticsWeight, $"resourceDistribution.weights[{i}].logisticsWeight", path);
                        ValidateFloat(entry.nebulaWeight, $"resourceDistribution.weights[{i}].nebulaWeight", path);
                        ValidateFloat(entry.ancientCoreWeight, $"resourceDistribution.weights[{i}].ancientCoreWeight", path);
                        ValidateFloat(entry.blackHoleWeight, $"resourceDistribution.weights[{i}].blackHoleWeight", path);
                        ValidateFloat(entry.neutronWeight, $"resourceDistribution.weights[{i}].neutronWeight", path);
                        ValidateFloat(entry.hazardWeight, $"resourceDistribution.weights[{i}].hazardWeight", path);
                        ValidateFloat(entry.superResourceWeight, $"resourceDistribution.weights[{i}].superResourceWeight", path);
                        ValidateFloat(entry.ruinsWeight, $"resourceDistribution.weights[{i}].ruinsWeight", path);
                        ValidateFloat(entry.gateWeight, $"resourceDistribution.weights[{i}].gateWeight", path);
                    }
                }
            }

            var fleet = config.businessFleet;
            if (fleet != null)
            {
                if (!string.IsNullOrWhiteSpace(fleet.defaultHullId))
                {
                    ValidateString(fleet.defaultHullId, "businessFleet.defaultHullId", path);
                }

                if (fleet.hulls != null)
                {
                    for (int i = 0; i < fleet.hulls.Length; i++)
                    {
                        var entry = fleet.hulls[i];
                        if (!string.IsNullOrWhiteSpace(entry.businessKind) && !TryParseBusinessKind(entry.businessKind, out _))
                        {
                            UDebug.LogWarning($"[Space4XSimServer] Unknown business kind '{entry.businessKind}' in {path} (businessFleet.hulls[{i}]).");
                        }

                        if (!string.IsNullOrWhiteSpace(entry.hullId))
                        {
                            ValidateString(entry.hullId, $"businessFleet.hulls[{i}].hullId", path);
                        }
                    }
                }
            }
        }

        private static void ValidateFloat(float value, string field, string path)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                UDebug.LogWarning($"[Space4XSimServer] Invalid float for {field} in {path} (NaN/Infinity).");
            }
        }

        private static void ValidateString(string value, string field, string path)
        {
            if (value != null && value.Length > 64)
            {
                UDebug.LogWarning($"[Space4XSimServer] String for {field} in {path} exceeds 64 characters.");
            }
        }
    }
}
