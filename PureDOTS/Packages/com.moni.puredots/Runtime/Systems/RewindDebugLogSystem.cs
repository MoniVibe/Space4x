#if UNITY_EDITOR
using Unity.Burst;
using Unity.Entities;
using PureDOTS.Runtime.Components;
using UnityEngine;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Editor-only debug system that logs RewindControlState phase changes.
    /// Helps verify the rewind state machine is working correctly.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct RewindDebugLogSystem : ISystem
    {
        private RewindPhase _lastPhase;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RewindControlState>();
            _lastPhase = RewindPhase.Inactive;
        }

        [BurstDiscard] // Contains Debug.Log calls
        public void OnUpdate(ref SystemState state)
        {
            var control = SystemAPI.GetSingleton<RewindControlState>();

            // Only log when phase changes or when not inactive
            if (control.Phase != _lastPhase || control.Phase != RewindPhase.Inactive)
            {
                if (control.Phase != _lastPhase)
                {
                    UnityEngine.Debug.Log(
                        $"[RewindDebug] Phase changed: {_lastPhase} -> {control.Phase} " +
                        $"(PresentTick={control.PresentTickAtStart}, PreviewTick={control.PreviewTick}, Speed={control.ScrubSpeed:F2}x)");
                    _lastPhase = control.Phase;
                }
                else if (control.Phase != RewindPhase.Inactive)
                {
                    // Log periodically when in preview phases (every 30 frames to avoid spam)
                    if (UnityEngine.Time.frameCount % 30 == 0)
                    {
                        UnityEngine.Debug.Log(
                            $"[RewindDebug] Phase={control.Phase} PresentTick={control.PresentTickAtStart} " +
                            $"PreviewTick={control.PreviewTick} Speed={control.ScrubSpeed:F2}x");
                    }
                }
            }
        }
    }
}
#endif

