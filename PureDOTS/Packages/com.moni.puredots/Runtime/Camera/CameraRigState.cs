using UnityEngine;

namespace PureDOTS.Runtime.Camera
{
    /// <summary>
    /// CAMERA RIG CONTRACT - PRESENTATION CODE
    ///
    /// Uses frame-time (Time.deltaTime).
    /// Not part of deterministic simulation / rewind.
    /// Safe utility for game projects to build cameras on.
    ///
    /// Identifies which gameplay rig currently owns the camera transform.
    /// </summary>
    public enum CameraRigType : byte
    {
        None = 0,
        Godgame = 1,
        BW2 = 2,
        Space4X = 3
    }

    public enum CameraRigMode : byte
    {
        Orbit = 0,
        FreeFly = 1
    }

    /// <summary>
    /// CAMERA RIG CONTRACT - CameraRigState
    ///
    /// Represents the canonical state of a camera rig.
    /// This is the single source of truth; the Unity Camera transform is derived from it.
    ///
    /// CONTRACT GUARANTEES:
    /// - Focus: World-space pivot/target the camera orbits/looks at (authoritative)
    /// - Yaw/Pitch/Roll: Orbit angles in degrees (authoritative)
    /// - Distance: Orbit radius in world units (authoritative; 0 = look-in-place / free-fly)
    /// - Mode: Orbit vs free-fly (authoritative; informs input/UX, not the math)
    /// - PerspectiveMode: true=perspective, false=orthographic (game-specific interpretation)
    /// - FieldOfView: Camera field of view in degrees (0 = use camera default)
    /// - RigType: Which game rig owns this state (for debugging/conflict resolution)
    ///
    /// DERIVED EVERY FRAME:
    /// Rotation = q(pitch around Right(yaw)) * q(yaw around WorldUp) then optional roll around Forward
    /// Position = Focus + Rotation * (0,0,-Distance)
    ///
    /// Uses frame-time (Time.deltaTime).
    /// Not part of deterministic simulation / rewind.
    /// Safe utility for game projects to build cameras on.
    /// </summary>
    public struct CameraRigState
    {
        /// <summary>World-space pivot/target the camera orbits/looks at.</summary>
        public Vector3 Focus;

        /// <summary>Pitch angle in degrees (for orbit cameras).</summary>
        public float Pitch;

        /// <summary>Yaw angle in degrees (for orbit cameras).</summary>
        public float Yaw;

        /// <summary>Roll angle in degrees (optional; default 0).</summary>
        public float Roll;

        /// <summary>Distance from pivot/target (for zoom cameras).</summary>
        public float Distance;

        /// <summary>Orbit vs free-fly (UX/input hint).</summary>
        public CameraRigMode Mode;

        /// <summary>true=perspective mode, false=orthographic (game-specific interpretation).</summary>
        public bool PerspectiveMode;

        /// <summary>Camera field of view in degrees (0 = use camera default).</summary>
        public float FieldOfView;

        /// <summary>Which game rig owns this state (for debugging/conflict resolution).</summary>
        public CameraRigType RigType;
    }

    public static class CameraRigMath
    {
        public static void DerivePose(in CameraRigState state, out Vector3 position, out Quaternion rotation)
        {
            // Yaw: rotate around world up.
            // Pitch: rotate around the yaw-rotated right axis.
            // Roll: optional, rotate around the forward axis after yaw+pitch.
            var yawRot = Quaternion.AngleAxis(state.Yaw, Vector3.up);
            var pitchAxis = yawRot * Vector3.right;
            var pitchRot = Quaternion.AngleAxis(state.Pitch, pitchAxis);
            rotation = pitchRot * yawRot;

            if (Mathf.Abs(state.Roll) > 1e-4f)
            {
                var forward = rotation * Vector3.forward;
                rotation = Quaternion.AngleAxis(state.Roll, forward) * rotation;
            }

            float distance = state.Distance;
            if (distance < 0f) distance = 0f;
            position = state.Focus - (rotation * Vector3.forward * distance);
        }
    }

    public interface ICameraStateProvider
    {
        CameraRigState CurrentCameraState { get; }
    }
}
