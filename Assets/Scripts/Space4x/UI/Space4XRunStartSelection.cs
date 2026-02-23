using System;
using Space4X.Modes;
using Space4X.Registry;
using Space4x.Scenario;
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

        public static string ShipPresetId { get; private set; } = "ship.square.carrier";
        public static string ShipDisplayName { get; private set; } = "Square Carrier";
        public static string Archetype { get; private set; } = "Unknown Archetype";
        public static string[] HullSegments { get; private set; } = Array.Empty<string>();
        public static string[] StartingModules { get; private set; } = Array.Empty<string>();
        public static string[] MetaPerks { get; private set; } = Array.Empty<string>();
        public static int Difficulty { get; private set; } = 2;
        public static string ScenePath { get; private set; } = Space4XShipPresetCatalog.DefaultGameplayScenePath;
        public static string ScenarioId { get; private set; } = Space4XModeSelectionState.FleetCrawlScenarioId;
        public static string ScenarioPath { get; private set; } = Space4XModeSelectionState.FleetCrawlScenarioPath;
        public static uint ScenarioSeed { get; private set; } = Space4XModeSelectionState.FleetCrawlSeed;
        public static ShipFlightProfile FlightProfile { get; private set; } = ShipFlightProfile.CreateDefault("ship.square.carrier");
        public static int MetaProgressionLevel { get; private set; } = 1;
        public static int ActiveMetaProgressionLevel { get; private set; } = 1;
        public static DateTime StartedUtc { get; private set; } = DateTime.MinValue;
        public static bool ScenarioSelectionPending { get; private set; }

        public static bool HasActiveSelection => StartedUtc != DateTime.MinValue;

        public static void Set(in Space4XShipPresetEntry preset, int difficulty, string scenePath)
        {
            ShipPresetId = preset.PresetId;
            ShipDisplayName = preset.DisplayName;
            Archetype = preset.Archetype;
            HullSegments = preset.HullSegments;
            StartingModules = preset.StartingModules;
            MetaPerks = preset.MetaPerks;
            Difficulty = Mathf.Max(1, difficulty);
            ScenePath = string.IsNullOrWhiteSpace(scenePath)
                ? Space4XShipPresetCatalog.DefaultGameplayScenePath
                : scenePath;
            FlightProfile = preset.FlightProfile;
            ActiveMetaProgressionLevel = Mathf.Max(1, MetaProgressionLevel);
            MetaProgressionLevel = ActiveMetaProgressionLevel + 1;
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
                Space4XScenarioAuthority.ResolvePlayableScenario(out scenarioId, out scenarioPath, out seed);
            }

            if (string.IsNullOrWhiteSpace(scenarioPath))
            {
                scenarioPath = Space4XScenarioAuthority.CanonicalScenarioPath;
            }

            if (seed == 0u)
            {
                seed = Space4XScenarioAuthority.CanonicalScenarioSeed;
            }

            return true;
        }

        public static void MarkScenarioSelectionApplied()
        {
            ScenarioSelectionPending = false;
        }

        private static void ResolveScenarioRouting(int difficulty, out string scenarioId, out string scenarioPath, out uint seed)
        {
            Space4XScenarioAuthority.ResolvePlayableScenario(out scenarioId, out scenarioPath, out seed);
        }
    }
}
