using PureDOTS.Runtime.Components;
using Space4x.Scenario;
using Unity.Entities;
using UnityEngine;
using UnityDebug = UnityEngine.Debug;

namespace Space4X.Headless
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XHeadlessScenarioQuitSystem : ISystem
    {
        private byte _quitRequested;

        public void OnCreate(ref SystemState state)
        {
            if (!Application.isBatchMode)
            {
                state.Enabled = false;
                return;
            }

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<Space4XScenarioRuntime>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_quitRequested != 0)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var scenarioRuntime = SystemAPI.GetSingleton<Space4XScenarioRuntime>();
            if (timeState.Tick < scenarioRuntime.EndTick)
            {
                return;
            }

            _quitRequested = 1;
            UnityDebug.Log($"[Space4XHeadlessScenarioQuitSystem] Scenario duration reached (tick {timeState.Tick} >= {scenarioRuntime.EndTick}); quitting.");
            Application.Quit(0);
        }
    }
}
