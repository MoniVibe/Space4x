using PureDOTS.Runtime.Core;
using UnityEngine;

namespace Space4x.Scenario
{
    internal static class Space4XFleetcrawlAutoUiBootstrap
    {
        private const string BootstrapObjectName = "Space4XFleetcrawlUI";
        private const string DebugUiEnv = "SPACE4X_FLEETCRAWL_DEBUG_UI";
        private const string DebugDriveEnv = "SPACE4X_FLEETCRAWL_DEBUG_DRIVE";
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
                EnsureComponents(existing.gameObject, enableUi, enableDrive);
                LogOnce($"existing ui={(enableUi ? 1 : 0)} drive={(enableDrive ? 1 : 0)}");
                return;
            }

            var go = new GameObject(BootstrapObjectName);
            Object.DontDestroyOnLoad(go);
            EnsureComponents(go, enableUi, enableDrive);
            LogOnce($"spawned ui={(enableUi ? 1 : 0)} drive={(enableDrive ? 1 : 0)}");
        }

        private static void EnsureComponents(GameObject go, bool enableUi, bool enableDrive)
        {
            if (enableUi)
            {
                EnsureComponent<Space4XFleetcrawlUiOverlayMono>(go);
                EnsureComponent<Space4XFleetcrawlManualPickInjectorMono>(go);
                EnsureComponent<Space4XFleetcrawlGateMarkersMono>(go);
            }

            if (enableDrive)
            {
                EnsureComponent<Space4XFleetcrawlPlayerControlMono>(go);
                EnsureComponent<Space4XFleetcrawlCameraFollowMono>(go);
            }
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
            var value = System.Environment.GetEnvironmentVariable(envName);
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized = value.Trim();
            return normalized.Equals("1", System.StringComparison.OrdinalIgnoreCase) ||
                   normalized.Equals("true", System.StringComparison.OrdinalIgnoreCase) ||
                   normalized.Equals("yes", System.StringComparison.OrdinalIgnoreCase) ||
                   normalized.Equals("on", System.StringComparison.OrdinalIgnoreCase);
        }

        private static T EnsureComponent<T>(GameObject go) where T : Component
        {
            var existing = go.GetComponent<T>();
            return existing != null ? existing : go.AddComponent<T>();
        }
    }
}
