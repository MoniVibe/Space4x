using Unity.Entities;
using Unity.Mathematics;

namespace Godgame.Camera
{
    /// <summary>
    /// Camera control mode - RTS/Free-fly or BW2-style Orbital.
    /// </summary>
    public enum CameraMode : byte
    {
        RTSFreeFly = 0,
        Orbital = 1
    }

    /// <summary>
    /// Camera settings with BW2 reference values.
    /// Burst-compatible component for camera configuration.
    /// </summary>
    public struct CameraSettings : IComponentData
    {
        // RTS/Free-fly mode settings
        public float MovementSpeed; // Movement speed for WASD
        public float RotationSensitivity; // Mouse look sensitivity

        // Zoom settings (both modes)
        public float ZoomSpeed; // Zoom speed per tick (6 units per scroll tick)
        public float ZoomMin; // Minimum zoom distance (6m)
        public float ZoomMax; // Maximum zoom distance (220m)

        // Orbital mode settings
        public float3 OrbitalFocusPoint; // Locked pivot point for MMB orbit
        public float OrbitalRotationSpeed; // Base rotation speed
        public float PanSensitivity; // Pan sensitivity for LMB drag (1.0 scale)

        // Distance-scaled sensitivity multipliers
        public float SensitivityClose; // Close range (6-20m): 1.5x
        public float SensitivityMid; // Mid range (20-100m): 1.0x
        public float SensitivityFar; // Far range (100-220m): 0.6x

        // Pitch limits (degrees)
        public float PitchMin; // Minimum pitch (-30°)
        public float PitchMax; // Maximum pitch (+85°)

        // Terrain collision
        public float TerrainClearance; // Minimum clearance above terrain (2m)
        public float CollisionBuffer; // Safety margin for collision (0.4m)

        /// <summary>
        /// Creates default settings with BW2 reference values.
        /// </summary>
        public static CameraSettings Default => new CameraSettings
        {
            MovementSpeed = 10f,
            RotationSensitivity = 2f,
            ZoomSpeed = 6f,
            ZoomMin = 6f,
            ZoomMax = 220f,
            OrbitalFocusPoint = float3.zero,
            OrbitalRotationSpeed = 1f,
            PanSensitivity = 1f,
            SensitivityClose = 1.5f,
            SensitivityMid = 1.0f,
            SensitivityFar = 0.6f,
            PitchMin = -30f,
            PitchMax = 85f,
            TerrainClearance = 2f,
            CollisionBuffer = 0.4f
        };
    }

    /// <summary>
    /// Camera transform state - position, rotation, and orbital parameters.
    /// Burst-compatible component storing camera state.
    /// </summary>
    public struct CameraTransform : IComponentData
    {
        public float3 Position; // Camera world position
        public quaternion Rotation; // Camera rotation
        public float DistanceFromPivot; // Distance from pivot (for orbital mode)
        public float PitchAngle; // Current pitch angle in degrees (for orbital mode limits)
    }

    /// <summary>
    /// Camera terrain interaction state for BW2-style controls.
    /// Stores grab plane and terrain height information.
    /// </summary>
    public struct CameraTerrainState : IComponentData
    {
        public float3 GrabPlanePosition; // Grab plane position for LMB pan
        public float3 GrabPlaneNormal; // Grab plane normal
        public float CurrentTerrainHeight; // Current terrain height at camera position
        public float CollisionClearance; // Current collision clearance
    }

    /// <summary>
    /// Current camera mode (singleton component).
    /// </summary>
    public struct CameraModeState : IComponentData
    {
        public CameraMode Mode;
        public bool JustToggled; // True if mode was toggled this frame (for debouncing)
    }
}

