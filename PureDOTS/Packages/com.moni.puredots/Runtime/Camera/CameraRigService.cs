using System;
using PureDOTS.Runtime.Config;
using UnityEngine;
using UnityTime = UnityEngine.Time;

#nullable enable

namespace PureDOTS.Runtime.Camera
{
    /// <summary>
    /// CAMERA RIG CONTRACT - CameraRigService
    ///
    /// The central authority for camera rig state. This is the ONLY service allowed to mutate
    /// camera rig state across all game projects. Ensures no conflicts between different camera rigs.
    ///
    /// CONTRACT GUARANTEES:
    /// - Only one active camera rig state at a time (last writer wins)
    /// - Thread-safe static access (no locks needed for frame-time code)
    /// - Events for rig state changes (for telemetry/debugging)
    /// - ECS camera mode detection (game-specific feature flags)
    ///
    /// RESPONSIBILITIES:
    /// - Store the current authoritative CameraRigState
    /// - Notify subscribers when camera state changes
    /// - Provide ECS camera mode configuration
    /// - Ensure only CameraRigApplier mutates Camera.main
    ///
    /// USAGE PATTERN:
    /// 1. Game camera controller computes new CameraRigState
    /// 2. Call CameraRigService.Publish(state) to make it authoritative
    /// 3. CameraRigApplier automatically applies it in LateUpdate
    ///
    /// Uses frame-time (Time.deltaTime).
    /// Not part of deterministic simulation / rewind.
    /// Safe utility for game projects to build cameras on.
    /// </summary>
    public static class CameraRigService
    {
        private static RuntimeConfigVar? s_ecsCameraVar;
        private static CameraRigState s_currentState;
        private static bool s_hasState;
        private static int s_lastPublishFrame = -1;
        private static CameraRigType s_lastPublishRigType = CameraRigType.None;
        private static CameraRigDiagnostics s_diagnostics;

        /// <summary>
        /// Fired whenever a new camera rig state is published.
        /// Use for telemetry, debugging, or cross-system camera awareness.
        /// </summary>
        public static event Action<CameraRigState>? CameraStateChanged;

        /// <summary>True if any camera rig has published a state.</summary>
        public static bool HasState => s_hasState;

        /// <summary>The current authoritative camera rig state (valid only if HasState is true).</summary>
        public static CameraRigState Current => s_currentState;

        /// <summary>Current diagnostics snapshot.</summary>
        public static CameraRigDiagnostics Diagnostics => s_diagnostics;

        /// <summary>
        /// Safely gets the current camera rig state. Returns false if nothing has published yet.
        /// </summary>
        public static bool TryGetState(out CameraRigState state)
        {
            if (s_hasState)
            {
                state = s_currentState;
                return true;
            }

            state = default;
            return false;
        }

        /// <summary>
        /// Safely derives camera transform from the current state. Returns false if no state exists yet.
        /// </summary>
        public static bool TryGetTransform(out Vector3 position, out Quaternion rotation)
        {
            if (s_hasState)
            {
                CameraRigMath.DerivePose(in s_currentState, out position, out rotation);
                return true;
            }

            position = Vector3.zero;
            rotation = Quaternion.identity;
            return false;
        }

        /// <summary>True if ECS camera mode is enabled (game-specific configuration).</summary>
        public static bool IsEcsCameraEnabled
        {
            get
            {
                // Lazy initialization to avoid triggering RuntimeConfigRegistry during domain reload
                if (s_ecsCameraVar == null)
                {
                    RuntimeConfigRegistry.Initialize();
                    s_ecsCameraVar = CameraConfigVars.EcsModeEnabled;
                }
                return s_ecsCameraVar != null && s_ecsCameraVar.BoolValue;
            }
        }

        /// <summary>
        /// Publish a new camera rig state, making it the authoritative state for all cameras.
        /// This is the ONLY way to change camera rig state - ensures no conflicts between rigs.
        /// </summary>
        /// <param name="state">The new camera rig state to apply.</param>
        public static void Publish(CameraRigState state)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            int frame = UnityTime.frameCount;
            if (s_diagnostics.LastPublishFrame != frame)
            {
                s_diagnostics.PublishCountThisFrame = 0;
            }

            s_diagnostics.PublishCountThisFrame++;

            if (s_hasState &&
                s_lastPublishFrame == frame &&
                state.RigType != CameraRigType.None &&
                s_lastPublishRigType != CameraRigType.None &&
                state.RigType != s_lastPublishRigType)
            {
                Debug.LogError($"[CameraRigService] Multiple camera rig publishers in the same frame: {s_lastPublishRigType} -> {state.RigType}. Enforce a single active publisher and let only CameraRigApplier write transforms.");
            }
            s_lastPublishFrame = frame;
            s_lastPublishRigType = state.RigType;
#endif

            s_currentState = state;
            s_hasState = true;
            s_diagnostics.LastPublishFrame = UnityTime.frameCount;
            s_diagnostics.LastRigType = state.RigType;
            s_diagnostics.TotalPublishCount++;
            CameraStateChanged?.Invoke(state);
        }
    }
}
