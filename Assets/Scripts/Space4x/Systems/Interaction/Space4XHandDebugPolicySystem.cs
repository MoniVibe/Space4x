using PureDOTS.Runtime.Hand;
using Unity.Entities;
using UnityEngine.InputSystem;

namespace Space4X.Systems.Interaction
{
    /// <summary>
    /// Dev-only toggle for world-grab debug policy.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4XHandDebugPolicySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<HandPickupPolicy>();
        }

        public void OnUpdate(ref SystemState state)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.f10Key.wasPressedThisFrame)
            {
                var policy = SystemAPI.GetSingleton<HandPickupPolicy>();
                policy.DebugWorldGrabAny = (byte)(policy.DebugWorldGrabAny == 0 ? 1 : 0);
                SystemAPI.SetSingleton(policy);
                UnityEngine.Debug.Log($"[Hand] DebugWorldGrabAny = {(policy.DebugWorldGrabAny != 0 ? "ON" : "OFF")}");
            }
#endif
        }
    }
}
