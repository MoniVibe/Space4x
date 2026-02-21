using System;
using System.IO;
using PureDOTS.Runtime.Scenarios;
using Space4X.UI;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Space4x.Scenario
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
#if UNITY_EDITOR
    [UpdateBefore(typeof(Space4XSmokeScenarioSelectorSystem))]
#endif
    [UpdateBefore(typeof(Space4XMiningScenarioSystem))]
    internal partial struct Space4XRunStartScenarioSelectorSystem : ISystem
    {
        private const string ScenarioPathEnv = "SPACE4X_SCENARIO_PATH";

        public void OnCreate(ref SystemState state)
        {
            if (!Application.isPlaying || Application.isBatchMode)
            {
                state.Enabled = false;
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!Space4XRunStartSelection.TryGetScenarioSelection(out var scenarioId, out var scenarioPath, out var seed))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(scenarioId))
            {
                scenarioId = Space4XRunStartSelection.SmokeScenarioId;
            }

            var safeSeed = seed == 0u ? Space4XRunStartSelection.SmokeScenarioSeed : seed;

            if (!SystemAPI.TryGetSingletonEntity<ScenarioInfo>(out var scenarioEntity))
            {
                scenarioEntity = state.EntityManager.CreateEntity(typeof(ScenarioInfo));
            }

            state.EntityManager.SetComponentData(scenarioEntity, new ScenarioInfo
            {
                ScenarioId = new FixedString64Bytes(scenarioId),
                Seed = safeSeed,
                RunTicks = 0
            });

            if (!string.IsNullOrWhiteSpace(scenarioPath))
            {
                System.Environment.SetEnvironmentVariable(ScenarioPathEnv, NormalizePathForScenarioEnv(scenarioPath));
            }

            Debug.Log($"[Space4XRunStartScenarioSelector] Injected ScenarioInfo id='{scenarioId}' seed={safeSeed} path='{scenarioPath}'.");
            Space4XRunStartSelection.MarkScenarioSelectionApplied();
        }

        private static string NormalizePathForScenarioEnv(string scenarioPath)
        {
            try
            {
                if (Path.IsPathRooted(scenarioPath))
                {
                    return Path.GetFullPath(scenarioPath);
                }

                var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                return Path.GetFullPath(Path.Combine(projectRoot, scenarioPath));
            }
            catch (Exception)
            {
                return scenarioPath;
            }
        }
    }
}
