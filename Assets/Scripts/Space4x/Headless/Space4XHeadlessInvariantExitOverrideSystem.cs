using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Headless
{
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    [UpdateBefore(typeof(Space4XHeadlessDiagnosticsSystem))]
    [UpdateBefore(typeof(PureDOTS.Systems.HeadlessExitSystem))]
    public partial struct Space4XHeadlessInvariantExitOverrideSystem : ISystem
    {
        private byte _enabled;

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
            if (_enabled == 0 || !Space4XHeadlessDiagnostics.HasInvariantFailures)
            {
                return;
            }

            if (SystemAPI.TryGetSingleton(out HeadlessExitRequest request))
            {
                if (request.ExitCode == 0)
                {
                    request.ExitCode = Space4XHeadlessDiagnostics.TestFailExitCode;
                    var requestEntity = SystemAPI.GetSingletonEntity<HeadlessExitRequest>();
                    state.EntityManager.SetComponentData(requestEntity, request);
                }
            }
        }
    }
}
