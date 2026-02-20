using System;
using Space4X.Registry;
using UnityEngine;

namespace Space4X.UI
{
    /// <summary>
    /// Runtime handoff payload for launching a playable run from the frontend.
    /// </summary>
    public static class Space4XRunStartSelection
    {
        public const string SmokeScenarioId = "space4x_smoke";
        public const string SmokeScenarioPath = "Assets/Scenarios/space4x_smoke.json";
        public const uint SmokeScenarioSeed = 77u;

        public const string FleetCrawlCanonicalScenarioId = "space4x_fleetcrawl_core_micro";
        public const string FleetCrawlCanonicalScenarioPath = "Assets/Scenarios/space4x_fleetcrawl_core_micro.json";
        public const uint FleetCrawlCanonicalSeedBase = 19021u;

        public static string ShipPresetId { get; private set; } = "ship.square.carrier";
        public static string ShipDisplayName { get; private set; } = "Square Carrier";
        public static int Difficulty { get; private set; } = 2;
        public static string ScenePath { get; private set; } = Space4XShipPresetCatalog.DefaultGameplayScenePath;
        public static string ScenarioId { get; private set; } = SmokeScenarioId;
        public static string ScenarioPath { get; private set; } = SmokeScenarioPath;
        public static uint ScenarioSeed { get; private set; } = SmokeScenarioSeed;
        public static ShipFlightProfile FlightProfile { get; private set; } = ShipFlightProfile.CreateDefault("ship.square.carrier");
        public static DateTime StartedUtc { get; private set; } = DateTime.MinValue;
        public static bool ScenarioSelectionPending { get; private set; }

        public static bool HasActiveSelection => StartedUtc != DateTime.MinValue;

        public static void Set(in Space4XShipPresetEntry preset, int difficulty, string scenePath)
        {
            ShipPresetId = preset.PresetId;
            ShipDisplayName = preset.DisplayName;
            Difficulty = Mathf.Max(1, difficulty);
            ScenePath = string.IsNullOrWhiteSpace(scenePath)
                ? Space4XShipPresetCatalog.DefaultGameplayScenePath
                : scenePath;
            FlightProfile = preset.FlightProfile;
            ResolveScenarioRouting(Difficulty, out var scenarioId, out var scenarioPath, out var scenarioSeed);
            ScenarioId = scenarioId;
            ScenarioPath = scenarioPath;
            ScenarioSeed = scenarioSeed;
            ScenarioSelectionPending = true;
            StartedUtc = DateTime.UtcNow;
        }

        public static bool TryGetScenarioSelection(out string scenarioId, out string scenarioPath, out uint seed)
        {
            scenarioId = ScenarioId;
            scenarioPath = ScenarioPath;
            seed = ScenarioSeed;

            if (!HasActiveSelection || !ScenarioSelectionPending)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(scenarioId))
            {
                scenarioId = SmokeScenarioId;
            }

            if (string.IsNullOrWhiteSpace(scenarioPath))
            {
                scenarioPath = SmokeScenarioPath;
            }

            if (seed == 0u)
            {
                seed = SmokeScenarioSeed;
            }

            return true;
        }

        public static void MarkScenarioSelectionApplied()
        {
            ScenarioSelectionPending = false;
        }

        private static void ResolveScenarioRouting(int difficulty, out string scenarioId, out string scenarioPath, out uint seed)
        {
            scenarioId = FleetCrawlCanonicalScenarioId;
            scenarioPath = FleetCrawlCanonicalScenarioPath;
            seed = FleetCrawlCanonicalSeedBase + (uint)(Mathf.Clamp(difficulty, 1, 9) * 97);
        }
    }
}
