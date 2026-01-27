using PureDOTS.Input;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Input
{
    /// <summary>
    /// Processes camera focus events (double-click LMB) and moves camera to target position.
    /// Uses frame-time for smooth camera movement (presentation code).
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(SelectionSystem))]
    public partial struct CameraBookmarkSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RtsInputSingletonTag>();
        }

        public void OnUpdate(ref SystemState state)
        {
            Entity rtsInputEntity = SystemAPI.GetSingletonEntity<RtsInputSingletonTag>();

            if (!state.EntityManager.HasBuffer<CameraFocusEvent>(rtsInputEntity))
            {
                return;
            }

            var focusBuffer = state.EntityManager.GetBuffer<CameraFocusEvent>(rtsInputEntity);
            if (focusBuffer.Length == 0)
            {
                return;
            }

            if (!state.EntityManager.HasBuffer<CameraRequestEvent>(rtsInputEntity))
            {
                state.EntityManager.AddBuffer<CameraRequestEvent>(rtsInputEntity);
            }
            var requests = state.EntityManager.GetBuffer<CameraRequestEvent>(rtsInputEntity);

            // Convert focus events into camera rig requests (camera is controlled by a single Mono publisher).
            for (int i = 0; i < focusBuffer.Length; i++)
            {
                var focusEvent = focusBuffer[i];
                requests.Add(new CameraRequestEvent
                {
                    Kind = CameraRequestKind.FocusWorld,
                    WorldPosition = focusEvent.WorldPosition,
                    PlayerId = focusEvent.PlayerId
                });
            }

            // Clear buffer after processing
            focusBuffer.Clear();
        }
    }
}
