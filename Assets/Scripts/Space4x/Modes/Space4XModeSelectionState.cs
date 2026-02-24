using System;
using System.IO;
using UnityEngine;
using SystemEnv = System.Environment;
using UnityDebug = UnityEngine.Debug;

namespace Space4X.Modes
{
    public enum Space4XModeKind : byte
    {
        Classic = 0,
        FleetCrawl = 1
    }

    /// <summary>
    /// Runtime mode router for Space4X shell -> scenario selection.
    /// Keeps FleetCrawl as an explicit mode instead of implicit bootstrap behavior.
    /// </summary>
    public static class Space4XModeSelectionState
    {
        public const string ModeEnvVar = "SPACE4X_MODE";
        public const string ScenarioPathEnvVar = "SPACE4X_SCENARIO_PATH";

        public const string ClassicScenarioId = "space4x_smoke";
        public const string ClassicScenarioPath = "Assets/Scenarios/space4x_smoke.json";
        public const uint ClassicSeed = 77u;

        // FleetCrawl mode defaults to the canonical room-director micro scenario.
        // The smoke shell scene still hosts the menu, but scenario content stays canonical.
        public const string FleetCrawlScenarioId = "space4x_fleetcrawl_core_micro";
        public const string FleetCrawlScenarioPath = "Assets/Scenarios/space4x_fleetcrawl_core_micro.json";
        public const uint FleetCrawlSeed = 19021u;

        private static bool s_initialized;
        private static Space4XModeKind s_currentMode = Space4XModeKind.FleetCrawl;

        public static event Action<Space4XModeKind> ModeChanged;

        public static Space4XModeKind CurrentMode
        {
            get
            {
                EnsureInitialized();
                return s_currentMode;
            }
        }

        public static bool IsFleetCrawlModeActive => CurrentMode == Space4XModeKind.FleetCrawl;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void BootstrapBeforeSceneLoad()
        {
            EnsureInitialized();
        }

        public static void EnsureInitialized()
        {
            if (s_initialized)
            {
                return;
            }

            s_initialized = true;
            s_currentMode = ResolveModeFromEnvironment();
            SystemEnv.SetEnvironmentVariable(ModeEnvVar, ToEnvToken(s_currentMode));
            ApplyScenarioEnvironment(s_currentMode);
        }

        public static bool SetMode(Space4XModeKind mode, bool applyScenarioEnvironment = true)
        {
            EnsureInitialized();
            if (s_currentMode == mode)
            {
                if (applyScenarioEnvironment)
                {
                    ApplyScenarioEnvironment(mode);
                }

                return false;
            }

            s_currentMode = mode;
            SystemEnv.SetEnvironmentVariable(ModeEnvVar, ToEnvToken(mode));
            if (applyScenarioEnvironment)
            {
                ApplyScenarioEnvironment(mode);
            }

            ModeChanged?.Invoke(mode);
            UnityDebug.Log($"[Space4XModeSelection] mode={mode} scenario={GetScenarioId(mode)}");
            return true;
        }

        public static string GetScenarioId(Space4XModeKind mode)
        {
            return mode == Space4XModeKind.FleetCrawl ? FleetCrawlScenarioId : ClassicScenarioId;
        }

        public static string GetScenarioPath(Space4XModeKind mode)
        {
            return mode == Space4XModeKind.FleetCrawl ? FleetCrawlScenarioPath : ClassicScenarioPath;
        }

        public static uint GetScenarioSeed(Space4XModeKind mode)
        {
            return mode == Space4XModeKind.FleetCrawl ? FleetCrawlSeed : ClassicSeed;
        }

        public static void GetCurrentScenario(out string scenarioId, out string scenarioPath, out uint seed)
        {
            var mode = CurrentMode;
            scenarioId = GetScenarioId(mode);
            scenarioPath = GetScenarioPath(mode);
            seed = GetScenarioSeed(mode);
        }

        public static void ApplyScenarioEnvironment(Space4XModeKind mode)
        {
            if (TryResolvePinnedScenarioPath(out var pinnedScenarioPath))
            {
                SystemEnv.SetEnvironmentVariable(ScenarioPathEnvVar, pinnedScenarioPath);
                return;
            }

            var scenarioPath = GetScenarioPath(mode);
            SystemEnv.SetEnvironmentVariable(ScenarioPathEnvVar, scenarioPath);
        }

        public static Space4XModeKind ResolveModeFromEnvironment()
        {
            var modeRaw = SystemEnv.GetEnvironmentVariable(ModeEnvVar);
            if (!string.IsNullOrWhiteSpace(modeRaw))
            {
                if (modeRaw.Trim().Equals("fleetcrawl", StringComparison.OrdinalIgnoreCase))
                {
                    return Space4XModeKind.FleetCrawl;
                }

                if (modeRaw.Trim().Equals("classic", StringComparison.OrdinalIgnoreCase))
                {
                    return Space4XModeKind.Classic;
                }
            }

            if (TryResolvePinnedScenarioPath(out var pinnedScenarioPath))
            {
                return pinnedScenarioPath.IndexOf("fleetcrawl", StringComparison.OrdinalIgnoreCase) >= 0
                    ? Space4XModeKind.FleetCrawl
                    : Space4XModeKind.Classic;
            }

            var scenarioPath = SystemEnv.GetEnvironmentVariable(ScenarioPathEnvVar);
            if (!string.IsNullOrWhiteSpace(scenarioPath) &&
                scenarioPath.IndexOf("fleetcrawl", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return Space4XModeKind.FleetCrawl;
            }

            return Space4XModeKind.FleetCrawl;
        }

        public static bool IsScenarioPathRoutable(string scenarioPath)
        {
            if (string.IsNullOrWhiteSpace(scenarioPath))
            {
                return false;
            }

            if (File.Exists(scenarioPath))
            {
                return true;
            }

            var absolute = Path.Combine(Directory.GetCurrentDirectory(), scenarioPath);
            return File.Exists(absolute);
        }

        private static string ToEnvToken(Space4XModeKind mode)
        {
            return mode == Space4XModeKind.FleetCrawl ? "fleetcrawl" : "classic";
        }

        private static bool TryResolvePinnedScenarioPath(out string scenarioPath)
        {
            scenarioPath = null;
            if (!Application.isBatchMode)
            {
                return false;
            }

            if (TryGetCommandLineArgumentValue("--scenario", out var scenarioArg))
            {
                var resolvedFromArgs = NormalizeScenarioPath(scenarioArg);
                if (!string.IsNullOrWhiteSpace(resolvedFromArgs))
                {
                    scenarioPath = resolvedFromArgs;
                    return true;
                }
            }

            var envPath = SystemEnv.GetEnvironmentVariable(ScenarioPathEnvVar);
            var resolvedFromEnv = NormalizeScenarioPath(envPath);
            if (!string.IsNullOrWhiteSpace(resolvedFromEnv))
            {
                scenarioPath = resolvedFromEnv;
                return true;
            }

            return false;
        }

        private static bool TryGetCommandLineArgumentValue(string key, out string value)
        {
            var args = SystemEnv.GetCommandLineArgs();
            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if (string.Equals(arg, key, StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < args.Length)
                    {
                        value = args[i + 1];
                        return true;
                    }

                    break;
                }

                var prefix = key + "=";
                if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    value = arg.Substring(prefix.Length).Trim('"');
                    return true;
                }
            }

            value = null;
            return false;
        }

        private static string NormalizeScenarioPath(string rawPath)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                return null;
            }

            var trimmed = rawPath.Trim();
            if (!trimmed.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            try
            {
                if (Path.IsPathRooted(trimmed))
                {
                    return Path.GetFullPath(trimmed);
                }

                var absolute = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), trimmed));
                return File.Exists(absolute) ? absolute : null;
            }
            catch
            {
                return null;
            }
        }
    }
}
