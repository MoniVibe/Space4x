using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Space4X.CameraSystem;

namespace Space4X.Registry
{
    /// <summary>
    /// DOTS authoring component that sets up Space4X camera with both ECS components
    /// and the new MonoBehaviour camera controller. Provides backward compatibility
    /// with existing ECS systems while enabling the new camera rig framework.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public sealed class Space4XCameraAuthoring : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField]
        private Space4XCameraProfile profile;

        [Header("New Camera Controller")]
        [Tooltip("Automatically add and configure the new Space4XCameraController")]
        [SerializeField] private bool _useNewCameraController = true;

        [Tooltip("Initial camera height for new controller")]
        [SerializeField] private float _initialHeight = 50f;

        [Tooltip("Initial camera distance for new controller")]
        [SerializeField] private float _initialDistance = 70.71f;

        private void Awake()
        {
            if (_useNewCameraController)
            {
                SetupNewCameraController();
            }
        }

        private void SetupNewCameraController()
        {
            // Check if we already have a controller
            var existingController = GetComponent<Space4XCameraController>();
            if (existingController != null)
            {
                Debug.Log("[Space4XCameraAuthoring] Space4XCameraController already exists, updating configuration.");
                ConfigureExistingController(existingController);
                return;
            }

            // Add the new camera controller
            var controller = gameObject.AddComponent<Space4XCameraController>();
            ConfigureController(controller);

            // Add CameraRigApplier if not present
            if (GetComponent<PureDOTS.Runtime.Camera.CameraRigApplier>() == null)
            {
                gameObject.AddComponent<PureDOTS.Runtime.Camera.CameraRigApplier>();
                Debug.Log("[Space4XCameraAuthoring] Added CameraRigApplier component.");
            }

            Debug.Log("[Space4XCameraAuthoring] Added and configured Space4XCameraController.");
        }

        private void ConfigureController(Space4XCameraController controller)
        {
            if (controller == null) return;

            var state = controller.GetCurrentState();

            // Set initial state based on transform and authoring settings
            state.Position = new Vector3(transform.position.x, _initialHeight, transform.position.z - _initialDistance);
            state.Distance = _initialDistance;
            state.PerspectiveMode = false;

            // Extract yaw from current rotation
            var euler = transform.rotation.eulerAngles;
            state.Yaw = euler.y;
            state.Pitch = 45f; // Default pitch

            controller.SetState(state);
        }

        private void ConfigureExistingController(Space4XCameraController controller)
        {
            ConfigureController(controller);
        }

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
