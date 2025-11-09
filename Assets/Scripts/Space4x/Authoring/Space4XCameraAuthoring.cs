using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Registry
{
    /// <summary>
    /// Pure DOTS authoring component that bakes camera state and configuration.
    /// Initializes camera position/rotation from GameObject transform.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public sealed class Space4XCameraAuthoring : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField]
        private Space4XCameraProfile profile;

        internal Space4XCameraState BuildInitialState()
        {
            var position = math.float3(transform.position);
            var rotation = new quaternion(
                transform.rotation.x,
                transform.rotation.y,
                transform.rotation.z,
                transform.rotation.w
            );
            var forward = math.float3(transform.forward);

            return new Space4XCameraState
            {
                Position = position,
                Rotation = rotation,
                ZoomDistance = math.distance(position, position + forward * 10f),
                FocusPoint = position + forward * 10f,
                InitialPosition = position,
                InitialRotation = rotation
            };
        }

        internal Space4XCameraConfig BuildConfigData()
        {
            if (profile != null)
            {
                return new Space4XCameraConfig
                {
                    PanSpeed = profile.PanSpeed,
                    ZoomSpeed = profile.ZoomSpeed,
                    VerticalMoveSpeed = profile.VerticalMoveSpeed,
                    ZoomMinDistance = profile.ZoomMinDistance,
                    ZoomMaxDistance = profile.ZoomMaxDistance,
                    RotationSpeed = profile.RotationSpeed,
                    PitchMin = math.radians(profile.PitchMin),
                    PitchMax = math.radians(profile.PitchMax),
                    Smoothing = profile.Smoothing,
                    PanBoundsMin = math.float3(profile.PanBoundsMin),
                    PanBoundsMax = math.float3(profile.PanBoundsMax),
                    UsePanBounds = profile.UsePanBounds
                };
            }

            return new Space4XCameraConfig
            {
                PanSpeed = 10f,
                ZoomSpeed = 5f,
                VerticalMoveSpeed = 10f,
                ZoomMinDistance = 10f,
                ZoomMaxDistance = 500f,
                RotationSpeed = 90f,
                PitchMin = math.radians(-30f),
                PitchMax = math.radians(85f),
                Smoothing = 0.1f,
                PanBoundsMin = new float3(-100f, 0f, -100f),
                PanBoundsMax = new float3(100f, 100f, 100f),
                UsePanBounds = false
            };
        }

        private sealed class Baker : Unity.Entities.Baker<Space4XCameraAuthoring>
        {
            public override void Bake(Space4XCameraAuthoring authoring)
            {
                var position = math.float3(authoring.transform.position);
                var rotation = new quaternion(
                    authoring.transform.rotation.x,
                    authoring.transform.rotation.y,
                    authoring.transform.rotation.z,
                    authoring.transform.rotation.w
                );
                var forward = math.float3(authoring.transform.forward);

                var cameraStateEntity = GetEntity(TransformUsageFlags.None);
                AddComponent(cameraStateEntity, new Space4XCameraState
                {
                    Position = position,
                    Rotation = rotation,
                    ZoomDistance = math.distance(position, position + forward * 10f),
                    FocusPoint = position + forward * 10f,
                    InitialPosition = position,
                    InitialRotation = rotation
                });

                var configEntity = GetEntity(TransformUsageFlags.None);
                var config = authoring.BuildConfigData();
                AddComponent(configEntity, config);
            }
        }
    }
}

