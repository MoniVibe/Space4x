using PureDOTS.Runtime.Core;
using Space4X.Modes.FleetCrawl;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Space4x.Scenario
{
    internal static class Space4XFleetcrawlAutoUiBootstrap
    {
        private const string BootstrapObjectName = "Space4XFleetcrawlUI";
        private static bool _sceneHooked;
        private static bool _logged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RegisterSceneHook()
        {
            if (_sceneHooked)
            {
                EnsureUiBootstrap();
                return;
            }

            _sceneHooked = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureUiBootstrap();
        }

        private static void OnSceneLoaded(Scene _, LoadSceneMode __)
        {
            EnsureUiBootstrap();
        }

        private static void EnsureUiBootstrap()
        {
            if (Application.isBatchMode || !RuntimeMode.IsRenderingEnabled)
            {
                return;
            }

            if (!Space4XFleetCrawlModePackage.IsActive)
            {
                var staleOverlay = Object.FindFirstObjectByType<Space4XFleetcrawlUiOverlayMono>();
                if (staleOverlay != null)
                {
                    Object.Destroy(staleOverlay.gameObject);
                }

                return;
            }

            var existingOverlay = Object.FindFirstObjectByType<Space4XFleetcrawlUiOverlayMono>();
            if (existingOverlay != null)
            {
                var host = existingOverlay.gameObject;
                EnsureComponent<Space4XFleetcrawlUiOverlayMono>(host);
                EnsureComponent<Space4XFleetcrawlManualPickInjectorMono>(host);
                EnsureComponent<Space4XFleetcrawlPlayerControlMono>(host);
                EnsureComponent<Space4XFleetcrawlCameraFollowMono>(host);
                EnsureComponent<Space4XFleetcrawlGateMarkersMono>(host);
                EnsureComponent<Space4XFleetcrawlEconomyAffordancesMono>(host);
                LogOnce("existing");
                return;
            }

            var go = new GameObject(BootstrapObjectName);
            EnsureComponent<Space4XFleetcrawlUiOverlayMono>(go);
            EnsureComponent<Space4XFleetcrawlManualPickInjectorMono>(go);
            EnsureComponent<Space4XFleetcrawlPlayerControlMono>(go);
            EnsureComponent<Space4XFleetcrawlCameraFollowMono>(go);
            EnsureComponent<Space4XFleetcrawlGateMarkersMono>(go);
            EnsureComponent<Space4XFleetcrawlEconomyAffordancesMono>(go);
            LogOnce("spawned");
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

        private static T EnsureComponent<T>(GameObject go) where T : Component
        {
            var existing = go.GetComponent<T>();
            return existing != null ? existing : go.AddComponent<T>();
        }
    }
}
