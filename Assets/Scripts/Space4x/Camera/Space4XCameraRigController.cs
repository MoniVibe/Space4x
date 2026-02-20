using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using Unity.Collections;
using Unity.Transforms;
using UCamera = UnityEngine.Camera;
using UObject = UnityEngine.Object;
using UDebug = UnityEngine.Debug;
using UTime = UnityEngine.Time;
using PureDOTS.Input;
using Space4X.Presentation;
using RmbContext = PureDOTS.Input.RmbContext;
using CameraRigState = PureDOTS.Runtime.Camera.CameraRigState;
using CameraRigMode = PureDOTS.Runtime.Camera.CameraRigMode;
using CameraRigType = PureDOTS.Runtime.Camera.CameraRigType;
using CameraRigService = PureDOTS.Runtime.Camera.CameraRigService;
using CameraRigApplier = PureDOTS.Runtime.Camera.CameraRigApplier;

namespace Space4X.Camera
{
    /// <summary>
    /// Space4X free-fly camera controller that mirrors Godgame semantics (WASD/QE/MMB/scroll).
    /// Publishes CameraRigState as the single source of truth for CameraRigApplier.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Space4XCameraRigController : MonoBehaviour
    {
        [Header("Render Target")]
        [SerializeField] private UCamera targetCamera;

        public UCamera TargetCamera
        {
            get => targetCamera;
            set => targetCamera = value;
        }

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 30f;
        [SerializeField] private float verticalSpeed = 20f;
        [SerializeField] private float panSpeedPerPixel = 0.1f;

        [Header("Rotation")]
        [SerializeField] private float rotationSpeed = 0.2f;
        [SerializeField] private float minPitch = -80f;
        [SerializeField] private float maxPitch = 80f;

        [Header("Zoom")]
        [SerializeField] private float zoomSpeed = 40f;
        [FormerlySerializedAs("minDistance")]
        [SerializeField] private float minZoomDistance = 5f;
        [FormerlySerializedAs("maxDistance")]
        [SerializeField] private float maxZoomDistance = 300f;
        [SerializeField] private float scrollUnitsPerNotch = 120f;

        [Header("Defaults")]
        [FormerlySerializedAs("focusPoint")]
        [SerializeField] private Vector3 defaultPosition = new Vector3(0f, 50f, -50f);
        [FormerlySerializedAs("yawDegrees")]
        [SerializeField] private float defaultYawDegrees = 0f;
        [FormerlySerializedAs("pitchDegrees")]
        [SerializeField] private float defaultPitchDegrees = 45f;
        [FormerlySerializedAs("distance")]
        [SerializeField] private float defaultFocusDistance = 60f;
        [SerializeField] private bool resetToDefaultOnStart = true;

        [Header("Y-Axis Lock")]
        [SerializeField] private bool yAxisLocked = true;

        [Header("Selection Focus")]
        [SerializeField] private Key focusSelectedKey = Key.F;
        [SerializeField] private Key toggleFollowKey = Key.G;
        [SerializeField] private float followLerp = 6f;
        [SerializeField] private float followVerticalOffset = 8f;

        [Header("Cinematic Take")]
        [SerializeField] private bool cinematicEnabled = true;
        [SerializeField] private Key toggleCinematicKey = Key.C;
        [SerializeField] private float cinematicDurationSeconds = 45f;
        [SerializeField] private float cinematicOrbitTurns = 1.25f;
        [SerializeField] private float cinematicHeightWave = 18f;

        [Header("Input Actions (Input System)")]
        [SerializeField] private InputActionReference moveAction;
        [SerializeField] private InputActionReference verticalAction;
        [SerializeField] private InputActionReference yAxisLockToggleAction;
        [SerializeField] private InputActionReference orbitAction;
        [SerializeField] private InputActionReference panAction;
        [SerializeField] private InputActionReference zoomAction;

        [Header("Input Profile (Optional)")]
        [SerializeField] private HandCameraInputProfile inputProfile;

        [Header("Router (Optional)")]
        [SerializeField] private HandCameraInputRouter inputRouter;

        private InputAction moveProfileAction;
        private InputAction verticalProfileAction;
        private InputAction yAxisToggleProfileAction;
        private InputAction orbitProfileAction;
        private InputAction panProfileAction;
        private InputAction zoomProfileAction;

        [Header("ECS Integration")]
        [SerializeField] private byte playerId = 0;

        private World _ecsWorld;
        private EntityQuery _rtsInputQuery;
        private bool _rtsQueryValid;
        private EntityQuery _selectedQuery;
        private bool _selectedQueryValid;

        // Internal state
        private Vector3 _position;
        private float _yaw;
        private float _pitch;
        private Quaternion _rotation;
        private bool _applierEnsured;
        private bool _loggedMissingCamera;
        private bool _followSelected;
        private Entity _followEntity;
        private Vector3 _followOffset;
        private bool _cinematicActive;
        private float _cinematicTime;
        private Vector3 _cinematicCenter;
        private float _cinematicBaseYaw;
        private float _cinematicBaseRadius;
        private float _cinematicBaseHeight;

        // Pan and zoom helpers
        private bool _isDraggingPan;
        private Vector3 _panWorldStart;
        private Vector3 _panPivotStart;
        private Plane _panPlane = new Plane(Vector3.up, Vector3.zero);
        private readonly Plane _groundPlane = new Plane(Vector3.up, Vector3.zero);

        public bool IsFollowSelectedActive => _followSelected;
        public bool IsCinematicActive => _cinematicActive;
        public bool IsYAxisLocked => yAxisLocked;

        public void SetYAxisLocked(bool locked)
        {
            yAxisLocked = locked;
        }

        private void OnEnable()
        {
            ResolveProfileActions();
            EnableActions();
            EnsureCameraReference();

            if (resetToDefaultOnStart)
            {
                ResetToDefaultPose();
            }
            else if (targetCamera != null)
            {
                var camTransform = targetCamera.transform;
                _position = camTransform.position;
                var euler = camTransform.rotation.eulerAngles;
                _yaw = euler.y;
                _pitch = Mathf.Clamp(euler.x, minPitch, maxPitch);
                _rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            }
        }

        private void OnDisable()
        {
            DisableActions();

            if (_rtsQueryValid)
            {
                try
                {
                    _rtsInputQuery.Dispose();
                }
                catch
                {
                    // World may already be tearing down.
                }

                _rtsQueryValid = false;
            }

            if (_selectedQueryValid)
            {
                try
                {
                    _selectedQuery.Dispose();
                }
                catch
                {
                    // World may already be tearing down.
                }

                _selectedQueryValid = false;
            }
        }

        private void Update()
        {
            EnsureCameraReference();
            EnsureInputRouter();

            float dt = UTime.deltaTime;
            var moveInput = ReadMoveInput();
            float verticalInput = ReadVerticalInput();
            Vector2 orbitDelta = ReadOrbitDelta();
            Vector2 panDelta = ReadPanDelta();
            float zoomValue = ReadZoomValue();
            bool togglePressed = ReadYAxisToggle();
            var context = inputRouter != null ? inputRouter.CurrentContext : default;
            var keyboard = Keyboard.current;

            if (keyboard != null && keyboard[toggleCinematicKey].wasPressedThisFrame && cinematicEnabled)
            {
                ToggleCinematicTake();
            }

            if (_cinematicActive)
            {
                UpdateCinematicTake(dt);
                ConsumeCameraRequests();
                return;
            }

            if (keyboard != null && keyboard[focusSelectedKey].wasPressedThisFrame)
            {
                FocusSelectedEntity();
            }

            if (keyboard != null && keyboard[toggleFollowKey].wasPressedThisFrame)
            {
                ToggleFollowSelected();
            }

            bool hasManualInput =
                moveInput.sqrMagnitude > 1e-4f ||
                Mathf.Abs(verticalInput) > 1e-4f ||
                orbitDelta.sqrMagnitude > 1e-4f ||
                panDelta.sqrMagnitude > 1e-4f ||
                Mathf.Abs(zoomValue) > 0.01f;

            if (_followSelected && hasManualInput)
            {
                _followSelected = false;
            }

            if (_followSelected)
            {
                UpdateFollowSelected(dt);
                ConsumeCameraRequests();
                return;
            }

            if (togglePressed)
            {
                yAxisLocked = !yAxisLocked;
            }

            if (ShouldApplyRotation(orbitDelta))
            {
                _yaw += orbitDelta.x * rotationSpeed;
                _yaw = NormalizeDegrees(_yaw);
                _pitch = Mathf.Clamp(_pitch - orbitDelta.y * rotationSpeed, minPitch, maxPitch);
            }

            _rotation = Quaternion.Euler(_pitch, _yaw, 0f);

            ApplyTranslation(moveInput, verticalInput, dt);

            if (Mathf.Abs(zoomValue) > 0.01f)
            {
                HandleZoom(zoomValue, in context);
            }

            if (panDelta.sqrMagnitude > 0.0001f)
            {
                HandleDragPan(panDelta, in context);
            }
            else
            {
                _isDraggingPan = false;
            }

            ConsumeCameraRequests();
        }

        private void LateUpdate()
        {
            PublishCommandPlaneState();

            var state = new CameraRigState
            {
                Focus = _position,
                Pitch = _pitch,
                Yaw = _yaw,
                Roll = 0f,
                Distance = 0f,
                Mode = CameraRigMode.FreeFly,
                PerspectiveMode = true,
                FieldOfView = targetCamera != null ? targetCamera.fieldOfView : 60f,
                RigType = CameraRigType.Space4X
            };

            CameraRigService.Publish(state);
        }

        private void ApplyTranslation(in Vector2 moveInput, float verticalInput, float dt)
        {
            Vector2 planar = moveInput;
            if (planar.sqrMagnitude > 1e-4f)
            {
                planar = planar.normalized;
            }

            Quaternion yawRotation = Quaternion.Euler(0f, _yaw, 0f);
            Quaternion movementRotation = yAxisLocked ? yawRotation : _rotation;

            Vector3 forward = movementRotation * Vector3.forward;
            Vector3 right = movementRotation * Vector3.right;
            Vector3 up = yAxisLocked ? Vector3.up : movementRotation * Vector3.up;

            Vector3 worldMove = (forward * planar.y + right * planar.x) * (moveSpeed * dt);
            Vector3 verticalMove = up * (verticalInput * verticalSpeed * dt);

            _position += worldMove + verticalMove;
        }

        private void HandleZoom(float scrollValue, in RmbContext context)
        {
            if (inputRouter != null && context.PointerOverUI)
            {
                return;
            }

            float unitsPerNotch = Mathf.Abs(scrollUnitsPerNotch) > 1e-4f ? scrollUnitsPerNotch : 120f;
            float scrollNotches = scrollValue / unitsPerNotch;
            float zoomAmount = scrollNotches * zoomSpeed;

            Vector3 zoomTarget;
            if (inputRouter != null && context.HasWorldHit)
            {
                zoomTarget = (Vector3)context.WorldPoint;
            }
            else
            {
                zoomTarget = SampleFallbackZoomTarget();
            }

            Vector3 camToTarget = zoomTarget - _position;
            float distance = camToTarget.magnitude;
            float targetDistance = Mathf.Clamp(distance - zoomAmount, minZoomDistance, maxZoomDistance);
            float appliedDelta = distance - targetDistance;
            if (Mathf.Abs(appliedDelta) < 1e-4f)
            {
                return;
            }

            _position += camToTarget.normalized * appliedDelta;
        }

        private Vector3 SampleFallbackZoomTarget()
        {
            var cam = targetCamera != null ? targetCamera : UCamera.main;
            if (cam == null)
            {
                return _position + (_rotation * Vector3.forward * 10f);
            }

            Vector2 pointer = inputRouter != null
                ? inputRouter.PointerPosition
                : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Ray ray = cam.ScreenPointToRay(new Vector3(pointer.x, pointer.y, 0f));
            if (_groundPlane.Raycast(ray, out float enter))
            {
                return ray.GetPoint(enter);
            }

            return _position + (_rotation * Vector3.forward * 25f);
        }

        private void HandleDragPan(in Vector2 panDelta, in RmbContext context)
        {
            if (inputRouter != null && context.PointerOverUI)
            {
                _isDraggingPan = false;
                return;
            }

            bool panAllowed = inputRouter == null || (inputRouter.LeftClickAction?.IsPressed() ?? false);
            if (!panAllowed)
            {
                _isDraggingPan = false;
                return;
            }

            if (!_isDraggingPan)
            {
                _isDraggingPan = true;
                if (inputRouter != null && context.HasWorldHit && context.HitGround)
                {
                    _panWorldStart = (Vector3)context.WorldPoint;
                    _panPivotStart = _position;
                    _panPlane = new Plane(Vector3.up, _panWorldStart);
                }
                return;
            }

            if (inputRouter != null && context.HasWorldHit)
            {
                Vector3 worldNow = (Vector3)context.WorldPoint;
                if (_panPlane.Raycast(context.PointerRay, out float enter))
                {
                    worldNow = context.PointerRay.GetPoint(enter);
                }

                Vector3 deltaWorld = _panWorldStart - worldNow;
                _position = _panPivotStart + deltaWorld;
                return;
            }

            if (panDelta.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Quaternion yawRotation = Quaternion.Euler(0f, _yaw, 0f);
            Vector3 right = yawRotation * Vector3.right;
            Vector3 forward = yawRotation * Vector3.forward;

            Vector3 pan = (-right * panDelta.x - forward * panDelta.y) * panSpeedPerPixel;
            _position += pan;
        }

        private void ConsumeCameraRequests()
        {
            if (!TryEnsureRtsInputQuery(out var em))
            {
                return;
            }

            if (_rtsInputQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var rtsEntity = _rtsInputQuery.GetSingletonEntity();
            if (!em.HasBuffer<CameraRequestEvent>(rtsEntity))
            {
                return;
            }

            var requests = em.GetBuffer<CameraRequestEvent>(rtsEntity);
            for (int i = requests.Length - 1; i >= 0; i--)
            {
                var req = requests[i];
                if (req.PlayerId != playerId)
                {
                    continue;
                }

                switch (req.Kind)
                {
                    case CameraRequestKind.FocusWorld:
                    {
                        var target = new Vector3(req.WorldPosition.x, req.WorldPosition.y, req.WorldPosition.z);
                        float snapDistance = Mathf.Max(minZoomDistance, defaultFocusDistance);
                        _position = target - (_rotation * Vector3.forward * snapDistance);
                        requests.RemoveAt(i);
                        break;
                    }

                    case CameraRequestKind.RecallBookmark:
                    {
                        var rot = new Quaternion(
                            req.BookmarkRotation.value.x,
                            req.BookmarkRotation.value.y,
                            req.BookmarkRotation.value.z,
                            req.BookmarkRotation.value.w);

                        var euler = rot.eulerAngles;
                        _yaw = euler.y;
                        _pitch = Mathf.Clamp(euler.x, minPitch, maxPitch);
                        _rotation = Quaternion.Euler(_pitch, _yaw, 0f);

                        _position = new Vector3(req.BookmarkPosition.x, req.BookmarkPosition.y, req.BookmarkPosition.z);
                        requests.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        private void PublishCommandPlaneState()
        {
            if (!TryEnsureRtsInputQuery(out var entityManager))
            {
                return;
            }

            if (_rtsInputQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var rtsEntity = _rtsInputQuery.GetSingletonEntity();
            bool hasState = entityManager.HasComponent<RtsCommandPlaneState>(rtsEntity);
            var planeState = hasState
                ? entityManager.GetComponentData<RtsCommandPlaneState>(rtsEntity)
                : CreateDefaultCommandPlaneState();

            float planeHeight = planeState.HasLastCommandPoint != 0
                ? planeState.LastCommandPoint.y
                : planeState.PlaneHeight;

            if (math.abs(planeHeight) < 1e-3f && planeState.HasLastCommandPoint == 0)
            {
                planeHeight = EstimateCommandPlaneAnchor().y;
            }

            Vector3 anchor = EstimateCommandPlaneAnchor();
            planeState.PlaneNormal = new float3(0f, 1f, 0f);
            planeState.PlaneHeight = planeHeight;
            planeState.PlaneOrigin = new float3(anchor.x, planeHeight, anchor.z);

            if (hasState)
            {
                entityManager.SetComponentData(rtsEntity, planeState);
            }
            else
            {
                entityManager.AddComponentData(rtsEntity, planeState);
            }
        }

        private RtsCommandPlaneState CreateDefaultCommandPlaneState()
        {
            Vector3 anchor = EstimateCommandPlaneAnchor();
            return new RtsCommandPlaneState
            {
                PlaneOrigin = new float3(anchor.x, anchor.y, anchor.z),
                PlaneNormal = new float3(0f, 1f, 0f),
                PlaneHeight = anchor.y,
                LastCommandPoint = float3.zero,
                HasLastCommandPoint = 0
            };
        }

        private Vector3 EstimateCommandPlaneAnchor()
        {
            if (TryGetSelectedWorldPosition(out var selectedPosition, out _))
            {
                return selectedPosition;
            }

            float distance = Mathf.Clamp(defaultFocusDistance, minZoomDistance, maxZoomDistance);
            return _position + (_rotation * Vector3.forward * distance);
        }

        private bool TryEnsureRtsInputQuery(out EntityManager entityManager)
        {
            if (_ecsWorld == null || !_ecsWorld.IsCreated)
            {
                _ecsWorld = World.DefaultGameObjectInjectionWorld;
                if (_ecsWorld == null || !_ecsWorld.IsCreated)
                {
                    entityManager = default;
                    return false;
                }
            }

            entityManager = _ecsWorld.EntityManager;
            if (!_rtsQueryValid)
            {
                _rtsInputQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<RtsInputSingletonTag>());
                _rtsQueryValid = true;
            }

            return true;
        }

        private bool TryEnsureSelectedQuery(out EntityManager entityManager)
        {
            if (!TryEnsureRtsInputQuery(out entityManager))
            {
                return false;
            }

            if (!_selectedQueryValid)
            {
                _selectedQuery = entityManager.CreateEntityQuery(new EntityQueryDesc
                {
                    All = new[]
                    {
                        ComponentType.ReadOnly<PureDOTS.Input.SelectedTag>(),
                        ComponentType.ReadOnly<LocalTransform>()
                    }
                });
                _selectedQueryValid = true;
            }

            return true;
        }

        private bool TryGetSelectedWorldPosition(out Vector3 worldPosition, out Entity entity)
        {
            worldPosition = default;
            entity = Entity.Null;

            if (!TryEnsureSelectedQuery(out var entityManager))
            {
                return false;
            }

            var count = _selectedQuery.CalculateEntityCount();
            if (count <= 0)
            {
                return false;
            }

            if (count == 1)
            {
                entity = _selectedQuery.GetSingletonEntity();
            }
            else
            {
                using var entities = _selectedQuery.ToEntityArray(Allocator.Temp);
                entity = entities.Length > 0 ? entities[0] : Entity.Null;
            }

            if (entity == Entity.Null || !entityManager.Exists(entity) || !entityManager.HasComponent<LocalTransform>(entity))
            {
                return false;
            }

            var tx = entityManager.GetComponentData<LocalTransform>(entity);
            worldPosition = new Vector3(tx.Position.x, tx.Position.y, tx.Position.z);
            return true;
        }

        private bool TryGetEntityWorldPosition(in Entity entity, out Vector3 worldPosition)
        {
            worldPosition = default;

            if (entity == Entity.Null || !TryEnsureRtsInputQuery(out var entityManager))
            {
                return false;
            }

            if (!entityManager.Exists(entity) || !entityManager.HasComponent<LocalTransform>(entity))
            {
                return false;
            }

            var tx = entityManager.GetComponentData<LocalTransform>(entity);
            worldPosition = new Vector3(tx.Position.x, tx.Position.y, tx.Position.z);
            return true;
        }

        private void FocusSelectedEntity()
        {
            if (!TryGetSelectedWorldPosition(out var target, out var selected))
            {
                return;
            }

            _followEntity = selected;
            var snapDistance = Mathf.Clamp(math.max(minZoomDistance, defaultFocusDistance), minZoomDistance, maxZoomDistance);
            _position = target - (_rotation * Vector3.forward * snapDistance);
            LookAtWorld(target + Vector3.up * followVerticalOffset, 1f);
        }

        private void ToggleFollowSelected()
        {
            if (_followSelected)
            {
                _followSelected = false;
                return;
            }

            if (!TryGetSelectedWorldPosition(out var target, out var selected))
            {
                _followSelected = false;
                return;
            }

            _followEntity = selected;
            _followOffset = _position - target;
            if (_followOffset.sqrMagnitude < 1e-4f)
            {
                var fallbackDistance = Mathf.Clamp(math.max(minZoomDistance, defaultFocusDistance), minZoomDistance, maxZoomDistance);
                _followOffset = -(_rotation * Vector3.forward * fallbackDistance);
            }

            _followSelected = true;
        }

        private void UpdateFollowSelected(float dt)
        {
            if (!TryGetEntityWorldPosition(_followEntity, out var target))
            {
                if (!TryGetSelectedWorldPosition(out target, out _followEntity))
                {
                    _followSelected = false;
                    return;
                }
            }

            var desiredOffset = _followOffset;
            desiredOffset.y = math.max(desiredOffset.y, followVerticalOffset);
            var desiredPos = target + desiredOffset;
            var gain = 1f - Mathf.Exp(-math.max(0.01f, followLerp) * dt);
            _position = Vector3.Lerp(_position, desiredPos, gain);
            LookAtWorld(target + Vector3.up * followVerticalOffset, gain);
        }

        private void ToggleCinematicTake()
        {
            if (_cinematicActive)
            {
                _cinematicActive = false;
                return;
            }

            StartCinematicTake();
        }

        private void StartCinematicTake()
        {
            _followSelected = false;
            _cinematicActive = true;
            _cinematicTime = 0f;

            if (!TryGetSelectedWorldPosition(out _cinematicCenter, out _))
            {
                _cinematicCenter = SampleFallbackZoomTarget();
            }

            var toCamera = _position - _cinematicCenter;
            _cinematicBaseRadius = Mathf.Clamp(new Vector2(toCamera.x, toCamera.z).magnitude, 50f, maxZoomDistance);
            _cinematicBaseHeight = Mathf.Clamp(toCamera.y, 20f, 140f);
            _cinematicBaseYaw = Mathf.Atan2(toCamera.x, toCamera.z) * Mathf.Rad2Deg;
        }

        private void UpdateCinematicTake(float dt)
        {
            _cinematicTime += dt;
            float duration = Mathf.Clamp(cinematicDurationSeconds, 30f, 60f);
            float t = Mathf.Clamp01(_cinematicTime / duration);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            float yaw = _cinematicBaseYaw + 360f * cinematicOrbitTurns * eased;
            float radius = Mathf.Lerp(_cinematicBaseRadius * 1.15f, _cinematicBaseRadius * 0.75f, eased);
            float height = _cinematicBaseHeight + Mathf.Sin(eased * Mathf.PI * 2f) * cinematicHeightWave;

            var radial = Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 0f, radius);
            _position = _cinematicCenter + radial + Vector3.up * height;
            LookAtWorld(_cinematicCenter + Vector3.up * followVerticalOffset, 1f);

            if (t >= 1f)
            {
                _cinematicActive = false;
            }
        }

        private void LookAtWorld(in Vector3 target, float blend)
        {
            var dir = target - _position;
            if (dir.sqrMagnitude <= 1e-6f)
            {
                return;
            }

            var desired = Quaternion.LookRotation(dir.normalized, Vector3.up);
            var desiredEuler = desired.eulerAngles;
            var desiredYaw = NormalizeDegrees(desiredEuler.y);
            var desiredPitch = Mathf.Clamp(NormalizeDegrees(desiredEuler.x), minPitch, maxPitch);
            var t = Mathf.Clamp01(blend);
            _yaw = Mathf.LerpAngle(_yaw, desiredYaw, t);
            _pitch = Mathf.Lerp(_pitch, desiredPitch, t);
            _rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        private void ResolveProfileActions()
        {
            moveProfileAction = null;
            verticalProfileAction = null;
            yAxisToggleProfileAction = null;
            orbitProfileAction = null;
            panProfileAction = null;
            zoomProfileAction = null;

            if (inputProfile == null)
            {
                return;
            }

            var asset = inputProfile.InputActions;
            if (asset == null)
            {
                UDebug.LogWarning("[Space4XCameraRigController] HandCameraInputProfile assigned but InputActionAsset is missing.", this);
                return;
            }

            var map = asset.FindActionMap(inputProfile.ActionMapName, throwIfNotFound: false);
            if (map == null)
            {
                UDebug.LogWarning($"[Space4XCameraRigController] Action map '{inputProfile.ActionMapName}' not found in HandCameraInputProfile.", this);
                return;
            }

            moveProfileAction = ResolveProfileAction(map, inputProfile.MoveActionName, inputProfile.DefaultMoveBinding, moveAction, "Move");
            verticalProfileAction = ResolveProfileAction(map, inputProfile.VerticalActionName, inputProfile.DefaultVerticalBinding, verticalAction, "Vertical");
            yAxisToggleProfileAction = ResolveProfileAction(map, inputProfile.YAxisLockToggleActionName, inputProfile.DefaultYAxisLockToggleBinding, yAxisLockToggleAction, "YAxisLockToggle");
            orbitProfileAction = ResolveProfileAction(map, inputProfile.OrbitActionName, inputProfile.DefaultOrbitBinding, orbitAction, "Orbit");
            panProfileAction = ResolveProfileAction(map, inputProfile.PanActionName, inputProfile.DefaultPanBinding, panAction, "Pan");
            zoomProfileAction = ResolveProfileAction(map, inputProfile.ZoomActionName, inputProfile.DefaultZoomBinding, zoomAction, "Zoom");
        }

        private InputAction ResolveProfileAction(InputActionMap map, string actionName, string defaultBinding, InputActionReference overrideReference, string label)
        {
            if (overrideReference != null)
            {
                return null;
            }

            var action = map.FindAction(actionName, throwIfNotFound: false);
            if (action == null)
            {
                if (!string.IsNullOrEmpty(defaultBinding))
                {
                    UDebug.LogWarning($"[Space4XCameraRigController] Action '{actionName}' for {label} missing from HandCameraInputProfile. Expected binding like '{defaultBinding}'.", this);
                }
                else
                {
                    UDebug.LogWarning($"[Space4XCameraRigController] Action '{actionName}' for {label} missing from HandCameraInputProfile.", this);
                }
            }

            return action;
        }

        private void EnableActions()
        {
            moveAction?.action.Enable();
            verticalAction?.action.Enable();
            yAxisLockToggleAction?.action.Enable();
            orbitAction?.action.Enable();
            panAction?.action.Enable();
            zoomAction?.action.Enable();

            moveProfileAction?.Enable();
            verticalProfileAction?.Enable();
            yAxisToggleProfileAction?.Enable();
            orbitProfileAction?.Enable();
            panProfileAction?.Enable();
            zoomProfileAction?.Enable();
        }

        private void DisableActions()
        {
            moveAction?.action.Disable();
            verticalAction?.action.Disable();
            yAxisLockToggleAction?.action.Disable();
            orbitAction?.action.Disable();
            panAction?.action.Disable();
            zoomAction?.action.Disable();

            moveProfileAction?.Disable();
            verticalProfileAction?.Disable();
            yAxisToggleProfileAction?.Disable();
            orbitProfileAction?.Disable();
            panProfileAction?.Disable();
            zoomProfileAction?.Disable();
        }

        private Vector2 ReadMoveInput()
        {
            var action = moveAction != null ? moveAction.action : moveProfileAction;
            return action != null ? action.ReadValue<Vector2>() : Vector2.zero;
        }

        private float ReadVerticalInput()
        {
            var action = verticalAction != null ? verticalAction.action : verticalProfileAction;
            return action != null ? action.ReadValue<float>() : 0f;
        }

        private Vector2 ReadOrbitDelta()
        {
            var action = orbitAction != null ? orbitAction.action : orbitProfileAction;
            return action != null ? action.ReadValue<Vector2>() : Vector2.zero;
        }

        private Vector2 ReadPanDelta()
        {
            var action = panAction != null ? panAction.action : panProfileAction;
            return action != null ? action.ReadValue<Vector2>() : Vector2.zero;
        }

        private float ReadZoomValue()
        {
            var action = zoomAction != null ? zoomAction.action : zoomProfileAction;
            return action != null ? action.ReadValue<float>() : 0f;
        }

        private bool ReadYAxisToggle()
        {
            var action = yAxisLockToggleAction != null ? yAxisLockToggleAction.action : yAxisToggleProfileAction;
            return action != null && action.WasPressedThisFrame();
        }

        private bool ShouldApplyRotation(Vector2 orbitDelta)
        {
            if (orbitDelta.sqrMagnitude < 1e-4f)
            {
                return false;
            }

            if (inputRouter != null)
            {
                var middle = inputRouter.MiddleClickAction;
                if (middle != null)
                {
                    return middle.IsPressed();
                }
            }

            return true;
        }

        private void EnsureCameraReference()
        {
            if (targetCamera != null)
            {
                if (!_applierEnsured && targetCamera.GetComponent<CameraRigApplier>() == null)
                {
                    targetCamera.gameObject.AddComponent<CameraRigApplier>();
                }

                _applierEnsured = true;
                return;
            }

            targetCamera = UCamera.main ?? UObject.FindFirstObjectByType<UCamera>();
            if (targetCamera == null)
            {
                if (!_loggedMissingCamera)
                {
                    UDebug.LogWarning("[Space4XCameraRigController] No Unity Camera found; controller will run headless until one is available.", this);
                    _loggedMissingCamera = true;
                }
                return;
            }

            _loggedMissingCamera = false;
            if (targetCamera.GetComponent<CameraRigApplier>() == null)
            {
                targetCamera.gameObject.AddComponent<CameraRigApplier>();
            }

            _applierEnsured = true;
        }

        private void EnsureInputRouter()
        {
            if (inputRouter != null)
            {
                return;
            }

            inputRouter = GetComponent<HandCameraInputRouter>() ??
                          GetComponentInChildren<HandCameraInputRouter>() ??
                          UObject.FindFirstObjectByType<HandCameraInputRouter>();
        }

        private void ResetToDefaultPose()
        {
            _position = defaultPosition;
            _yaw = defaultYawDegrees;
            _pitch = Mathf.Clamp(defaultPitchDegrees, minPitch, maxPitch);
            _rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        private static float NormalizeDegrees(float degrees)
        {
            while (degrees > 180f) degrees -= 360f;
            while (degrees < -180f) degrees += 360f;
            return degrees;
        }
    }
}
