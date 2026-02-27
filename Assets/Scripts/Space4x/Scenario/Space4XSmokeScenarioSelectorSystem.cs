#if UNITY_EDITOR
using PureDOTS.Runtime.Scenarios;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Space4x.Scenario
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateBefore(typeof(Space4XMiningScenarioSystem))]
    internal partial struct Space4XSmokeScenarioSelectorSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            if (!Application.isPlaying || Application.isBatchMode)
            {
                state.Enabled = false;
            }
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.HasSingleton<ScenarioInfo>())
            {
                return;
            }

            Space4XScenarioAuthority.ResolvePlayableScenario(out var scenarioId, out _, out var seed);
            Space4XScenarioAuthority.ApplyPlayableScenarioEnvironment();

            var scenarioEntity = state.EntityManager.CreateEntity(typeof(ScenarioInfo));
            state.EntityManager.SetComponentData(scenarioEntity, new ScenarioInfo
            {
                ScenarioId = new FixedString64Bytes(scenarioId),
                Seed = seed,
                RunTicks = 0
            });

            Debug.Log($"[Space4XSmokeScenarioSelector] Injected ScenarioInfo fallback pointing at canonical '{scenarioId}'.");
        }
    }
}
#endif
