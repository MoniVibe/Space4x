using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Scenarios;
using Space4x.Scenario;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Headless
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4XScenarioRuntimeBridgeSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            if (!RuntimeMode.IsHeadless || !Application.isBatchMode)
            {
                state.Enabled = false;
                return;
            }

            state.RequireForUpdate<ScenarioInfo>();
            state.RequireForUpdate<ScenarioEntityCountElement>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.HasSingleton<Space4XScenarioRuntime>())
            {
                state.Enabled = false;
                return;
            }

            var scenarioInfo = SystemAPI.GetSingleton<ScenarioInfo>();
            var runTicks = scenarioInfo.RunTicks < 0 ? 0u : (uint)scenarioInfo.RunTicks;

            var durationSeconds = 0f;
            if (SystemAPI.TryGetSingleton<TimeState>(out var timeState))
            {
                durationSeconds = math.max(0f, timeState.FixedDeltaTime * runTicks);
            }

            var runtimeEntity = state.EntityManager.CreateEntity(typeof(Space4XScenarioRuntime));
            state.EntityManager.SetComponentData(runtimeEntity, new Space4XScenarioRuntime
            {
                StartTick = 0u,
                EndTick = runTicks,
                DurationSeconds = durationSeconds
            });

            state.Enabled = false;
        }
    }
}
