using PureDOTS.Runtime.Hybrid;
using Unity.Entities;
using UnityEngine.InputSystem;

namespace PureDOTS.Systems.Hybrid
{
    /// <summary>
    /// Lightweight input listener that lets designers cycle between control modes at runtime.
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct HybridControlToggleSystem : ISystem
    {
        private bool _initialized;
        private bool _loggedRegistration;

        public void OnCreate(ref SystemState state)
        {
#if UNITY_EDITOR
            if (!_loggedRegistration)
            {
                UnityEngine.Debug.Log("[HybridControlToggleSystem] System registered in InitializationSystemGroup. Press F9 to cycle control modes.");
                _loggedRegistration = true;
            }
#endif
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (!_initialized)
            {
                // Default to dual mode the first time the system runs so existing scenes remain unchanged.
                HybridControlCoordinator.Mode = HybridControlCoordinator.InputMode.Dual;
                _initialized = true;
            }

            if (keyboard.f9Key.wasPressedThisFrame)
            {
                HybridControlCoordinator.CycleMode();
            }
        }
    }
}
