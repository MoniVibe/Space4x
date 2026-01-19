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
    public partial struct Space4XHeadlessScenarioRuntimeBridgeSystem : ISystem
    {
        private byte _initialized;

        public void OnCreate(ref SystemState state)
        {
            if (!RuntimeMode.IsHeadless || !Application.isBatchMode)
            {
                state.Enabled = false;
                return;
            }

            state.RequireForUpdate<ScenarioInfo>();
            state.RequireForUpdate<ScenarioRunnerTick>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_initialized != 0)
            {
                return;
            }

            if (SystemAPI.HasSingleton<Space4XScenarioRuntime>())
            {
                _initialized = 1;
                return;
            }

            if (!SystemAPI.TryGetSingleton(out ScenarioInfo info))
            {
                return;
            }

            var startTick = ResolveStartTick();
            var runTicks = (uint)math.max(1, info.RunTicks);
            var fixedDelta = ResolveFixedDelta();
            var durationSeconds = fixedDelta > 0f ? runTicks * fixedDelta : 0f;

            if (!SystemAPI.TryGetSingletonEntity<Space4XScenarioRuntime>(out var runtimeEntity))
            {
                runtimeEntity = state.EntityManager.CreateEntity(typeof(Space4XScenarioRuntime));
            }

            state.EntityManager.SetComponentData(runtimeEntity, new Space4XScenarioRuntime
            {
                StartTick = startTick,
                EndTick = startTick + runTicks,
                DurationSeconds = durationSeconds
            });

            _initialized = 1;
        }

        private static uint ResolveStartTick()
        {
            if (SystemAPI.TryGetSingleton<TickTimeState>(out var tickTime))
            {
                return tickTime.Tick;
            }

            if (SystemAPI.TryGetSingleton<ScenarioRunnerTick>(out var scenarioTick))
            {
                return scenarioTick.Tick;
            }

            if (SystemAPI.TryGetSingleton<TimeState>(out var timeState))
            {
                return timeState.Tick;
            }

            return 0u;
        }

        private static float ResolveFixedDelta()
        {
            if (SystemAPI.TryGetSingleton<TickTimeState>(out var tickTime))
            {
                return tickTime.FixedDeltaTime;
            }

            if (SystemAPI.TryGetSingleton<TimeState>(out var timeState))
            {
                return timeState.FixedDeltaTime;
            }

            return 0f;
        }
    }
}
