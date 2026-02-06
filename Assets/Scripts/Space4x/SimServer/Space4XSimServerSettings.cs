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
            return new Space4XSimServerConfig
            {
                Seed = (uint)ReadInt(SeedEnv, 1337),
                FactionCount = (ushort)Mathf.Clamp(ReadInt(FactionsEnv, 4), 1, 32),
                SystemsPerFaction = (ushort)Mathf.Clamp(ReadInt(SystemsPerFactionEnv, 3), 1, 16),
                ResourcesPerSystem = (ushort)Mathf.Clamp(ReadInt(ResourcesPerSystemEnv, 6), 1, 64),
                StartRadius = ReadFloat(StartRadiusEnv, 5000f),
                SystemSpacing = ReadFloat(SystemSpacingEnv, 2000f),
                ResourceBaseUnits = ReadFloat(ResourceBaseEnv, 800f),
                ResourceRichnessGradient = ReadFloat(ResourceGradientEnv, 0.2f),
                TechDiffusionDurationSeconds = ReadFloat(TechDurationEnv, 14400f),
                TargetTicksPerSecond = Mathf.Clamp(ReadFloat(TargetTpsEnv, 2f), 0.5f, 120f),
                HttpPort = (ushort)Mathf.Clamp(ReadInt(PortEnv, 45100), 1024, 65535),
                AutosaveSeconds = Mathf.Max(0f, ReadFloat(AutosaveEnv, 0f)),
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
    }
}
