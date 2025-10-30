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

                if (authoring.profile != null)
                {
                    var configEntity = GetEntity(TransformUsageFlags.None);
                    AddComponent(configEntity, new Space4XCameraConfig
                    {
                        PanSpeed = authoring.profile.PanSpeed,
                        ZoomSpeed = authoring.profile.ZoomSpeed,
                        ZoomMinDistance = authoring.profile.ZoomMinDistance,
                        ZoomMaxDistance = authoring.profile.ZoomMaxDistance,
                        RotationSpeed = authoring.profile.RotationSpeed,
                        PitchMin = math.radians(authoring.profile.PitchMin),
                        PitchMax = math.radians(authoring.profile.PitchMax),
                        Smoothing = authoring.profile.Smoothing,
                        PanBoundsMin = math.float3(authoring.profile.PanBoundsMin),
                        PanBoundsMax = math.float3(authoring.profile.PanBoundsMax),
                        UsePanBounds = authoring.profile.UsePanBounds
                    });
                }
            }
        }
    }
}

