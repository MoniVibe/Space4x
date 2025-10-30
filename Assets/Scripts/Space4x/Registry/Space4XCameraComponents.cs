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
        public float2 RotateInput;
        public bool ResetRequested;
        public bool EnablePan;
        public bool EnableZoom;
        public bool EnableRotation;
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
        public bool EnableRotation;
    }
}

