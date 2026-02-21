using PureDOTS.Runtime.Core;
using Space4x.Scenario;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Space4X.Modes
{
    /// <summary>
    /// Lightweight shell overlay for selecting Space4X mode in smoke scene.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Space4XModeSelectorOverlayMono : MonoBehaviour
    {
        private const string SmokeSceneName = "TRI_Space4X_Smoke";
        private const string SmokeScenePath = "Assets/Scenes/TRI_Space4X_Smoke.unity";
        private const string HeadlessBootstrapSceneName = "HeadlessBootstrap";
        private const string ObjectName = "Space4XModeSelectorOverlay";
        private const string ShowOverlayEnv = "SPACE4X_SHOW_MODE_SELECTOR";
        private static bool s_sceneHooked;

        private readonly Rect _panelRect = new Rect(16f, 16f, 300f, 128f);
        private GUIStyle _panelStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _labelStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!s_sceneHooked)
            {
                s_sceneHooked = true;
                SceneManager.sceneLoaded += OnSceneLoaded;
            }

            EnsureOverlayForScene(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode _)
        {
            EnsureOverlayForScene(scene);
        }

        private static void EnsureOverlayForScene(Scene scene)
        {
            if (Application.isBatchMode || !RuntimeMode.IsRenderingEnabled)
            {
                return;
            }

            if (!IsOverlayEnabled())
            {
                return;
            }

            if (!scene.IsValid())
            {
                return;
            }

            if (string.Equals(scene.name, HeadlessBootstrapSceneName, System.StringComparison.Ordinal))
            {
#if UNITY_EDITOR
                try
                {
                    var loadOp = EditorSceneManager.LoadSceneAsyncInPlayMode(
                        SmokeScenePath,
                        new LoadSceneParameters(LoadSceneMode.Single));
                    if (loadOp != null)
                    {
                        return;
                    }
                }
                catch
                {
                }
#endif
                SceneManager.LoadScene(SmokeSceneName, LoadSceneMode.Single);
                return;
            }

            if (!string.Equals(scene.name, SmokeSceneName, System.StringComparison.Ordinal))
            {
                return;
            }

            if (Object.FindFirstObjectByType<Space4XModeSelectorOverlayMono>() != null)
            {
                return;
            }

            var go = new GameObject(ObjectName);
            go.AddComponent<Space4XModeSelectorOverlayMono>();
        }

        private static bool IsOverlayEnabled()
        {
            var value = global::System.Environment.GetEnvironmentVariable(ShowOverlayEnv);
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var trimmed = value.Trim();
            return string.Equals(trimmed, "1", global::System.StringComparison.OrdinalIgnoreCase)
                   || string.Equals(trimmed, "true", global::System.StringComparison.OrdinalIgnoreCase)
                   || string.Equals(trimmed, "yes", global::System.StringComparison.OrdinalIgnoreCase)
                   || string.Equals(trimmed, "on", global::System.StringComparison.OrdinalIgnoreCase);
        }

        private void OnEnable()
        {
            Space4XModeSelectionState.EnsureInitialized();
        }

        private void OnGUI()
        {
            EnsureStyles();

            GUILayout.BeginArea(_panelRect, GUIContent.none, _panelStyle);
            GUILayout.Label("Space4X Mode", _headerStyle);

            var mode = Space4XModeSelectionState.CurrentMode;
            Space4XModeSelectionState.GetCurrentScenario(out var scenarioId, out var scenarioPath, out _);
            GUILayout.Label($"Active: {mode}", _labelStyle);
            GUILayout.Label($"Scenario: {scenarioId}", _labelStyle);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Classic", GUILayout.Height(26f)))
            {
                SwitchMode(Space4XModeKind.Classic);
            }

            if (GUILayout.Button("FleetCrawl", GUILayout.Height(26f)))
            {
                SwitchMode(Space4XModeKind.FleetCrawl);
            }
            GUILayout.EndHorizontal();

            if (!Space4XModeSelectionState.IsScenarioPathRoutable(scenarioPath))
            {
                GUILayout.Label($"Missing scenario path: {scenarioPath}", _labelStyle);
            }
            else
            {
                GUILayout.Label("Mode change reloads current scene.", _labelStyle);
            }

            GUILayout.EndArea();
        }

        private void SwitchMode(Space4XModeKind mode)
        {
            Space4XModeSelectionState.SetMode(mode, applyScenarioEnvironment: true);
            RequestScenarioReloadAcrossWorlds();

            var active = SceneManager.GetActiveScene();
            if (active.IsValid())
            {
                SceneManager.LoadScene(active.name, LoadSceneMode.Single);
            }
        }

        private static void RequestScenarioReloadAcrossWorlds()
        {
            for (var i = 0; i < World.All.Count; i++)
            {
                var world = World.All[i];
                if (world == null || !world.IsCreated)
                {
                    continue;
                }

                var miningScenarioSystem = world.GetExistingSystemManaged<Space4XMiningScenarioSystem>();
                if (miningScenarioSystem == null)
                {
                    continue;
                }

                miningScenarioSystem.RequestReloadForModeSwitch();
            }
        }

        private void EnsureStyles()
        {
            if (_panelStyle != null)
            {
                return;
            }

            _panelStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(10, 10, 8, 8)
            };
            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold
            };
            _labelStyle = new GUIStyle(GUI.skin.label);
        }
    }
}
