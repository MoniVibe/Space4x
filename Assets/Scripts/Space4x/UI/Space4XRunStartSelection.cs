using System;
using System.IO;
using Space4X.Modes;
using Space4X.Registry;
using Space4x.Scenario;
using UnityEngine;

namespace Space4X.UI
{
    public enum Space4XRunStartAnchor : byte
    {
        Auto = 0,
        Ship = 1,
        Station = 2,
        Colony = 3
    }

    /// <summary>
    /// Runtime handoff payload for launching a playable run from the frontend.
    /// </summary>
    public static class Space4XRunStartSelection
    {
        public const string SmokeScenarioId = "space4x_smoke";
        public const string SmokeScenarioPath = "Assets/Scenarios/space4x_smoke.json";
        public const uint SmokeScenarioSeed = 77u;
        public const string AutoStartScenarioPathEnv = "SPACE4X_AUTOSTART_SCENARIO_PATH";
        public const string AutoStartScenarioSeedEnv = "SPACE4X_AUTOSTART_SCENARIO_SEED";
        public const string AutoStartScenarioIdEnv = "SPACE4X_AUTOSTART_SCENARIO_ID";
        public const string AutoStartControlModeEnv = "SPACE4X_AUTOSTART_CONTROL_MODE";
        public const string AutoStartControlVariantEnv = "SPACE4X_AUTOSTART_CONTROL_VARIANT";
        public const string AutoStartFlagshipAnchorEnv = "SPACE4X_AUTOSTART_FLAGSHIP_ANCHOR";

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
        public static Space4XRunStartAnchor PreferredFlagshipAnchor { get; private set; } = Space4XRunStartAnchor.Auto;
        public static Space4XControlMode InitialControlMode { get; private set; } = Space4XControlMode.CursorOrient;
        public static bool InitialControlModeVariantEnabled { get; private set; }
        public static bool InitialControlModeOverridePending { get; private set; }

        private static bool _explicitAnchorOverride;
        private static Space4XRunStartAnchor _explicitAnchor;
        private static bool _explicitControlModeOverride;
        private static Space4XControlMode _explicitControlMode = Space4XControlMode.CursorOrient;
        private static bool _explicitControlModeVariantEnabled;
        private static bool _scenarioOverrideEnabled;
        private static string _scenarioOverridePath = string.Empty;
        private static string _scenarioOverrideId = string.Empty;
        private static uint _scenarioOverrideSeed;

        public static bool HasActiveSelection => StartedUtc != DateTime.MinValue;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void BootstrapOverrides()
        {
            ApplyStartupOverrides("ship.square.carrier");
        }

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
            ApplyStartupOverrides(preset.PresetId);
            ResolveScenarioRouting(Difficulty, out var scenarioId, out var scenarioPath, out var scenarioSeed);
            ScenarioId = scenarioId;
            ScenarioPath = scenarioPath;
            ScenarioSeed = scenarioSeed;
            ScenarioSelectionPending = true;
            StartedUtc = DateTime.UtcNow;
        }

        public static void ConfigureStartupOverrides(
            Space4XRunStartAnchor preferredAnchor,
            Space4XControlMode? initialMode = null,
            bool initialModeVariantEnabled = false)
        {
            _explicitAnchorOverride = true;
            _explicitAnchor = preferredAnchor;

            if (initialMode.HasValue)
            {
                _explicitControlModeOverride = true;
                _explicitControlMode = initialMode.Value;
                _explicitControlModeVariantEnabled = initialModeVariantEnabled;
                InitialControlModeOverridePending = true;
            }
        }

        public static void ClearStartupOverrides()
        {
            _explicitAnchorOverride = false;
            _explicitAnchor = Space4XRunStartAnchor.Auto;
            _explicitControlModeOverride = false;
            _explicitControlMode = Space4XControlMode.CursorOrient;
            _explicitControlModeVariantEnabled = false;
            PreferredFlagshipAnchor = Space4XRunStartAnchor.Auto;
            InitialControlModeOverridePending = false;
            InitialControlModeVariantEnabled = false;
        }

        public static void SetScenarioOverride(string scenarioPath, string scenarioId = null, uint scenarioSeed = 0u)
        {
            if (string.IsNullOrWhiteSpace(scenarioPath))
            {
                ClearScenarioOverride();
                return;
            }

            _scenarioOverrideEnabled = true;
            _scenarioOverridePath = scenarioPath.Trim();
            _scenarioOverrideId = string.IsNullOrWhiteSpace(scenarioId)
                ? Path.GetFileNameWithoutExtension(_scenarioOverridePath)
                : scenarioId.Trim();
            _scenarioOverrideSeed = scenarioSeed;
        }

        public static void ClearScenarioOverride()
        {
            _scenarioOverrideEnabled = false;
            _scenarioOverridePath = string.Empty;
            _scenarioOverrideId = string.Empty;
            _scenarioOverrideSeed = 0u;
        }

        public static bool TryConsumeInitialControlModeOverride(out Space4XControlMode mode, out bool variantEnabled)
        {
            mode = InitialControlMode;
            variantEnabled = InitialControlModeVariantEnabled;
            if (!InitialControlModeOverridePending)
            {
                return false;
            }

            InitialControlModeOverridePending = false;
            return true;
        }

        public static bool ApplyInitialControlModeOverride()
        {
            if (!TryConsumeInitialControlModeOverride(out var mode, out var variantEnabled))
            {
                return false;
            }

            Space4XControlModeState.SetMode(mode);
            Space4XControlModeState.SetVariantEnabled(mode, variantEnabled);
            return true;
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
            if (TryResolveScenarioOverride(out scenarioId, out scenarioPath, out seed))
            {
                return;
            }

            Space4XScenarioAuthority.ResolvePlayableScenario(out scenarioId, out scenarioPath, out seed);
        }

        private static bool TryResolveScenarioOverride(out string scenarioId, out string scenarioPath, out uint seed)
        {
            scenarioId = string.Empty;
            scenarioPath = string.Empty;
            seed = 0u;

            if (_scenarioOverrideEnabled && !string.IsNullOrWhiteSpace(_scenarioOverridePath))
            {
                scenarioPath = _scenarioOverridePath;
                scenarioId = string.IsNullOrWhiteSpace(_scenarioOverrideId)
                    ? Path.GetFileNameWithoutExtension(_scenarioOverridePath)
                    : _scenarioOverrideId;
                seed = _scenarioOverrideSeed;
            }
            else
            {
                if (!TryGetNonEmptyEnvironmentVariable(AutoStartScenarioPathEnv, out scenarioPath))
                {
                    return false;
                }

                if (!TryGetNonEmptyEnvironmentVariable(AutoStartScenarioIdEnv, out scenarioId))
                {
                    scenarioId = Path.GetFileNameWithoutExtension(scenarioPath);
                }

                if (!TryGetNonEmptyEnvironmentVariable(AutoStartScenarioSeedEnv, out var seedRaw) ||
                    !uint.TryParse(seedRaw, out seed))
                {
                    seed = 0u;
                }
            }

            if (string.IsNullOrWhiteSpace(scenarioPath))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(scenarioId))
            {
                scenarioId = Path.GetFileNameWithoutExtension(scenarioPath);
            }

            if (seed == 0u)
            {
                seed = Space4XScenarioAuthority.CanonicalScenarioSeed;
            }

            return true;
        }

        private static void ApplyStartupOverrides(string presetId)
        {
            var inferredAnchor = InferAnchorFromPresetId(presetId);
            PreferredFlagshipAnchor = inferredAnchor;
            InitialControlMode = Space4XControlMode.CursorOrient;
            InitialControlModeVariantEnabled = false;
            InitialControlModeOverridePending = false;

            if (_explicitAnchorOverride)
            {
                PreferredFlagshipAnchor = _explicitAnchor;
            }

            if (TryGetNonEmptyEnvironmentVariable(AutoStartFlagshipAnchorEnv, out var anchorToken) &&
                TryParseAnchor(anchorToken, out var envAnchor))
            {
                PreferredFlagshipAnchor = envAnchor;
            }

            if (_explicitControlModeOverride)
            {
                InitialControlMode = _explicitControlMode;
                InitialControlModeVariantEnabled = _explicitControlModeVariantEnabled;
                InitialControlModeOverridePending = true;
            }

            if (TryGetNonEmptyEnvironmentVariable(AutoStartControlModeEnv, out var modeToken) &&
                TryParseControlMode(modeToken, out var envMode))
            {
                InitialControlMode = envMode;
                InitialControlModeOverridePending = true;
            }

            if (TryGetNonEmptyEnvironmentVariable(AutoStartControlVariantEnv, out var variantToken) &&
                TryParseBooleanToken(variantToken, out var envVariantEnabled))
            {
                InitialControlModeVariantEnabled = envVariantEnabled;
                if (!InitialControlModeOverridePending)
                {
                    InitialControlModeOverridePending = true;
                }
            }
        }

        private static Space4XRunStartAnchor InferAnchorFromPresetId(string presetId)
        {
            if (string.IsNullOrWhiteSpace(presetId))
            {
                return Space4XRunStartAnchor.Ship;
            }

            if (presetId.Contains("station", StringComparison.OrdinalIgnoreCase))
            {
                return Space4XRunStartAnchor.Station;
            }

            if (presetId.Contains("colony", StringComparison.OrdinalIgnoreCase))
            {
                return Space4XRunStartAnchor.Colony;
            }

            return Space4XRunStartAnchor.Ship;
        }

        private static bool TryParseControlMode(string token, out Space4XControlMode mode)
        {
            mode = Space4XControlMode.CursorOrient;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            var normalized = token.Trim().ToLowerInvariant();
            switch (normalized)
            {
                case "1":
                case "m1":
                case "mode1":
                case "cursor":
                case "cursororient":
                    mode = Space4XControlMode.CursorOrient;
                    return true;
                case "2":
                case "m2":
                case "mode2":
                case "cruise":
                case "cruiselook":
                    mode = Space4XControlMode.CruiseLook;
                    return true;
                case "3":
                case "m3":
                case "mode3":
                case "rts":
                    mode = Space4XControlMode.Rts;
                    return true;
                case "4":
                case "m4":
                case "mode4":
                case "god":
                case "divine":
                case "divinehand":
                    mode = Space4XControlMode.DivineHand;
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryParseAnchor(string token, out Space4XRunStartAnchor anchor)
        {
            anchor = Space4XRunStartAnchor.Auto;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            var normalized = token.Trim().ToLowerInvariant();
            switch (normalized)
            {
                case "auto":
                    anchor = Space4XRunStartAnchor.Auto;
                    return true;
                case "ship":
                case "carrier":
                case "vessel":
                    anchor = Space4XRunStartAnchor.Ship;
                    return true;
                case "station":
                    anchor = Space4XRunStartAnchor.Station;
                    return true;
                case "colony":
                case "planet":
                    anchor = Space4XRunStartAnchor.Colony;
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryParseBooleanToken(string token, out bool value)
        {
            value = false;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            var normalized = token.Trim();
            if (normalized.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("on", StringComparison.OrdinalIgnoreCase))
            {
                value = true;
                return true;
            }

            if (normalized.Equals("0", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("false", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("no", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("off", StringComparison.OrdinalIgnoreCase))
            {
                value = false;
                return true;
            }

            return false;
        }

        private static bool TryGetNonEmptyEnvironmentVariable(string envName, out string value)
        {
            value = System.Environment.GetEnvironmentVariable(envName);
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            value = value.Trim();
            return !string.IsNullOrWhiteSpace(value);
        }
    }
}
