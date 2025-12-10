using UnityEngine;

namespace PureDOTS.Camera
{
    /// <summary>
    /// Enum defining different camera rig types for different games/projects.
    /// Each rig type can have its own specialized controller.
    /// </summary>
    public enum CameraRigType
    {
        /// <summary>
        /// Default/undefined rig type
        /// </summary>
        None = 0,

        /// <summary>
        /// Space4X gameplay camera rig
        /// </summary>
        Space4X = 1,

        /// <summary>
        /// BW2 style camera rig (example implementation)
        /// </summary>
        BW2Style = 2
    }

    /// <summary>
    /// State structure containing all camera rig data.
    /// This is the central data structure that camera controllers publish
    /// and CameraRigApplier consumes to position the actual Camera.
    /// </summary>
    public struct CameraRigState
    {
        /// <summary>
        /// World position of the camera
        /// </summary>
        public Vector3 Position;

        /// <summary>
        /// World rotation of the camera
        /// </summary>
        public Quaternion Rotation;

        /// <summary>
        /// Pitch angle in degrees (rotation around X axis)
        /// </summary>
        public float Pitch;

        /// <summary>
        /// Yaw angle in degrees (rotation around Y axis)
        /// </summary>
        public float Yaw;

        /// <summary>
        /// Distance from camera to focus point (zoom level)
        /// </summary>
        public float Distance;

        /// <summary>
        /// Whether the camera is in free-flight mode (true) or locked to Y-plane (false)
        /// </summary>
        public bool PerspectiveMode;

        /// <summary>
        /// Field of view in degrees
        /// </summary>
        public float FieldOfView;

        /// <summary>
        /// Type of camera rig this state represents
        /// </summary>
        public CameraRigType RigType;

        /// <summary>
        /// Returns a default CameraRigState with sensible defaults
        /// </summary>
        public static CameraRigState Default => new CameraRigState
        {
            Position = new Vector3(0, 10, -10),
            Rotation = Quaternion.Euler(45, 0, 0),
            Pitch = 45f,
            Yaw = 0f,
            Distance = 14.14f, // sqrt(10^2 + 10^2)
            PerspectiveMode = false,
            FieldOfView = 60f,
            RigType = CameraRigType.None
        };

        /// <summary>
        /// Creates a CameraRigState from position, rotation, and rig type
        /// </summary>
        public static CameraRigState FromTransform(Transform transform, CameraRigType rigType = CameraRigType.None)
        {
            var euler = transform.rotation.eulerAngles;
            var forward = transform.forward;
            var distance = 10f; // Default distance

            // Try to estimate distance if we have a reasonable forward vector
            if (forward != Vector3.zero)
            {
                // This is a rough estimate - in practice, camera controllers should set this properly
                distance = 10f;
            }

            return new CameraRigState
            {
                Position = transform.position,
                Rotation = transform.rotation,
                Pitch = euler.x,
                Yaw = euler.y,
                Distance = distance,
                PerspectiveMode = false,
                FieldOfView = 60f,
                RigType = rigType
            };
        }

        public override string ToString()
        {
            return $"CameraRigState[Type={RigType}, Pos={Position}, Rot=({Pitch:F1}, {Yaw:F1}, 0), Dist={Distance:F1}, FOV={FieldOfView}, Mode={PerspectiveMode}]";
        }
    }
}
