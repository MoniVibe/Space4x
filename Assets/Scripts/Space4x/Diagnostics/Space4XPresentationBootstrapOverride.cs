#if UNITY_EDITOR
using System;
using UnityEngine;
using PureDOTS.Runtime.Core;
using UnityEngine.SceneManagement;

namespace Space4X.Diagnostics
{
    internal static class Space4XPresentationBootstrapOverride
    {
        private const string SmokeSceneName = "TRI_Space4X_Smoke";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsurePresentationForSmokeScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !string.Equals(scene.name, SmokeSceneName, StringComparison.Ordinal))
            {
                return;
            }

            if (RuntimeMode.IsRenderingEnabled)
            {
                return;
            }

            RuntimeMode.ForceRenderingEnabled(true, $"Editor smoke scene '{scene.name}'");
        }
    }
}
#endif
