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
        private const string ScenarioIdString = "space4x_smoke";
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

            var scenarioEntity = state.EntityManager.CreateEntity(typeof(ScenarioInfo));
            state.EntityManager.SetComponentData(scenarioEntity, new ScenarioInfo
            {
                ScenarioId = new FixedString64Bytes(ScenarioIdString),
                Seed = 77,
                RunTicks = 240
            });

            if (!SystemAPI.HasSingleton<Space4XLegacyMiningDisabledTag>())
            {
                state.EntityManager.CreateEntity(typeof(Space4XLegacyMiningDisabledTag));
            }

            if (!SystemAPI.HasSingleton<Space4XLegacyPatrolDisabledTag>())
            {
                state.EntityManager.CreateEntity(typeof(Space4XLegacyPatrolDisabledTag));
            }

            Debug.Log($"[Space4XSmokeScenarioSelector] Injected ScenarioInfo fallback pointing at '{ScenarioIdString}'.");
            _injected = true;
            state.Enabled = false;
        }
    }
}
#endif
