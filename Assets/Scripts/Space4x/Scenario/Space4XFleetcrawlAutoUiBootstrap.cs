using PureDOTS.Runtime.Core;
using UnityEngine;

namespace Space4x.Scenario
{
    internal static class Space4XFleetcrawlAutoUiBootstrap
    {
        private const string BootstrapObjectName = "Space4XFleetcrawlUI";
        private const string DebugUiEnv = "SPACE4X_FLEETCRAWL_DEBUG_UI";
        private const string DebugDriveEnv = "SPACE4X_FLEETCRAWL_DEBUG_DRIVE";
        private const string UiOverlayToggleEnv = "SPACE4X_FLEETCRAWL_UI_OVERLAY";
        private const string UiManualPickToggleEnv = "SPACE4X_FLEETCRAWL_UI_MANUAL_PICK";
        private const string UiGateMarkersToggleEnv = "SPACE4X_FLEETCRAWL_UI_GATE_MARKERS";
        private const string DriveControlToggleEnv = "SPACE4X_FLEETCRAWL_DRIVE_PLAYER_CONTROL";
        private const string DriveCameraToggleEnv = "SPACE4X_FLEETCRAWL_DRIVE_CAMERA_FOLLOW";
        private static bool _logged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureUiBootstrap()
        {
            if (Application.isBatchMode || !RuntimeMode.IsRenderingEnabled)
            {
                return;
            }

            var enableUi = IsTruthyEnvironmentVariable(DebugUiEnv);
            var enableDrive = IsTruthyEnvironmentVariable(DebugDriveEnv);
            if (!enableUi && !enableDrive)
            {
                LogOnce("disabled");
                return;
            }

            var existing = Object.FindFirstObjectByType<Space4XFleetcrawlUiOverlayMono>();
            if (existing != null)
            {
                var summary = EnsureComponents(existing.gameObject, enableUi, enableDrive);
                LogOnce($"existing {summary}");
                return;
            }

            var go = new GameObject(BootstrapObjectName);
            Object.DontDestroyOnLoad(go);
            var spawnSummary = EnsureComponents(go, enableUi, enableDrive);
            LogOnce($"spawned {spawnSummary}");
        }

        private static string EnsureComponents(GameObject go, bool enableUi, bool enableDrive)
        {
            var enableUiOverlay = enableUi && ReadToggleOrDefault(UiOverlayToggleEnv, true);
            var enableManualPick = enableUi && ReadToggleOrDefault(UiManualPickToggleEnv, true);
            var enableGateMarkers = enableUi && ReadToggleOrDefault(UiGateMarkersToggleEnv, true);
            var enablePlayerControl = enableDrive && ReadToggleOrDefault(DriveControlToggleEnv, true);
            var enableCameraFollow = enableDrive && ReadToggleOrDefault(DriveCameraToggleEnv, true);

            ApplyComponentState<Space4XFleetcrawlUiOverlayMono>(go, enableUiOverlay);
            ApplyComponentState<Space4XFleetcrawlManualPickInjectorMono>(go, enableManualPick);
            ApplyComponentState<Space4XFleetcrawlGateMarkersMono>(go, enableGateMarkers);
            ApplyComponentState<Space4XFleetcrawlPlayerControlMono>(go, enablePlayerControl);
            ApplyComponentState<Space4XFleetcrawlCameraFollowMono>(go, enableCameraFollow);

            return $"ui={(enableUi ? 1 : 0)} drive={(enableDrive ? 1 : 0)} overlay={(enableUiOverlay ? 1 : 0)} manual_pick={(enableManualPick ? 1 : 0)} gates={(enableGateMarkers ? 1 : 0)} player={(enablePlayerControl ? 1 : 0)} cam={(enableCameraFollow ? 1 : 0)}";
        }

        private static void LogOnce(string mode)
        {
            if (_logged)
            {
                return;
            }

            _logged = true;
            Debug.Log($"[Space4XFleetcrawlAutoUiBootstrap] active=1 mode={mode}");
        }

        private static bool IsTruthyEnvironmentVariable(string envName)
        {
            return TryReadEnvironmentToggle(envName, out var enabled) && enabled;
        }

        private static bool ReadToggleOrDefault(string envName, bool defaultValue)
        {
            return TryReadEnvironmentToggle(envName, out var enabled) ? enabled : defaultValue;
        }

        private static bool TryReadEnvironmentToggle(string envName, out bool enabled)
        {
            enabled = false;
            var value = System.Environment.GetEnvironmentVariable(envName);
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized = value.Trim();
            if (normalized.Equals("1", System.StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("true", System.StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("yes", System.StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("on", System.StringComparison.OrdinalIgnoreCase))
            {
                enabled = true;
                return true;
            }

            if (normalized.Equals("0", System.StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("false", System.StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("no", System.StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("off", System.StringComparison.OrdinalIgnoreCase))
            {
                enabled = false;
                return true;
            }

            return false;
        }

        private static void ApplyComponentState<T>(GameObject go, bool enabled) where T : Component
        {
            var existing = go.GetComponent<T>();
            if (enabled)
            {
                if (existing == null)
                {
                    go.AddComponent<T>();
                }

                return;
            }

            if (existing != null)
            {
                Object.Destroy(existing);
            }
        }
    }
}
