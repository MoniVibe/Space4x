using PureDOTS.Runtime.Core;
using UnityEngine;

namespace Space4x.Scenario
{
    internal static class Space4XFleetcrawlAutoUiBootstrap
    {
        private const string BootstrapObjectName = "Space4XFleetcrawlUI";
        private static bool _logged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureUiBootstrap()
        {
            if (Application.isBatchMode || !RuntimeMode.IsRenderingEnabled)
            {
                return;
            }

            var existing = Object.FindFirstObjectByType<Space4XFleetcrawlUiOverlayMono>();
            if (existing != null)
            {
                EnsureComponent<Space4XFleetcrawlGateMarkersMono>(existing.gameObject);
                EnsureComponent<Space4XFleetcrawlEconomyAffordancesMono>(existing.gameObject);
                LogOnce("existing");
                return;
            }

            var go = new GameObject(BootstrapObjectName);
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Space4XFleetcrawlUiOverlayMono>();
            go.AddComponent<Space4XFleetcrawlManualPickInjectorMono>();
            go.AddComponent<Space4XFleetcrawlPlayerControlMono>();
            go.AddComponent<Space4XFleetcrawlCameraFollowMono>();
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
