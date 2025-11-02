using Godgame.Camera;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Godgame.Authoring
{
    /// <summary>
    /// Authoring component for camera controller setup.
    /// Bakes camera settings and initial transform into DOTS singleton components.
    /// </summary>
    [RequireComponent(typeof(UnityEngine.Camera))]
    public class CameraControllerAuthoring : MonoBehaviour
    {
        [Header("Camera Mode")]
        [Tooltip("Initial camera mode")]
        public CameraMode initialMode = CameraMode.RTSFreeFly;

        [Header("RTS/Free-fly Settings")]
        public float movementSpeed = 10f;
        public float rotationSensitivity = 2f;

        [Header("Zoom Settings")]
        public float zoomSpeed = 6f;
        public float zoomMin = 6f;
        public float zoomMax = 220f;

        [Header("Orbital Settings")]
        public float orbitalRotationSpeed = 1f;
        public float panSensitivity = 1f;
        
        [Header("Distance-Scaled Sensitivity")]
        [Tooltip("Sensitivity multiplier for close range (6-20m)")]
        public float sensitivityClose = 1.5f;
        [Tooltip("Sensitivity multiplier for mid range (20-100m)")]
        public float sensitivityMid = 1.0f;
        [Tooltip("Sensitivity multiplier for far range (100-220m)")]
        public float sensitivityFar = 0.6f;

        [Header("Pitch Limits (degrees)")]
        public float pitchMin = -30f;
        public float pitchMax = 85f;

        [Header("Terrain Collision")]
        public float terrainClearance = 2f;
        public float collisionBuffer = 0.4f;

        private class Baker : Baker<CameraControllerAuthoring>
        {
            public override void Bake(CameraControllerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                // Create camera singleton components
                // Note: These will be singletons, so we create them as separate entities
                // The system will handle singleton management, but we can create initial entities here

                // Create CameraSettings singleton
                var settingsEntity = CreateAdditionalEntity(TransformUsageFlags.None);
                AddComponent(settingsEntity, new CameraSettings
                {
                    MovementSpeed = authoring.movementSpeed,
                    RotationSensitivity = authoring.rotationSensitivity,
                    ZoomSpeed = authoring.zoomSpeed,
                    ZoomMin = authoring.zoomMin,
                    ZoomMax = authoring.zoomMax,
                    OrbitalFocusPoint = authoring.transform.position,
                    OrbitalRotationSpeed = authoring.orbitalRotationSpeed,
                    PanSensitivity = authoring.panSensitivity,
                    SensitivityClose = authoring.sensitivityClose,
                    SensitivityMid = authoring.sensitivityMid,
                    SensitivityFar = authoring.sensitivityFar,
                    PitchMin = authoring.pitchMin,
                    PitchMax = authoring.pitchMax,
                    TerrainClearance = authoring.terrainClearance,
                    CollisionBuffer = authoring.collisionBuffer
                });

                // Create CameraModeState singleton
                var modeEntity = CreateAdditionalEntity(TransformUsageFlags.None);
                AddComponent(modeEntity, new CameraModeState
                {
                    Mode = authoring.initialMode,
                    JustToggled = false
                });

                // Create CameraTransform singleton with initial transform
                var transformEntity = CreateAdditionalEntity(TransformUsageFlags.None);
                var worldPos = authoring.transform.position;
                var worldRot = authoring.transform.rotation;
                AddComponent(transformEntity, new CameraTransform
                {
                    Position = worldPos,
                    Rotation = worldRot,
                    DistanceFromPivot = math.length(worldPos),
                    PitchAngle = 45f // Default pitch
                });

                // Create CameraTerrainState singleton
                var terrainEntity = CreateAdditionalEntity(TransformUsageFlags.None);
                AddComponent(terrainEntity, new CameraTerrainState
                {
                    GrabPlanePosition = float3.zero,
                    GrabPlaneNormal = new float3(0f, 1f, 0f),
                    CurrentTerrainHeight = 0f,
                    CollisionClearance = authoring.terrainClearance
                });

                // Add CameraRenderBridge component to camera GameObject
                if (authoring.GetComponent<CameraRenderBridge>() == null)
                {
                    authoring.gameObject.AddComponent<CameraRenderBridge>();
                }
            }
        }
    }
}

