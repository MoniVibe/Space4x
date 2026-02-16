using PureDOTS.Runtime.Core;
using UnityEngine;
using UnityDebug = UnityEngine.Debug;

namespace Space4X.TimeDebug
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    internal static class Space4XAutoRewindDebugBootstrap
    {
        private const string BootstrapObjectName = "Space4XAutoRewindDebugBootstrap";
        private static bool _logged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRewindDebug()
        {
            if (Application.isBatchMode || !RuntimeMode.IsRenderingEnabled)
            {
                return;
            }

            var existing = Object.FindFirstObjectByType<Space4XRewindDebug>();
            if (existing != null)
            {
                LogOnce(mode: "existing");
                return;
            }

            var bootstrapGo = new GameObject(BootstrapObjectName);
            Object.DontDestroyOnLoad(bootstrapGo);
            bootstrapGo.AddComponent<Space4XRewindDebug>();
            LogOnce(mode: "spawned");
        }

        private static void LogOnce(string mode)
        {
            if (_logged)
            {
                return;
            }

            _logged = true;
            UnityDebug.Log($"[Space4XAutoRewindDebugBootstrap] rewind_debug_ready=1 mode={mode}");
        }
    }
#endif
}
