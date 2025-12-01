using PureDOTS.Runtime.Hybrid;
using PureDOTS.Systems;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Space4X.Registry
{
    /// <summary>
    /// MonoBehaviour that reads Input System actions directly and writes to camera control state.
    /// Runs every frame to capture input before camera update.
    /// Non-Burst compatible due to Input System access.
    /// </summary>
    public class Space4XCameraInputSystem : MonoBehaviour
    {
        private bool _initialized;
        private bool _loggedFallback;
        private bool _loggedEnabled;
        private bool _warnedMissing;
        private bool _loggedInputAssetName;
        private InputActionMap _cameraMap;
        private World _world;
        private EntityQuery _controlStateQuery;
        private EntityQuery _configQuery;
        private int _inputLogFramesRemaining = 120;

        private void Awake()
        {
            _world = World.DefaultGameObjectInjectionWorld;
            
            if (_world != null)
            {
                // Create cached queries
                _configQuery = _world.EntityManager.CreateEntityQuery(typeof(Space4XCameraInputConfig));
                _controlStateQuery = _world.EntityManager.CreateEntityQuery(typeof(Space4XCameraControlState));
                
                // Provide a fallback config so camera controls work when no authoring component is present.
                if (_configQuery.IsEmptyIgnoreFilter)
                {
                    var entity = _world.EntityManager.CreateEntity(typeof(Space4XCameraInputConfig));
                    _world.EntityManager.SetComponentData(entity, new Space4XCameraInputConfig
                    {
                        EnablePan = true,
                        EnableZoom = true,
                        EnableVerticalMove = true,
                        EnableRotation = true,
                        RequireRightMouseForRotation = true
                    });

#if UNITY_EDITOR
                    Debug.Log("[Space4XCameraInputSystem] Created default Space4XCameraInputConfig (no authoring found).");
#endif
                }
            }

            Debug.Log("[Space4XCameraInputSystem] System is running and looking for input...");
        }

        private void OnDestroy()
        {
            DisableCameraInputs();
        }

        private void Update()
        {
            if (_world == null)
            {
                _world = World.DefaultGameObjectInjectionWorld;
                if (_world == null)
                    return;
            }

            // Ensure queries exist (they may be null if world was recreated)
            if (_configQuery == null)
            {
                _configQuery = _world.EntityManager.CreateEntityQuery(typeof(Space4XCameraInputConfig));
            }
            if (_controlStateQuery == null)
            {
                _controlStateQuery = _world.EntityManager.CreateEntityQuery(typeof(Space4XCameraControlState));
            }

            if (!HybridControlCoordinator.Space4XInputEnabled)
            {
                DisableCameraInputs();
                ZeroOutCameraControlState();
                return;
            }

            if (!TryEnsureConfig(out var inputConfig))
            {
                return;
            }
            
            // Log once that system is running
            if (UnityEngine.Time.frameCount == 1)
            {
                Debug.Log("[Space4XCameraInputSystem] System is running and looking for input...");
            }

            if (!_initialized)
            {
                if (!TryFindInputActionAsset(out var inputActions))
                {
                    if (!_loggedFallback)
                    {
                        Debug.Log("[Space4XCameraInputSystem] Using built-in default camera input.");
                        _loggedFallback = true;
                    }
                    inputActions = BuildDefaultCameraInputAsset();
                }

                _cameraMap = inputActions.FindActionMap("Camera") ?? (inputActions.actionMaps.Count > 0 ? inputActions.actionMaps[0] : null);
                if (!_loggedInputAssetName && inputActions != null)
                {
                    Debug.Log($"[Space4XCameraInputSystem] Using InputActionAsset '{inputActions.name}' for camera controls.");
                    _loggedInputAssetName = true;
                }
                _initialized = true;
            }

            if (_cameraMap == null)
                return;

            // Ensure the InputActionAsset and map are enabled once
            if (!_cameraMap.enabled)
            {
                _cameraMap.Enable();
                if (!_cameraMap.asset.enabled)
                    _cameraMap.asset.Enable();
                if (!_loggedEnabled)
                {
                    Debug.Log("[Space4XCameraInputSystem] Enabled Camera action map");
                    _loggedEnabled = true;
                }
            }

            var panAction = _cameraMap.FindAction("Pan");
            var zoomAction = _cameraMap.FindAction("Zoom");
            var verticalMoveAction = _cameraMap.FindAction("VerticalMove") ?? _cameraMap.FindAction("Vertical");
            var rotateAction = _cameraMap.FindAction("Rotate");
            var resetAction = _cameraMap.FindAction("Reset");
            var toggleVerticalModeAction = _cameraMap.FindAction("ToggleVerticalMode");

            var missingPan = panAction == null;
            var missingZoom = zoomAction == null;
            var missingVertical = verticalMoveAction == null;
            var missingRotate = rotateAction == null;
            var missingReset = resetAction == null;
            var missingToggle = toggleVerticalModeAction == null;

            if (missingPan && missingZoom)
            {
                if (UnityEngine.Time.frameCount % 60 == 0)
                {
                    Debug.LogWarning("[Space4XCameraInputSystem] Camera input action map is missing both pan and zoom actions. Cannot drive camera input.");
                }
                return;
            }

            if (UnityEngine.Time.frameCount % 120 == 0 && (missingPan || missingZoom || missingVertical || missingRotate || missingReset || missingToggle))
            {
                Debug.LogWarning($"[Space4XCameraInputSystem] Missing actions - Pan:{!missingPan} Zoom:{!missingZoom} Vertical:{!missingVertical} Rotate:{!missingRotate} Reset:{!missingReset} ToggleVertical:{!missingToggle}. Continuing with available inputs.");
            }

            var controlState = ReadInputActions(inputConfig, panAction, zoomAction, verticalMoveAction, rotateAction, resetAction, toggleVerticalModeAction);
            
            var shouldLogFrame = _inputLogFramesRemaining > 0;
            if (shouldLogFrame)
            {
                Debug.Log($"[Space4XCameraInputSystem] Frame {UnityEngine.Time.frameCount}: Pan {controlState.PanInput}, Zoom {controlState.ZoomInput}, Vertical {controlState.VerticalMoveInput}, Rotate {controlState.RotateInput}, RotationEnabled {controlState.EnableRotation}");
                _inputLogFramesRemaining--;
            }
            else if (UnityEngine.Time.frameCount % 60 == 0 && (math.lengthsq(controlState.PanInput) > 0f || math.abs(controlState.ZoomInput) > 0f || math.abs(controlState.VerticalMoveInput) > 0f || math.lengthsq(controlState.RotateInput) > 0f))
            {
                Debug.Log($"[Space4XCameraInputSystem] Input - Pan: {controlState.PanInput}, Zoom: {controlState.ZoomInput}, Vertical: {controlState.VerticalMoveInput}, Rotate: {controlState.RotateInput}, RotationEnabled: {controlState.EnableRotation}");
            }
            
            // Check for singleton using cached query
            if (_controlStateQuery.IsEmptyIgnoreFilter)
            {
                var entity = _world.EntityManager.CreateEntity(typeof(Space4XCameraControlState));
                _world.EntityManager.SetComponentData(entity, controlState);
                Debug.Log("[Space4XCameraInputSystem] Created Space4XCameraControlState singleton");
            }
            else
            {
                var entity = _controlStateQuery.GetSingletonEntity();
                _world.EntityManager.SetComponentData(entity, controlState);
            }
        }

        static bool sWarned;
        static InputActionAsset BuildDefaultCameraInputAsset()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = new InputActionMap("Camera");

            var pan = map.AddAction("Pan", InputActionType.Value);
            var panComposite = pan.AddCompositeBinding("2DVector");
            panComposite.With("Up", "<Keyboard>/w");
            panComposite.With("Down", "<Keyboard>/s");
            panComposite.With("Left", "<Keyboard>/a");
            panComposite.With("Right", "<Keyboard>/d");
            var panCompositeArrows = pan.AddCompositeBinding("2DVector");
            panCompositeArrows.With("Up", "<Keyboard>/upArrow");
            panCompositeArrows.With("Down", "<Keyboard>/downArrow");
            panCompositeArrows.With("Left", "<Keyboard>/leftArrow");
            panCompositeArrows.With("Right", "<Keyboard>/rightArrow");
            pan.AddBinding("<Mouse>/delta");

            var rotate = map.AddAction("Rotate", InputActionType.Value);
            rotate.AddBinding("<Mouse>/delta");

            var zoom = map.AddAction("Zoom", InputActionType.Value);
            zoom.AddBinding("<Mouse>/scroll/y");
            zoom.AddBinding("<Keyboard>/equals");
            zoom.AddBinding("<Keyboard>/minus");

            var vertical = map.AddAction("VerticalMove", InputActionType.Value);
            var verticalComposite = vertical.AddCompositeBinding("1DAxis");
            verticalComposite.With("Negative", "<Keyboard>/q");
            verticalComposite.With("Positive", "<Keyboard>/e");

            asset.AddActionMap(map);
            return asset;
        }

        private bool TryFindInputActionAsset(out InputActionAsset asset)
        {
            try
            {
                Space4XCameraInputAuthoring authoring = null;
#if UNITY_2022_2_OR_NEWER
                authoring = Object.FindFirstObjectByType<Space4XCameraInputAuthoring>(FindObjectsInactive.Include);
#else
                var list = Object.FindObjectsOfType<Space4XCameraInputAuthoring>(true);
                if (list.Length > 0) authoring = list[0];
#endif

                if (authoring != null)
                {
                    asset = authoring.InputActions;
                    sWarned = false;
                    return asset != null;
                }

                if (!sWarned)
                {
                    Debug.LogWarning("Space4XCameraInputSystem: Space4XCameraInputAuthoring component not found in scene.");
                    sWarned = true;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Space4XCameraInputSystem: Error finding InputActionAsset: {ex.Message}");
            }
            asset = null;
            return false;
        }

        private Space4XCameraControlState ReadInputActions(
            Space4XCameraInputConfig config,
            InputAction panAction,
            InputAction zoomAction,
            InputAction verticalMoveAction,
            InputAction rotateAction,
            InputAction resetAction,
            InputAction toggleVerticalModeAction)
        {
            Vector2 panValue = Vector2.zero;
            float zoomScalar = 0f;
            float verticalMoveValue = 0f;
            Vector2 rotateValue = Vector2.zero;
            bool resetPressed = false;
            bool toggleVerticalModePressed = false;

            try
            {
                panValue = ReadVector2Safe(panAction);
                zoomScalar = ReadScalarSafe(zoomAction);
                verticalMoveValue = ReadScalarSafe(verticalMoveAction);
                rotateValue = ReadVector2Safe(rotateAction);
                resetPressed = resetAction != null && resetAction.WasPressedThisFrame();
                toggleVerticalModePressed = toggleVerticalModeAction != null && toggleVerticalModeAction.WasPressedThisFrame();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Space4XCameraInputSystem: Error reading input values: {ex.Message}");
            }

            bool rightMouseHeld = false;
            try
            {
                rightMouseHeld = Mouse.current != null && Mouse.current.rightButton.isPressed;
                
                // Lock cursor when RMB is held to prevent cursor position resolution issues
                if (rightMouseHeld && config.RequireRightMouseForRotation)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
                else if (Cursor.lockState == CursorLockMode.Locked)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
            }
            catch
            {
                // Mouse may be unavailable
            }

            // Allow rotation with mouse movement OR right-click + mouse movement (more flexible)
            var effectiveRotateInput = config.EnableRotation ? rotateValue : Vector2.zero;
            // If rotation is enabled but requires right mouse, check that condition
            bool rotationEnabled = config.EnableRotation && (!config.RequireRightMouseForRotation || rightMouseHeld);

            return new Space4XCameraControlState
            {
                PanInput = math.float2(panValue.x, panValue.y),
                ZoomInput = zoomScalar,
                VerticalMoveInput = verticalMoveValue,
                RotateInput = rotationEnabled ? math.float2(effectiveRotateInput.x, effectiveRotateInput.y) : float2.zero,
                ResetRequested = resetPressed,
                ToggleVerticalModeRequested = toggleVerticalModePressed,
                EnablePan = config.EnablePan,
                EnableZoom = config.EnableZoom,
                EnableVerticalMove = config.EnableVerticalMove,
                EnableRotation = rotationEnabled
            };
        }

        private static Vector2 ReadVector2Safe(InputAction action)
        {
            if (action == null)
            {
                return Vector2.zero;
            }

            try
            {
                return action.ReadValue<Vector2>();
            }
            catch (System.Exception)
            {
            }

            try
            {
                var scalar = action.ReadValue<float>();
                return new Vector2(scalar, 0f);
            }
            catch (System.Exception)
            {
            }

            return Vector2.zero;
        }

        private static float ReadScalarSafe(InputAction action)
        {
            if (action == null)
            {
                return 0f;
            }

            try
            {
                return action.ReadValue<float>();
            }
            catch (System.Exception)
            {
            }

            try
            {
                var value = action.ReadValue<Vector2>();
                return Mathf.Abs(value.y) >= Mathf.Abs(value.x) ? value.y : value.x;
            }
            catch (System.Exception)
            {
            }

            return 0f;
        }

        private void DisableCameraInputs()
        {
            try
            {
                var authoring = Object.FindFirstObjectByType<Space4XCameraInputAuthoring>();
                if (authoring?.InputActions == null)
                {
                    return;
                }

                var cameraMap = authoring.InputActions.FindActionMap("Camera", throwIfNotFound: false);
                if (cameraMap != null && cameraMap.enabled)
                {
                    cameraMap.Disable();
                }

                if (authoring.InputActions.enabled)
                {
                    authoring.InputActions.Disable();
                }
            }
            catch
            {
                // Intentionally ignored; disabling is best effort only.
            }
        }

        private void ZeroOutCameraControlState()
        {
            if (_world == null || _controlStateQuery == null)
                return;

            if (_controlStateQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var entity = _controlStateQuery.GetSingletonEntity();
            _world.EntityManager.SetComponentData(entity, default(Space4XCameraControlState));
        }

        private bool TryEnsureConfig(out Space4XCameraInputConfig config)
        {
            if (_world == null)
            {
                _world = World.DefaultGameObjectInjectionWorld;
                if (_world == null)
                {
                    config = default;
                    return false;
                }
            }

            // Try to get singleton using cached query
            if (_configQuery != null && !_configQuery.IsEmptyIgnoreFilter)
            {
                var configEntity = _configQuery.GetSingletonEntity();
                config = _world.EntityManager.GetComponentData<Space4XCameraInputConfig>(configEntity);
                return true;
            }

            Space4XCameraInputAuthoring authoring = null;
            try
            {
                authoring = Object.FindFirstObjectByType<Space4XCameraInputAuthoring>();
            }
            catch
            {
                // ignore - FindFirstObjectByType can throw when quitting
            }

            if (authoring == null)
            {
                if (UnityEngine.Time.frameCount == 1)
                {
                    Debug.LogWarning("[Space4XCameraInputSystem] Space4XCameraInputAuthoring not found. Camera input config missing.");
                }
                config = default;
                return false;
            }

            if (authoring.InputActions == null)
            {
                if (UnityEngine.Time.frameCount % 300 == 0)
                {
                    Debug.LogWarning("[Space4XCameraInputSystem] InputActionAsset is null on authoring component. Assign an InputActionAsset.");
                }
                config = default;
                return false;
            }

            config = authoring.BuildConfigData();

            var entity = _world.EntityManager.CreateEntity(typeof(Space4XCameraInputConfig));
            _world.EntityManager.SetComponentData(entity, config);
            Debug.Log("[Space4XCameraInputSystem] Created Space4XCameraInputConfig singleton at runtime from authoring component.");
            return true;
        }
    }
}
