using PureDOTS.Runtime.Hybrid;
using UnityEngine;
using UnityEngine.Events;

namespace PureDOTS.Authoring.Hybrid
{
    /// <summary>
    /// Simple MonoBehaviour bridge so designers can hook UI buttons to hybrid control switching.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HybridControlToggleAuthoring : MonoBehaviour
    {
        [SerializeField]
        private HybridControlCoordinator.InputMode _defaultMode = HybridControlCoordinator.InputMode.Dual;

        [SerializeField]
        private UnityEvent<HybridControlCoordinator.InputMode> _onModeChanged;

        private void OnEnable()
        {
            HybridControlCoordinator.ModeChanged += HandleModeChanged;
            HybridControlCoordinator.Mode = _defaultMode;
        }

        private void OnDisable()
        {
            HybridControlCoordinator.ModeChanged -= HandleModeChanged;
        }

        /// <summary>
        /// Cycles to the next mode (Dual → Space4X → Godgame → Dual).
        /// Suitable for UI button binding.
        /// </summary>
        public void CycleMode()
        {
            HybridControlCoordinator.CycleMode();
        }

        public void SetDualMode()
        {
            HybridControlCoordinator.Mode = HybridControlCoordinator.InputMode.Dual;
        }

        public void SetGodgameOnlyMode()
        {
            HybridControlCoordinator.Mode = HybridControlCoordinator.InputMode.GodgameOnly;
        }

        public void SetSpace4XOnlyMode()
        {
            HybridControlCoordinator.Mode = HybridControlCoordinator.InputMode.Space4XOnly;
        }

        private void HandleModeChanged(HybridControlCoordinator.InputMode mode)
        {
            _onModeChanged?.Invoke(mode);
        }
    }
}


