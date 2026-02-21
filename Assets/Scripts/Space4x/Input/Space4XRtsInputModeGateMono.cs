using PureDOTS.Input;
using Space4X.UI;
using UnityEngine;

namespace Space4X.Input
{
    /// <summary>
    /// Enables RTS input bridge only while RTS control mode is active.
    /// Keeps mode 4 (Divine Hand) isolated from RTS selection/order input paths.
    /// </summary>
    [DefaultExecutionOrder(-890)]
    [DisallowMultipleComponent]
    public sealed class Space4XRtsInputModeGateMono : MonoBehaviour
    {
        [SerializeField] private bool keepEnabledOutsideRts;

        private RtsInputBridge _bridge;
        private Space4XRtsClassicCommandMono _classicCommands;

        private void OnEnable()
        {
            ResolveReferences();
            Space4XControlModeState.ModeChanged += OnModeChanged;
            ApplyMode(Space4XControlModeState.CurrentMode);
        }

        private void OnDisable()
        {
            Space4XControlModeState.ModeChanged -= OnModeChanged;
        }

        private void OnModeChanged(Space4XControlMode mode)
        {
            ApplyMode(mode);
        }

        private void ResolveReferences()
        {
            if (_bridge == null)
            {
                _bridge = GetComponent<RtsInputBridge>();
            }

            if (_classicCommands == null)
            {
                _classicCommands = GetComponent<Space4XRtsClassicCommandMono>();
            }
        }

        private void ApplyMode(Space4XControlMode mode)
        {
            ResolveReferences();

            bool rtsEnabled = mode == Space4XControlMode.Rts;
            bool bridgeEnabled = keepEnabledOutsideRts || rtsEnabled;

            if (_bridge != null)
            {
                _bridge.enabled = bridgeEnabled;
            }

            if (_classicCommands != null)
            {
                _classicCommands.enabled = rtsEnabled;
            }
        }
    }
}
