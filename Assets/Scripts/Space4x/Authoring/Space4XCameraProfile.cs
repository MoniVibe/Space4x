using UnityEngine;

namespace Space4X.Registry
{
    /// <summary>
    /// Configuration profile for Space4X camera controls.
    /// Follows PureDOTS HandCameraProfile pattern for designer-configurable parameters.
    /// </summary>
    [CreateAssetMenu(fileName = "Space4XCameraProfile", menuName = "Space4X/Camera Profile", order = 1)]
    public sealed class Space4XCameraProfile : ScriptableObject
    {
        [Header("Pan Settings")]
        [Tooltip("Speed of camera panning movement")]
        public float PanSpeed = 10f;

        [Tooltip("Minimum pan bounds (world space)")]
        public Vector3 PanBoundsMin = new Vector3(-100f, 0f, -100f);

        [Tooltip("Maximum pan bounds (world space)")]
        public Vector3 PanBoundsMax = new Vector3(100f, 100f, 100f);

        [Tooltip("Enable pan bounds clamping")]
        public bool UsePanBounds = false;

        [Header("Zoom Settings")]
        [Tooltip("Speed of camera zooming")]
        public float ZoomSpeed = 5f;

        [Tooltip("Minimum zoom distance")]
        public float ZoomMinDistance = 10f;

        [Tooltip("Maximum zoom distance")]
        public float ZoomMaxDistance = 500f;

        [Header("Vertical Movement Settings")]
        [Tooltip("Speed of camera vertical movement (up/down on Y axis)")]
        public float VerticalMoveSpeed = 10f;

        [Header("Rotation Settings")]
        [Tooltip("Speed of camera rotation")]
        public float RotationSpeed = 90f;

        [Tooltip("Minimum pitch angle in degrees")]
        public float PitchMin = -30f;

        [Tooltip("Maximum pitch angle in degrees")]
        public float PitchMax = 85f;

        [Header("Smoothing")]
        [Tooltip("Interpolation smoothing factor (0 = instant, 1 = fully smoothed)")]
        [Range(0f, 1f)]
        public float Smoothing = 0.1f;
    }
}

