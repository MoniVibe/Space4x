using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Camera controller authoring component that reads Input System actions and writes to DOTS singleton.
    /// Follows PureDOTS DivineHandInputBridge pattern for deterministic input routing.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public sealed class Space4XCameraController : MonoBehaviour
    {
        [Header("Input Actions")]
        [SerializeField]
        private InputActionAsset inputActions;

        [Header("Configuration")]
        [SerializeField]
        private Space4XCameraProfile profile;

        [Header("Enable Flags")]
        [SerializeField]
        private bool enablePan = true;

        [SerializeField]
        private bool enableZoom = true;

        [SerializeField]
        private bool enableRotation = false;

        private InputActionMap cameraActionMap;
        private InputAction panAction;
        private InputAction zoomAction;
        private InputAction rotateAction;
        private InputAction resetAction;

        private Camera cameraComponent;
        private World targetWorld;
        private EntityManager entityManager;

        private void Awake()
        {
            cameraComponent = GetComponent<Camera>();

            if (inputActions == null)
            {
                Debug.LogError("Space4XCameraController: InputActionAsset is not assigned.", this);
                enabled = false;
                return;
            }

            cameraActionMap = inputActions.FindActionMap("Camera");
            if (cameraActionMap == null)
            {
                Debug.LogError("Space4XCameraController: Camera action map not found in InputActionAsset.", this);
                enabled = false;
                return;
            }

            panAction = cameraActionMap.FindAction("Pan");
            zoomAction = cameraActionMap.FindAction("Zoom");
            rotateAction = cameraActionMap.FindAction("Rotate");
            resetAction = cameraActionMap.FindAction("Reset");

            if (panAction == null || zoomAction == null || rotateAction == null || resetAction == null)
            {
                Debug.LogError("Space4XCameraController: One or more camera actions not found.", this);
                enabled = false;
                return;
            }
        }

        private void OnEnable()
        {
            cameraActionMap?.Enable();
        }

        private void OnDisable()
        {
            cameraActionMap?.Disable();
        }

        private void Start()
        {
            targetWorld = World.DefaultGameObjectInjectionWorld;
            if (targetWorld == null || !targetWorld.IsCreated)
            {
                Debug.LogWarning("Space4XCameraController: DefaultWorld not available. Camera will initialize when world is created.", this);
                return;
            }

            entityManager = targetWorld.EntityManager;
            InitializeCameraState();
            InitializeCameraConfig();
        }

        private void Update()
        {
            if (targetWorld == null || !targetWorld.IsCreated)
            {
                targetWorld = World.DefaultGameObjectInjectionWorld;
                if (targetWorld != null && targetWorld.IsCreated)
                {
                    entityManager = targetWorld.EntityManager;
                    InitializeCameraState();
                    InitializeCameraConfig();
                }
                else
                {
                    return;
                }
            }

            if (entityManager == null || !entityManager.IsQueryValid(default))
            {
                return;
            }

            WriteInputToDOTS();
        }

        private void InitializeCameraState()
        {
            if (entityManager == null)
            {
                return;
            }

            var query = entityManager.CreateEntityQuery(typeof(Space4XCameraState));
            if (query.IsEmptyIgnoreFilter)
            {
                var entity = entityManager.CreateEntity(typeof(Space4XCameraState));
                var position = math.float3(transform.position);
                var rotation = new quaternion(transform.rotation.x, transform.rotation.y, transform.rotation.z, transform.rotation.w);
                var forward = math.float3(transform.forward);

                entityManager.SetComponentData(entity, new Space4XCameraState
                {
                    Position = position,
                    Rotation = rotation,
                    ZoomDistance = math.distance(position, position + forward * 10f),
                    FocusPoint = position + forward * 10f,
                    InitialPosition = position,
                    InitialRotation = rotation
                });
            }
            else
            {
                var entity = query.GetSingletonEntity();
                var state = entityManager.GetComponentData<Space4XCameraState>(entity);
                state.Position = math.float3(transform.position);
                state.Rotation = new quaternion(transform.rotation.x, transform.rotation.y, transform.rotation.z, transform.rotation.w);
                state.InitialPosition = state.Position;
                state.InitialRotation = state.Rotation;
                entityManager.SetComponentData(entity, state);
            }

            query.Dispose();
        }

        private void InitializeCameraConfig()
        {
            if (entityManager == null || profile == null)
            {
                return;
            }

            var query = entityManager.CreateEntityQuery(typeof(Space4XCameraConfig));
            if (query.IsEmptyIgnoreFilter)
            {
                var entity = entityManager.CreateEntity(typeof(Space4XCameraConfig));
                entityManager.SetComponentData(entity, CreateConfigFromProfile());
            }
            else
            {
                var entity = query.GetSingletonEntity();
                entityManager.SetComponentData(entity, CreateConfigFromProfile());
            }

            query.Dispose();
        }

        private Space4XCameraConfig CreateConfigFromProfile()
        {
            return new Space4XCameraConfig
            {
                PanSpeed = profile.PanSpeed,
                ZoomSpeed = profile.ZoomSpeed,
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

        private void WriteInputToDOTS()
        {
            if (entityManager == null)
            {
                return;
            }

            var query = entityManager.CreateEntityQuery(typeof(Space4XCameraControlState));
            if (query.IsEmptyIgnoreFilter)
            {
                var entity = entityManager.CreateEntity(typeof(Space4XCameraControlState));
                entityManager.SetComponentData(entity, ReadInputActions());
            }
            else
            {
                var entity = query.GetSingletonEntity();
                entityManager.SetComponentData(entity, ReadInputActions());
            }

            query.Dispose();
        }

        private Space4XCameraControlState ReadInputActions()
        {
            var panValue = panAction.ReadValue<Vector2>();
            var zoomValue = zoomAction.ReadValue<Vector2>();
            var rotateValue = rotateAction.ReadValue<Vector2>();
            var resetPressed = resetAction.WasPressedThisFrame();

            var rightMouseHeld = Mouse.current != null && Mouse.current.rightButton.isPressed;
            var effectiveRotateInput = (enableRotation && rightMouseHeld) ? rotateValue : Vector2.zero;

            return new Space4XCameraControlState
            {
                PanInput = math.float2(panValue.x, panValue.y),
                ZoomInput = zoomValue.y != 0f ? zoomValue.y : (zoomValue.x != 0f ? zoomValue.x : 0f),
                RotateInput = math.float2(effectiveRotateInput.x, effectiveRotateInput.y),
                ResetRequested = resetPressed,
                EnablePan = enablePan,
                EnableZoom = enableZoom,
                EnableRotation = enableRotation && rightMouseHeld
            };
        }
    }
}

