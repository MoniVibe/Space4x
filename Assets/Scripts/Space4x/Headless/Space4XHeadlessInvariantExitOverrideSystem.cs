using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Time;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Headless
{
    [UpdateInGroup(typeof(PureDOTS.Systems.LateSimulationSystemGroup))]
    [UpdateBefore(typeof(Space4XHeadlessDiagnosticsSystem))]
    [UpdateBefore(typeof(PureDOTS.Systems.HeadlessExitSystem))]
    public partial struct Space4XHeadlessInvariantExitOverrideSystem : ISystem
    {
        private byte _enabled;
        private byte _exitRequested;

        public void OnCreate(ref SystemState state)
        {
            RuntimeMode.RefreshFromEnvironment();
            if (!RuntimeMode.IsHeadless || !Application.isBatchMode)
            {
                state.Enabled = false;
                return;
            }

            Space4XHeadlessDiagnostics.InitializeFromArgs();
            if (!Space4XHeadlessDiagnostics.Enabled)
            {
                state.Enabled = false;
                return;
            }

            _enabled = 1;
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_enabled == 0 || _exitRequested != 0 || !Space4XHeadlessDiagnostics.HasInvariantFailures)
            {
                return;
            }

            var tick = ResolveTick(ref state);
            HeadlessExitUtility.Request(state.EntityManager, tick, Space4XHeadlessDiagnostics.TestFailExitCode);
            _exitRequested = 1;
        }

        private uint ResolveTick(ref SystemState state)
        {
            uint tick = 0;
            if (SystemAPI.TryGetSingleton<TickTimeState>(out var tickTimeState))
            {
                tick = tickTimeState.Tick;
            }

            if (SystemAPI.TryGetSingleton<TimeState>(out var timeState) && timeState.Tick > tick)
            {
                tick = timeState.Tick;
            }

            return tick;
        }
    }
}
