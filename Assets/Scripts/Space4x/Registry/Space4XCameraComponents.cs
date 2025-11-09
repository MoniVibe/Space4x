using PureDOTS.Runtime.Components;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Camera control input state written by the authoring component each frame.
    /// This is a singleton component that receives input from the Input System bridge.
    /// </summary>
    public struct Space4XCameraControlState : IComponentData
    {
        public float2 PanInput;
        public float ZoomInput;
        public float VerticalMoveInput;
        public float2 RotateInput;
        public bool ResetRequested;
        public bool ToggleVerticalModeRequested;
        public bool EnablePan;
        public bool EnableZoom;
        public bool EnableVerticalMove;
        public bool EnableRotation;
    }

    /// <summary>
    /// Persistent camera state for toggle modes and settings.
    /// Tracks whether vertical movement is in world space or camera-relative mode.
    /// </summary>
    public struct Space4XCameraPersistentState : IComponentData
    {
        /// <summary>
        /// If true, vertical movement (Q/E) moves relative to camera orientation.
        /// If false, vertical movement moves along world Y axis (XZ plane locked).
        /// </summary>
        public bool VerticalMoveCameraRelative;
    }

    /// <summary>
    /// Current camera transform state read and updated by the camera system.
    /// This is a singleton component that tracks the camera's world-space position and rotation.
    /// </summary>
    public struct Space4XCameraState : IComponentData
    {
        public float3 Position;
        public quaternion Rotation;
        public float ZoomDistance;
        public float3 FocusPoint;
        public float3 InitialPosition;
        public quaternion InitialRotation;
    }

    /// <summary>
    /// Camera configuration baked from Space4XCameraProfile ScriptableObject.
    /// Contains all tunable parameters for camera behavior.
    /// </summary>
    public struct Space4XCameraConfig : IComponentData
    {
        public float PanSpeed;
        public float ZoomSpeed;
        public float VerticalMoveSpeed;
        public float ZoomMinDistance;
        public float ZoomMaxDistance;
        public float RotationSpeed;
        public float PitchMin;
        public float PitchMax;
        public float Smoothing;
        public float3 PanBoundsMin;
        public float3 PanBoundsMax;
        public bool UsePanBounds;
    }

    /// <summary>
    /// Input system configuration for camera controls.
    /// Stores reference GUID to InputActionAsset and enable flags.
    /// Note: InputActionAsset reference stored as Entity-based singleton reference.
    /// </summary>
    public struct Space4XCameraInputConfig : IComponentData
    {
        public bool EnablePan;
        public bool EnableZoom;
        public bool EnableVerticalMove;
        public bool EnableRotation;
        public bool RequireRightMouseForRotation; // If true, rotation only works when right mouse is held
    }
}

