using Unity.Entities;
using UnityEngine;

namespace Space4X.Presentation
{
    /// <summary>
    /// MonoBehaviour component that loads a scenario JSON file on scene start.
    /// Used for Demo_02 and other scenario-driven demos.
    /// </summary>
    public class Space4XScenarioLoader : MonoBehaviour
    {
        [Header("Scenario Configuration")]
        [Tooltip("Scenario JSON file path (relative to Assets/)")]
        public string ScenarioPath = "Scenarios/demo_02_combat.json";

        [Tooltip("Load scenario on Start")]
        public bool LoadOnStart = true;

        [Tooltip("Scenario name (for display)")]
        public string ScenarioName = "Demo_02 Combat";

        private void Start()
        {
            if (LoadOnStart && !string.IsNullOrEmpty(ScenarioPath))
            {
                LoadScenario();
            }
        }

        /// <summary>
        /// Load the scenario via PureDOTS ScenarioRunner.
        /// </summary>
        public void LoadScenario()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null)
            {
                Debug.LogError("[Space4XScenarioLoader] DefaultGameObjectInjectionWorld not found. Cannot load scenario.");
                return;
            }

            // Try to get ScenarioRunner system
            // Note: ScenarioRunner is a PureDOTS system, may need to use reflection or direct access
            var scenarioRunnerType = System.Type.GetType("PureDOTS.Runtime.Devtools.ScenarioRunner, PureDOTS.Runtime");
            if (scenarioRunnerType == null)
            {
                Debug.LogWarning("[Space4XScenarioLoader] ScenarioRunner type not found. Scenario loading may not work. Ensure PureDOTS package is referenced.");
                // Fallback: Just log the scenario path for now
                Debug.Log($"[Space4XScenarioLoader] Would load scenario: {ScenarioPath}");
                return;
            }

            // Get or create ScenarioRunner system
            var scenarioRunner = world.GetOrCreateSystemManaged(scenarioRunnerType);
            if (scenarioRunner == null)
            {
                Debug.LogError("[Space4XScenarioLoader] Failed to get ScenarioRunner system.");
                return;
            }

            // Call LoadScenario method via reflection
            var loadMethod = scenarioRunnerType.GetMethod("LoadScenario", new[] { typeof(string) });
            if (loadMethod != null)
            {
                string fullPath = System.IO.Path.Combine(Application.dataPath, "..", ScenarioPath);
                loadMethod.Invoke(scenarioRunner, new object[] { fullPath });
                Debug.Log($"[Space4XScenarioLoader] Loaded scenario: {ScenarioName} from {ScenarioPath}");
            }
            else
            {
                Debug.LogWarning("[Space4XScenarioLoader] LoadScenario method not found on ScenarioRunner. Scenario loading may not work.");
            }
        }

        /// <summary>
        /// Get the current scenario name for display in debug panel.
        /// </summary>
        public string GetScenarioName()
        {
            return ScenarioName;
        }
    }
}

