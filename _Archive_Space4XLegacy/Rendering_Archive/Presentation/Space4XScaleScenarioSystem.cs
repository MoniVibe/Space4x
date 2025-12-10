using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Presentation
{
    /// <summary>
    /// Configuration asset for scale scenario testing.
    /// </summary>
    [CreateAssetMenu(fileName = "Space4XScaleTestConfig", menuName = "Space4X/Scale Test Config")]
    public class Space4XScaleTestConfig : ScriptableObject
    {
        [Header("Scenario")]
        [Tooltip("Path to scenario JSON file")]
        public string ScenarioPath;

        [Header("LOD Override")]
        [Tooltip("Override LOD thresholds for scale tests")]
        public bool OverrideLOD = false;

        [Tooltip("LOD threshold overrides")]
        public PresentationLODConfig LODThresholds = PresentationLODConfig.Default;

        [Header("Render Density Override")]
        [Tooltip("Override render density for scale tests")]
        public bool OverrideDensity = false;

        [Range(0f, 1f)]
        [Tooltip("Render density override")]
        public float RenderDensity = 1f;
    }

    /// <summary>
    /// System that loads and applies scale scenario configurations.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4XScaleScenarioSystem : ISystem
    {
        private bool _scenarioLoaded;

        public void OnCreate(ref SystemState state)
        {
            // This system would load scenarios via PureDOTS ScenarioRunner
            // For now, it's a placeholder that applies config overrides
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_scenarioLoaded)
            {
                return;
            }

            // Check for scale test config (would be set via editor menu or define)
            // For now, just ensure LOD and density configs exist

            if (!SystemAPI.HasSingleton<PresentationLODConfig>())
            {
                var lodEntity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(lodEntity, PresentationLODConfig.Default);
            }

            if (!SystemAPI.HasSingleton<RenderDensityConfig>())
            {
                var densityEntity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(densityEntity, RenderDensityConfig.Default);
            }

            _scenarioLoaded = true;
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Editor menu item to load scale scenarios.
    /// </summary>
    public static class Space4XScaleScenarioMenu
    {
        [UnityEditor.MenuItem("Tools/Space4X/Load Scale Scenario")]
        public static void LoadScaleScenario()
        {
            string path = UnityEditor.EditorUtility.OpenFilePanel("Select Scale Scenario", "Assets/Scenarios", "json");
            if (!string.IsNullOrEmpty(path))
            {
                Debug.Log($"[Space4XScaleScenarioMenu] Selected scenario: {path}");
                // Would load scenario via PureDOTS ScenarioRunner
                // For now, just log the path
            }
        }
    }
#endif
}

