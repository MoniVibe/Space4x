using System;
using Space4X.Modes;
using Unity.Collections;

namespace Space4x.Scenario
{
    /// <summary>
    /// Single authority for playable FleetCrawl scenario routing.
    /// Legacy scenario variants remain available only via explicit env opt-in.
    /// </summary>
    public static class Space4XScenarioAuthority
    {
        private const string LegacyFleetCrawlScenariosEnv = "SPACE4X_ENABLE_LEGACY_FLEETCRAWL_SCENARIOS";
        private const string KeepLegacyScenarioOverlaysEnv = "SPACE4X_KEEP_LEGACY_SCENARIO_OVERLAYS";
        private const string FleetCrawlPrefix = "space4x_fleetcrawl";
        private static readonly FixedString64Bytes CanonicalScenarioIdFixed =
            new FixedString64Bytes(Space4XModeSelectionState.FleetCrawlScenarioId);

        private static readonly string[] LegacyOverlayEnvVars =
        {
            "SPACE4X_SHOW_MODE_SELECTOR",
            "SPACE4X_FLEETCRAWL_DEBUG_UI",
            "SPACE4X_FLEETCRAWL_DEBUG_DRIVE"
        };

        public static string CanonicalScenarioId => Space4XModeSelectionState.FleetCrawlScenarioId;
        public static string CanonicalScenarioPath => Space4XModeSelectionState.FleetCrawlScenarioPath;
        public static uint CanonicalScenarioSeed => Space4XModeSelectionState.FleetCrawlSeed;

        public static void ResolvePlayableScenario(out string scenarioId, out string scenarioPath, out uint seed)
        {
            scenarioId = CanonicalScenarioId;
            scenarioPath = CanonicalScenarioPath;
            seed = CanonicalScenarioSeed;
        }

        public static void ApplyPlayableScenarioEnvironment()
        {
            Environment.SetEnvironmentVariable(Space4XModeSelectionState.ModeEnvVar, "fleetcrawl");
            Environment.SetEnvironmentVariable(Space4XModeSelectionState.ScenarioPathEnvVar, CanonicalScenarioPath);
        }

        public static void NormalizeLegacyScenarioOverlayEnvironment()
        {
            if (IsTruthyEnvironmentVariable(KeepLegacyScenarioOverlaysEnv))
            {
                return;
            }

            for (var i = 0; i < LegacyOverlayEnvVars.Length; i++)
            {
                Environment.SetEnvironmentVariable(LegacyOverlayEnvVars[i], string.Empty);
            }
        }

        public static bool IsPlayableFleetCrawlScenario(in FixedString64Bytes scenarioId)
        {
            if (IsCanonicalPlayableScenario(scenarioId))
            {
                return true;
            }

            if (!IsTruthyEnvironmentVariable(LegacyFleetCrawlScenariosEnv))
            {
                return false;
            }

            return IsAnyFleetCrawlScenario(scenarioId.ToString());
        }

        public static bool IsCanonicalPlayableScenario(in FixedString64Bytes scenarioId)
        {
            return scenarioId.Equals(CanonicalScenarioIdFixed);
        }

        public static bool IsCanonicalPlayableScenario(string scenarioId)
        {
            return string.Equals(scenarioId?.Trim(), CanonicalScenarioId, StringComparison.OrdinalIgnoreCase);
        }

        public static bool ShouldRunLegacyRoomDirector(string scenarioId)
        {
            if (!IsAnyFleetCrawlScenario(scenarioId))
            {
                return false;
            }

            if (IsCanonicalPlayableScenario(scenarioId))
            {
                return true;
            }

            return IsTruthyEnvironmentVariable(LegacyFleetCrawlScenariosEnv);
        }

        private static bool IsAnyFleetCrawlScenario(string scenarioId)
        {
            return !string.IsNullOrWhiteSpace(scenarioId) &&
                   scenarioId.StartsWith(FleetCrawlPrefix, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTruthyEnvironmentVariable(string envName)
        {
            var value = Environment.GetEnvironmentVariable(envName);
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var trimmed = value.Trim();
            return trimmed.Equals("1", StringComparison.OrdinalIgnoreCase)
                   || trimmed.Equals("true", StringComparison.OrdinalIgnoreCase)
                   || trimmed.Equals("yes", StringComparison.OrdinalIgnoreCase)
                   || trimmed.Equals("on", StringComparison.OrdinalIgnoreCase);
        }
    }
}
