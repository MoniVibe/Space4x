using PureDOTS.Runtime.Scenarios;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Space4x.Scenario
{
    public struct Space4XSmokeShowcaseTuningAppliedTag : IComponentData
    {
    }

    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(Space4XMiningScenarioSystem))]
    public partial struct Space4XSmokeShowcaseTuningSystem : ISystem
    {
        private const float RetrogradeBoostValue = 1.5f;
        private FixedString64Bytes _smokeScenarioId;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScenarioInfo>();
            _smokeScenarioId = new FixedString64Bytes("space4x_smoke");
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<ScenarioInfo>(out var scenarioEntity))
            {
                return;
            }

            if (state.EntityManager.HasComponent<Space4XSmokeShowcaseTuningAppliedTag>(scenarioEntity))
            {
                state.Enabled = false;
                return;
            }

            var scenarioInfo = state.EntityManager.GetComponentData<ScenarioInfo>(scenarioEntity);
            if (!scenarioInfo.ScenarioId.Equals(_smokeScenarioId))
            {
                state.EntityManager.AddComponent<Space4XSmokeShowcaseTuningAppliedTag>(scenarioEntity);
                state.Enabled = false;
                return;
            }

            var config = VesselMotionProfileConfig.Default;
            if (SystemAPI.TryGetSingleton<VesselMotionProfileConfig>(out var existing))
            {
                config = existing;
            }

            config.RetrogradeBoost = RetrogradeBoostValue;

            if (!SystemAPI.TryGetSingletonEntity<VesselMotionProfileConfig>(out var configEntity))
            {
                configEntity = state.EntityManager.CreateEntity(typeof(VesselMotionProfileConfig));
            }

            state.EntityManager.SetComponentData(configEntity, config);
            state.EntityManager.AddComponent<Space4XSmokeShowcaseTuningAppliedTag>(scenarioEntity);
            state.Enabled = false;
        }
    }
}
