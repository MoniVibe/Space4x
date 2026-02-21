using System;
using Space4X.Modes;
using Space4X.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Space4X.Diagnostics
{
    /// <summary>
    /// Logs scenario routing references on scene load so editor/runtime can confirm
    /// which scenario id/path are active without guessing.
    /// </summary>
    internal static class Space4XScenarioReferenceProbe
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void LogScenarioReference()
        {
            Space4XModeSelectionState.EnsureInitialized();
            Space4XModeSelectionState.GetCurrentScenario(out var modeScenarioId, out var modeScenarioPath, out var modeSeed);

            var envMode = Environment.GetEnvironmentVariable(Space4XModeSelectionState.ModeEnvVar);
            var envScenarioPath = Environment.GetEnvironmentVariable(Space4XModeSelectionState.ScenarioPathEnvVar);
            var scene = SceneManager.GetActiveScene();

            Debug.Log(
                $"[Space4XScenarioRef] scene='{scene.name}' mode={Space4XModeSelectionState.CurrentMode} " +
                $"mode_scenario_id='{modeScenarioId}' mode_scenario_path='{modeScenarioPath}' mode_seed={modeSeed} " +
                $"run_scenario_id='{Space4XRunStartSelection.ScenarioId}' run_scenario_path='{Space4XRunStartSelection.ScenarioPath}' " +
                $"run_seed={Space4XRunStartSelection.ScenarioSeed} run_pending={(Space4XRunStartSelection.ScenarioSelectionPending ? 1 : 0)} " +
                $"env_mode='{envMode ?? string.Empty}' env_scenario_path='{envScenarioPath ?? string.Empty}'.");
        }
    }
}
