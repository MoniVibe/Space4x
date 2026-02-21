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
                LogOnce("existing");
                return;
            }

            var go = new GameObject(BootstrapObjectName);
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Space4XFleetcrawlUiOverlayMono>();
            go.AddComponent<Space4XFleetcrawlCommandMailboxMono>();
            go.AddComponent<Space4XFleetcrawlStarterLoadoutOverrideMono>();
            go.AddComponent<Space4XFleetcrawlMetaProgressionMono>();
            go.AddComponent<Space4XFleetcrawlPlayerControlMono>();
            go.AddComponent<Space4XFleetcrawlCameraFollowMono>();
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
    }
}
