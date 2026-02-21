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
        private const string ForceRenderEnvVar = "PUREDOTS_FORCE_RENDER";
        private const string LaptopValidationProbeMarker = "[Space4X Laptop Probe 2026-02-21T07:10Z]";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EmitLaptopValidationProbe()
        {
            Debug.Log($"{LaptopValidationProbeMarker} checkout reached editor domain reload.");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsurePresentationForSmokeScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !string.Equals(scene.name, SmokeSceneName, StringComparison.Ordinal))
            {
                return;
            }

            if (IsTruthy(global::System.Environment.GetEnvironmentVariable(ForceRenderEnvVar)))
            {
                return;
            }

            RuntimeMode.ForceRenderingEnabled(true, $"Editor smoke scene '{scene.name}'");
        }

        private static bool IsTruthy(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            value = value.Trim();
            return value.Equals("1", StringComparison.OrdinalIgnoreCase)
                || value.Equals("true", StringComparison.OrdinalIgnoreCase)
                || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
                || value.Equals("on", StringComparison.OrdinalIgnoreCase);
        }
    }
}
#endif
