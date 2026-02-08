using System;
using PureDOTS.Runtime.WorldGen;
using UnityEngine;

namespace Space4X.SimServer
{
    internal static class Space4XSimServerSettings
    {
        private const string EnableEnv = "SPACE4X_SIM_SERVER";
        private const string EnableArg = "--space4x-sim-server";
        private const string PortEnv = "SPACE4X_SIM_HTTP_PORT";
        private const string HostEnv = "SPACE4X_SIM_HTTP_HOST";
        private const string SeedEnv = "SPACE4X_SIM_SEED";
        private const string FactionsEnv = "SPACE4X_SIM_FACTIONS";
        private const string SystemsPerFactionEnv = "SPACE4X_SIM_SYSTEMS_PER_FACTION";
        private const string ResourcesPerSystemEnv = "SPACE4X_SIM_RESOURCES_PER_SYSTEM";
        private const string StartRadiusEnv = "SPACE4X_SIM_START_RADIUS";
        private const string SystemSpacingEnv = "SPACE4X_SIM_SYSTEM_SPACING";
        private const string ResourceBaseEnv = "SPACE4X_SIM_RESOURCE_BASE";
        private const string ResourceGradientEnv = "SPACE4X_SIM_RESOURCE_GRADIENT";
        private const string ResourceBiasChanceEnv = "SPACE4X_SIM_RESOURCE_BIAS_CHANCE";
        private const string TargetTpsEnv = "SPACE4X_SIM_TPS";
        private const string TechDurationEnv = "SPACE4X_SIM_TECH_DURATION";
        private const string AutosaveEnv = "SPACE4X_SIM_AUTOSAVE_SECONDS";

        internal static bool IsEnabled()
        {
            return EnvIsTruthy(EnableEnv) || HasArg(EnableArg);
        }

        internal static string ResolveHost()
        {
            var host = System.Environment.GetEnvironmentVariable(HostEnv);
            if (string.IsNullOrWhiteSpace(host))
            {
                return "localhost";
            }

            return host.Trim();
        }

        internal static Space4XSimServerConfig ResolveConfig()
        {
            var config = new Space4XSimServerConfig
            {
                Seed = 1337u,
                FactionCount = 4,
                SystemsPerFaction = 3,
                ResourcesPerSystem = 6,
                StartRadius = 5000f,
                SystemSpacing = 2000f,
                ResourceBaseUnits = 800f,
                ResourceRichnessGradient = 0.2f,
                ResourceBiasChance = -1f,
                TechDiffusionDurationSeconds = 14400f,
                TargetTicksPerSecond = 2f,
                HttpPort = 45100,
                AutosaveSeconds = 0f,
                FoodPerPopPerSecond = 0.0003f,
                WaterPerPopPerSecond = 0.0003f,
                FuelPerPopPerSecond = 0.00015f,
                SuppliesConsumptionPerPopPerSecond = 0.0001f,
                TraitMask = GalaxySystemTraitMask.All,
                PoiMask = GalaxyPoiMask.All,
                MaxTraitsPerSystem = 1,
                MaxPoisPerSystem = 1,
                TraitChanceBase = 0.35f,
                TraitChancePerRing = 0.1f,
                TraitChanceMax = 0.8f,
                PoiChanceBase = 0.25f,
                PoiChancePerRing = 0.12f,
                PoiChanceMax = 0.75f,
                PoiOffsetMin = 450f,
                PoiOffsetMax = 900f
            };

            Space4XSimServerUserConfig.EnsureDefaultConfigFileExists();
            if (Space4XSimServerUserConfig.TryLoad(out var userConfig))
            {
                Space4XSimServerUserConfig.ApplyOverrides(ref config, userConfig);
            }

            ApplyEnvOverrides(ref config);
            ClampConfig(ref config);
            return config;
        }

        private static bool EnvIsTruthy(string key)
        {
            var v = System.Environment.GetEnvironmentVariable(key);
            return string.Equals(v, "1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(v, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(v, "yes", StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasArg(string key)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static int ReadInt(string key, int fallback)
        {
            var v = System.Environment.GetEnvironmentVariable(key);
            return int.TryParse(v, out var parsed) ? parsed : fallback;
        }

        private static float ReadFloat(string key, float fallback)
        {
            var v = System.Environment.GetEnvironmentVariable(key);
            return float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : fallback;
        }

        internal static void ApplyEnvOverrides(ref Space4XSimServerConfig config)
        {
            if (TryReadInt(SeedEnv, out var seed)) config.Seed = (uint)seed;
            if (TryReadInt(FactionsEnv, out var factions)) config.FactionCount = (ushort)factions;
            if (TryReadInt(SystemsPerFactionEnv, out var systems)) config.SystemsPerFaction = (ushort)systems;
            if (TryReadInt(ResourcesPerSystemEnv, out var resources)) config.ResourcesPerSystem = (ushort)resources;
            if (TryReadFloat(StartRadiusEnv, out var startRadius)) config.StartRadius = startRadius;
            if (TryReadFloat(SystemSpacingEnv, out var systemSpacing)) config.SystemSpacing = systemSpacing;
            if (TryReadFloat(ResourceBaseEnv, out var resourceBase)) config.ResourceBaseUnits = resourceBase;
            if (TryReadFloat(ResourceGradientEnv, out var gradient)) config.ResourceRichnessGradient = gradient;
            if (TryReadFloat(ResourceBiasChanceEnv, out var biasChance)) config.ResourceBiasChance = biasChance;
            if (TryReadFloat(TechDurationEnv, out var techDuration)) config.TechDiffusionDurationSeconds = techDuration;
            if (TryReadFloat(TargetTpsEnv, out var targetTps)) config.TargetTicksPerSecond = targetTps;
            if (TryReadInt(PortEnv, out var port)) config.HttpPort = (ushort)port;
            if (TryReadFloat(AutosaveEnv, out var autosave)) config.AutosaveSeconds = autosave;
        }

        internal static void ClampConfig(ref Space4XSimServerConfig config)
        {
            config.FactionCount = (ushort)Mathf.Clamp(config.FactionCount, 1, 32);
            config.SystemsPerFaction = (ushort)Mathf.Clamp(config.SystemsPerFaction, 1, 16);
            config.ResourcesPerSystem = (ushort)Mathf.Clamp(config.ResourcesPerSystem, 1, 64);
            config.TargetTicksPerSecond = Mathf.Clamp(config.TargetTicksPerSecond, 0.5f, 120f);
            config.HttpPort = (ushort)Mathf.Clamp(config.HttpPort, 1024, 65535);
            config.AutosaveSeconds = Mathf.Max(0f, config.AutosaveSeconds);
            config.FoodPerPopPerSecond = Mathf.Max(0f, config.FoodPerPopPerSecond);
            config.WaterPerPopPerSecond = Mathf.Max(0f, config.WaterPerPopPerSecond);
            config.FuelPerPopPerSecond = Mathf.Max(0f, config.FuelPerPopPerSecond);
            config.SuppliesConsumptionPerPopPerSecond = Mathf.Max(0f, config.SuppliesConsumptionPerPopPerSecond);
            if (config.ResourceBiasChance >= 0f)
            {
                config.ResourceBiasChance = Mathf.Clamp01(config.ResourceBiasChance);
            }
        }

        private static bool TryReadInt(string key, out int value)
        {
            var v = System.Environment.GetEnvironmentVariable(key);
            return int.TryParse(v, out value);
        }

        private static bool TryReadFloat(string key, out float value)
        {
            var v = System.Environment.GetEnvironmentVariable(key);
            return float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value);
        }
    }
}
