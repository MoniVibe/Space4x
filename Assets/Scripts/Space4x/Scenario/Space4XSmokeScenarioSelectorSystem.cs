#if UNITY_EDITOR
using Space4X.Modes;
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
        private bool _injected;

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
            if (_injected)
            {
                state.Enabled = false;
                return;
            }

            if (SystemAPI.HasSingleton<ScenarioInfo>())
            {
                _injected = true;
                state.Enabled = false;
                return;
            }

            Space4XModeSelectionState.EnsureInitialized();
            Space4XModeSelectionState.GetCurrentScenario(out var scenarioId, out _, out var seed);

            var scenarioEntity = state.EntityManager.CreateEntity(typeof(ScenarioInfo));
            state.EntityManager.SetComponentData(scenarioEntity, new ScenarioInfo
            {
                ScenarioId = new FixedString64Bytes(scenarioId),
                Seed = seed,
                RunTicks = 240
            });

            Debug.Log($"[Space4XSmokeScenarioSelector] Injected ScenarioInfo fallback pointing at '{scenarioId}' mode={Space4XModeSelectionState.CurrentMode}.");
            _injected = true;
            state.Enabled = false;
        }
    }
}
#endif
