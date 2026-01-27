using System;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif
using PureDOTS.Input;
using Unity.Entities;
using Unity.Mathematics;
using PureDOTS.Runtime.Hybrid;
using UnityEngineCamera = UnityEngine.Camera;
#if GODGAME
using Godgame.Runtime;
#endif

namespace PureDOTS.Runtime.Camera
{
    /// <summary>
    /// Black & White 2 inspired camera controller: LMB pans across terrain, MMB orbits the scene,
    /// scroll wheel adjusts zoom radius. Terrain clamps are the only height restriction applied.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnityEngineCamera))]
    [RequireComponent(typeof(CameraRigApplier))]
    [RequireComponent(typeof(BW2CameraInputBridge))]
    public sealed class BW2StyleCameraController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] UnityEngine.Camera targetCamera;
        [SerializeField] Transform pivotTransform;
        [SerializeField] CameraRigType rigType = CameraRigType.BW2;

        [Header("Input")]
        [SerializeField] HandCameraInputRouter inputRouter;

        [Header("Ground")]
        [SerializeField] LayerMask groundMask = ~0;
        [SerializeField] float groundProbeDistance = 600f;

        public LayerMask GroundMask
        {
            get => groundMask;
            set => groundMask = value;
        }

        [Header("Pan")]
        [SerializeField] float panScale = 1f;

        public float PanScale
        {
            get => panScale;
            set => panScale = value;
        }
        [SerializeField] bool allowPanOverUI;

        [Header("Orbit")]
        [SerializeField] float orbitYawSensitivity = 0.25f;
        [SerializeField] float orbitPitchSensitivity = 0.25f;
        [SerializeField] Vector2 pitchClamp = new(-30f, 85f);
        [SerializeField] bool allowOrbitOverUI = true;

        [Header("Zoom")]
        [SerializeField] float zoomSpeed = 6f;

        public float ZoomSpeed
        {
            get => zoomSpeed;
            set => zoomSpeed = value;
        }
        [SerializeField] float minDistance = 6f;
        [SerializeField] float maxDistance = 220f;
        [SerializeField] bool invertZoom;
        [SerializeField] bool allowZoomOverUI = true;

        float yaw;
        float pitch;
        float distance;

        Transform runtimePivot;
        Vector3 pivotPosition;
        bool warnedMissingGroundMask;
        World handWorld;
        EntityQuery handQuery;
        bool handQueryValid;
        EntityQuery _rtsInputQuery;
        bool _rtsQueryValid;
        bool orbitPivotLocked;
        Vector3 lockedPivot;
        float lockedDistance;
        bool grabbing;
        Plane panPlane;
        Vector3 panWorldStart;
        Vector3 panPivotStart;
        Vector3 lockedPivotStart;
        bool lockedPivotGrabActive;
        float grabHeightOffset;

        static int s_activeRigCount;

        BW2CameraInputBridge.Snapshot _inputSnapshot;
        bool _hasSnapshot;
        RmbContext _routerContext;
        Vector3 _currentCameraPosition;
        Quaternion _currentCameraRotation = Quaternion.identity;
        Vector3 _currentCameraWorldPos;
        byte _playerId;

        public static bool HasActiveRig => s_activeRigCount > 0;

        Vector3 Pivot
        {
            get => pivotTransform != null ? pivotTransform.position : pivotPosition;
            set
            {
                if (pivotTransform != null) pivotTransform.position = value;
                else pivotPosition = value;
            }
        }

        void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = GetComponent<UnityEngine.Camera>();
                if (targetCamera == null)
                {
                    targetCamera = GetComponentInChildren<UnityEngine.Camera>();
                    if (targetCamera == null) targetCamera = UnityEngine.Camera.main;
                }
            }

            _currentCameraPosition = targetCamera != null ? targetCamera.transform.position : Vector3.zero;
            _currentCameraRotation = targetCamera != null ? targetCamera.transform.rotation : Quaternion.identity;
            _currentCameraWorldPos = _currentCameraPosition;
            _playerId = 0;

            EnsureInputRouter();

            if (pivotTransform == null)
            {
                runtimePivot = new GameObject("[CameraPivot]").transform;
                pivotTransform = runtimePivot;
            }
        }

        void OnEnable()
        {
            s_activeRigCount++;
        }

        void OnDisable()
        {
            s_activeRigCount = math.max(0, s_activeRigCount - 1);

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
        }

        void EnsureInputRouter()
        {
            if (inputRouter == null)
                inputRouter = GetComponent<HandCameraInputRouter>();
            if (inputRouter == null)
                inputRouter = GetComponentInParent<HandCameraInputRouter>();
            if (inputRouter == null)
                inputRouter = FindFirstObjectByType<HandCameraInputRouter>();
            if (inputRouter == null && !warnedMissingGroundMask)
            {
                Debug.LogWarning($"{nameof(BW2StyleCameraController)} on {name} could not find {nameof(HandCameraInputRouter)}; input will be inactive.", this);
            }
        }

        void Update()
        {
            if (inputRouter == null)
            {
                EnsureInputRouter();
                if (inputRouter == null)
                {
                    return;
                }
            }

            UpdateInputSnapshot();
            ConsumeCameraRequests();

            // Basic pan/orbit/zoom based on snapshot
            ApplyInput();

            // Publish rig state
            PublishRig();
        }

        void ConsumeCameraRequests()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            var em = world.EntityManager;
            if (!_rtsQueryValid)
            {
                _rtsInputQuery = em.CreateEntityQuery(ComponentType.ReadOnly<RtsInputSingletonTag>());
                _rtsQueryValid = true;
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
                if (req.PlayerId != _playerId)
                {
                    continue;
                }

                switch (req.Kind)
                {
                    case CameraRequestKind.FocusWorld:
                        _currentCameraPosition = new Vector3(req.WorldPosition.x, req.WorldPosition.y, req.WorldPosition.z);
                        requests.RemoveAt(i);
                        break;

                    case CameraRequestKind.RecallBookmark:
                        {
                            var rot = new Quaternion(
                                req.BookmarkRotation.value.x,
                                req.BookmarkRotation.value.y,
                                req.BookmarkRotation.value.z,
                                req.BookmarkRotation.value.w);

                            var euler = rot.eulerAngles;
                            yaw = euler.y;
                            pitch = math.clamp(euler.x, pitchClamp.x, pitchClamp.y);

                            var camPos = new Vector3(req.BookmarkPosition.x, req.BookmarkPosition.y, req.BookmarkPosition.z);
                            _currentCameraPosition = camPos + (rot * Vector3.forward * distance);
                            _currentCameraRotation = rot;
                            requests.RemoveAt(i);
                        }
                        break;
                }
            }
        }

        void UpdateInputSnapshot()
        {
            _hasSnapshot = BW2CameraInputBridge.TryGetSnapshot(out _inputSnapshot);
        }

        void ApplyInput()
        {
            if (!_hasSnapshot) return;
            var context = inputRouter.CurrentContext;
            bool pointerOverUI = context.PointerOverUI;

            bool panAllowed = allowPanOverUI || !pointerOverUI;
            bool orbitAllowed = allowOrbitOverUI || !pointerOverUI;
            bool orbitHeld = _inputSnapshot.MiddleHeld || _inputSnapshot.RightHeld;
            bool orbitPressed = _inputSnapshot.MiddlePressed || _inputSnapshot.RightPressed;

            if (!orbitHeld)
            {
                orbitPivotLocked = false;
            }

            if (orbitPressed && orbitAllowed)
            {
                orbitPivotLocked = true;
                lockedDistance = distance;

                lockedPivot = context.HasWorldHit ? (Vector3)context.WorldPoint : _currentCameraPosition;
            }

            if (orbitPivotLocked)
            {
                _currentCameraPosition = lockedPivot;
            }

            // Zoom
            float scroll = _inputSnapshot.Scroll;
            if ((allowZoomOverUI || !pointerOverUI) && math.abs(scroll) > 0.01f)
            {
                float zoomDir = invertZoom ? -scroll : scroll;
                float scrollNotches = zoomDir / 120f;
                distance = math.clamp(distance - scrollNotches * (zoomSpeed * 2f), minDistance, maxDistance);
            }

            // Grab-land pan (LMB drag): lock a ground plane on press; keep the grabbed point under cursor.
            if (!orbitHeld && panAllowed)
            {
                if (_inputSnapshot.LeftPressed)
                {
                    if (context.HasWorldHit && context.HitGround)
                    {
                        grabbing = true;
                        panWorldStart = (Vector3)context.WorldPoint;
                        panPivotStart = _currentCameraPosition;
                        panPlane = new Plane(Vector3.up, panWorldStart);
                    }
                }
                else if (_inputSnapshot.LeftReleased)
                {
                    grabbing = false;
                }

                if (grabbing && _inputSnapshot.LeftHeld)
                {
                    if (context.HasWorldHit)
                    {
                        Vector3 worldNow = (Vector3)context.WorldPoint;
                        if (panPlane.Raycast(context.PointerRay, out float enter))
                        {
                            worldNow = context.PointerRay.GetPoint(enter);
                        }
                        Vector3 deltaWorld = panWorldStart - worldNow;
                        _currentCameraPosition = panPivotStart + deltaWorld;
                    }
                }
            }
            else if (_inputSnapshot.LeftReleased)
            {
                grabbing = false;
            }

            // Orbit
            if (orbitHeld && orbitAllowed)
            {
                yaw += _inputSnapshot.PointerDelta.x * orbitYawSensitivity;
                pitch = math.clamp(pitch - _inputSnapshot.PointerDelta.y * orbitPitchSensitivity, pitchClamp.x, pitchClamp.y);
            }

            // Pan (edge scroll)
            Vector2 delta = Vector2.zero;
            if (_inputSnapshot.EdgeLeft) delta.x -= 1f;
            if (_inputSnapshot.EdgeRight) delta.x += 1f;
            if (_inputSnapshot.EdgeTop) delta.y += 1f;
            if (_inputSnapshot.EdgeBottom) delta.y -= 1f;

            if (!grabbing && !orbitPivotLocked && panAllowed && delta.sqrMagnitude > 0.0001f)
            {
                float panSpeed = panScale * math.max(distance, 1f);
                var yawRot = Quaternion.Euler(0f, yaw, 0f);
                Vector3 right = yawRot * Vector3.right;
                Vector3 forward = yawRot * Vector3.forward;
                _currentCameraPosition += (-right * delta.x + -forward * delta.y) * panSpeed * UnityEngine.Time.deltaTime;
            }

            // Apply to camera and pivot
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 camPos = _currentCameraPosition - rot * Vector3.forward * distance;
            _currentCameraWorldPos = camPos;
            Pivot = _currentCameraPosition;
            _currentCameraRotation = rot;
        }

        void PublishRig()
        {
            var state = new CameraRigState
            {
                Focus = _currentCameraPosition,
                Pitch = pitch,
                Yaw = yaw,
                Roll = 0f,
                Distance = distance,
                Mode = CameraRigMode.Orbit,
                PerspectiveMode = true,
                FieldOfView = targetCamera.fieldOfView,
                RigType = rigType
            };
            CameraRigService.Publish(state);
        }
    }
}
