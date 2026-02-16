using PureDOTS.Runtime.Core;
using UnityEngine;

namespace Space4X.Presentation.Overlay
{
    /// <summary>
    /// Auto-installs presentation debug overlay components for editor/dev runs.
    /// </summary>
    internal static class Space4XAutoPresentationHUDDebugBootstrap
    {
        private const string BootstrapLog = "[Space4XAutoPresentationHUDDebugBootstrap] active=1";
        private static bool s_bootstrapped;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureActive()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (s_bootstrapped || Application.isBatchMode || !RuntimeMode.IsRenderingEnabled)
            {
                return;
            }

            var existingHud = Object.FindFirstObjectByType<Space4XPresentationHUDMono>();
            var root = existingHud != null ? existingHud.gameObject : new GameObject("Space4X Presentation Debug Overlay");

            EnsureComponent<Space4XPresentationHUDMono>(root);
            EnsureComponent<Space4XEntityPicker>(root);
            EnsureComponent<Space4XCarrierInspector>(root);
            EnsureComponent<Space4XShotDirector>(root);

            Object.DontDestroyOnLoad(root);
            s_bootstrapped = true;
            Debug.Log(BootstrapLog);
#endif
        }

        private static void EnsureComponent<T>(GameObject root)
            where T : Component
        {
            if (root.GetComponent<T>() == null)
            {
                root.AddComponent<T>();
            }
        }
    }
}
