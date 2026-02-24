#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SysEnv = global::System.Environment;
using UDebug = global::UnityEngine.Debug;

namespace Space4X.Editor.Diagnostics
{
    public static class Space4XAutoPlayProbeRunner
    {
        private const string SmokeScenePath = "Assets/Scenes/TRI_Space4X_Smoke.unity";
        private const string SessionActiveKey = "Space4XAutoPlayProbeRunner.Active";
        private const string SessionExitRequestedKey = "Space4XAutoPlayProbeRunner.ExitRequested";
        private const string SessionStartTimeKey = "Space4XAutoPlayProbeRunner.StartTime";
        private const string SessionDurationKey = "Space4XAutoPlayProbeRunner.Duration";
        private const float DefaultDurationSeconds = 180f;

        static Space4XAutoPlayProbeRunner()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        public static void Run()
        {
            if (Application.isBatchMode)
            {
                UDebug.LogError("[Space4XAutoPlayProbeRunner] This runner requires non-batch mode.");
                EditorApplication.Exit(2);
                return;
            }

            ConfigureEnvironment();

            var durationSec = ParseDurationSeconds(DefaultDurationSeconds);
            SessionState.SetBool(SessionActiveKey, true);
            SessionState.SetBool(SessionExitRequestedKey, false);
            SessionState.SetFloat(SessionStartTimeKey, (float)EditorApplication.timeSinceStartup);
            SessionState.SetFloat(SessionDurationKey, durationSec);

            EditorSceneManager.OpenScene(SmokeScenePath, OpenSceneMode.Single);
            UDebug.Log($"[Space4XAutoPlayProbeRunner] start scene='{SmokeScenePath}' duration_s={durationSec:0.0}");
            EditorApplication.isPlaying = true;
        }

        private static void OnEditorUpdate()
        {
            if (!SessionState.GetBool(SessionActiveKey, false))
            {
                return;
            }

            var startTime = SessionState.GetFloat(SessionStartTimeKey, 0f);
            var durationSec = SessionState.GetFloat(SessionDurationKey, DefaultDurationSeconds);
            var elapsed = (float)EditorApplication.timeSinceStartup - startTime;
            if (elapsed < durationSec)
            {
                return;
            }

            if (SessionState.GetBool(SessionExitRequestedKey, false))
            {
                return;
            }

            SessionState.SetBool(SessionExitRequestedKey, true);
            UDebug.Log($"[Space4XAutoPlayProbeRunner] duration reached elapsed_s={elapsed:0.0}, exiting.");

            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
                return;
            }

            ExitNow();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(SessionActiveKey, false))
            {
                return;
            }

            if (state == PlayModeStateChange.EnteredEditMode &&
                SessionState.GetBool(SessionExitRequestedKey, false))
            {
                ExitNow();
            }
        }

        private static void ExitNow()
        {
            SessionState.SetBool(SessionActiveKey, false);
            SessionState.SetBool(SessionExitRequestedKey, false);
            SessionState.SetFloat(SessionStartTimeKey, 0f);
            SessionState.SetFloat(SessionDurationKey, DefaultDurationSeconds);
            EditorApplication.Exit(0);
        }

        private static float ParseDurationSeconds(float fallback)
        {
            var args = SysEnv.GetCommandLineArgs();
            if (args == null)
            {
                return fallback;
            }

            for (var i = 0; i < args.Length; i++)
            {
                var token = args[i];
                if (string.IsNullOrWhiteSpace(token))
                {
                    continue;
                }

                if (token.StartsWith("--probeDurationSec=", StringComparison.OrdinalIgnoreCase))
                {
                    var value = token.Substring("--probeDurationSec=".Length);
                    if (float.TryParse(value, out var parsed) && parsed > 0f)
                    {
                        return parsed;
                    }
                }
            }

            return fallback;
        }

        private static void ConfigureEnvironment()
        {
            SysEnv.SetEnvironmentVariable("SPACE4X_MODE", "fleetcrawl");
            SysEnv.SetEnvironmentVariable("SPACE4X_SCENARIO_PATH", "Assets/Scenarios/space4x_fleetcrawl_core_micro.json");
            SysEnv.SetEnvironmentVariable("SPACE4X_AUTOSTART_RUN", "1");
            SysEnv.SetEnvironmentVariable("SPACE4X_AUTOSTART_PRESET", "ship.capsule.interceptor");
            SysEnv.SetEnvironmentVariable("SPACE4X_AUTOSTART_DIFFICULTY", "2");
            SysEnv.SetEnvironmentVariable("SPACE4X_FLAGSHIP_PIPELINE_PROBE", "1");
            SysEnv.SetEnvironmentVariable("SPACE4X_MOVEMENT_GLITCH_PROBE", "1");
            SysEnv.SetEnvironmentVariable("SPACE4X_ENTITY_VISIBILITY_PROBE", "1");
        }
    }
}
#endif
