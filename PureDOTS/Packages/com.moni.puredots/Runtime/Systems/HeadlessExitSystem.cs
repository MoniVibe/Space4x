using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Systems.Telemetry;
using Unity.Entities;
using UnityEngine;
using UnityDebug = UnityEngine.Debug;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Defers <see cref="Application.Quit(int)"/> until after telemetry export has flushed.
    /// </summary>
    [UpdateInGroup(typeof(LateSimulationSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(TelemetryExportSystem))]
    public partial struct HeadlessExitSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            if (!RuntimeMode.IsHeadless || !Application.isBatchMode)
            {
                state.Enabled = false;
                return;
            }

            state.RequireForUpdate<HeadlessExitRequest>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<HeadlessExitRequest>(out var requestEntity))
            {
                return;
            }

            var request = state.EntityManager.GetComponentData<HeadlessExitRequest>(requestEntity);
            UnityDebug.Log($"[HeadlessExitSystem] Quit requested (code={request.ExitCode}, tick={request.RequestedTick}); quitting.");
            Quit(request.ExitCode);
        }

        private static void Quit(int exitCode)
        {
#if UNITY_EDITOR
            if (Application.isEditor && Application.isBatchMode)
            {
                UnityEditor.EditorApplication.Exit(exitCode);
                return;
            }
#endif
            Application.Quit(exitCode);
        }
    }
}
