#if UNITY_EDITOR
using PureDOTS.Runtime.Scenarios;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;
using UEditor = UnityEditor.EditorApplication;
using UnityDebug = UnityEngine.Debug;

namespace Space4X.Diagnostics
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    internal partial struct Space4XSmokeSceneGuardSystem : ISystem
    {
        private const string CanonicalScenePath = "Assets/Scenes/TRI_Space4X_Smoke.unity";
        private const string DeprecatedScenePath = "Assets/Deprecated/DO_NOT_USE__TRI_Space4X_Smoke.unity";
        private const string DeprecatedLegacyScenePath = "Assets/TRI_Space4X_Smoke.unity";
        private bool _checked;

        public void OnCreate(ref SystemState state)
        {
            if (!Application.isPlaying)
            {
                state.Enabled = false;
            }
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_checked)
            {
                state.Enabled = false;
                return;
            }

            _checked = true;
            var activeScene = SceneManager.GetActiveScene();
            var scenePath = activeScene.path;
            var dataPath = Application.dataPath;

            if (string.Equals(scenePath, DeprecatedScenePath, System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenePath, DeprecatedLegacyScenePath, System.StringComparison.OrdinalIgnoreCase))
            {
                UnityDebug.LogError($"[Space4XSmokeSceneGuard] Deprecated smoke scene opened: '{scenePath}'. Use '{CanonicalScenePath}'. DataPath='{dataPath}'.");
                UEditor.isPlaying = false;
                state.Enabled = false;
                return;
            }

            UnityDebug.Log($"[Space4XSmokeSceneGuard] ActiveScene='{scenePath}', DataPath='{dataPath}'.");

            if (SystemAPI.TryGetSingleton<ScenarioInfo>(out var scenarioInfo))
            {
                UnityDebug.Log($"[Space4XSmokeSceneGuard] ScenarioInfo='{scenarioInfo.ScenarioId.ToString()}', Seed={scenarioInfo.Seed}, RunTicks={scenarioInfo.RunTicks}.");
            }

            state.Enabled = false;
        }
    }
}
#endif
