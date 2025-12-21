using PureDOTS.Runtime.Camera;
using PureDOTS.Input;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityCamera = UnityEngine.Camera;

namespace Space4X.Camera
{
    /// <summary>
    /// Lightweight Space4X camera rig controller that publishes CameraRigState via CameraRigService.
    /// Uses frame-time input (Input System) and avoids deterministic simulation dependencies.
    /// </summary>
    [DisallowMultipleComponent]
    public class Space4XCameraRigController : MonoBehaviour
    {
        public enum Vector2InputSemantics : byte
        {
            Auto = 0,
            NormalizedAxis = 1,
            PointerDelta = 2
        }

        public enum FloatInputSemantics : byte
        {
            Auto = 0,
            NormalizedAxis = 1,
            ScrollDelta = 2
        }

        [Header("References")]
        [SerializeField] private UnityCamera targetCamera;

        public UnityCamera TargetCamera
        {
            get => targetCamera;
            set => targetCamera = value;
        }

        [Header("Input Actions (Input System)")]
        [SerializeField] private InputActionReference orbitAction;
        [SerializeField] private InputActionReference panAction;
        [SerializeField] private InputActionReference zoomAction;

        [Header("Speeds")]
        [SerializeField] private float orbitDegreesPerSecond = 120f;
        [SerializeField] private float orbitDegreesPerPixel = 0.15f;
        [SerializeField] private float panUnitsPerSecond = 25f;
        [SerializeField] private float panUnitsPerPixel = 0.02f;
        [SerializeField] private float zoomUnitsPerSecond = 35f;
        [SerializeField] private float zoomUnitsPerNotch = 70f;

        [Header("Input Semantics")]
        [SerializeField] private Vector2InputSemantics orbitSemantics = Vector2InputSemantics.Auto;
        [SerializeField] private Vector2InputSemantics panSemantics = Vector2InputSemantics.Auto;
        [SerializeField] private FloatInputSemantics zoomSemantics = FloatInputSemantics.Auto;
        [SerializeField] private float scrollUnitsPerNotch = 120f;

        [Header("Limits")]
        [SerializeField] private float minDistance = 5f;
        [SerializeField] private float maxDistance = 300f;
        [SerializeField] private float minPitch = 5f;
        [SerializeField] private float maxPitch = 85f;

        [Header("State")]
        [SerializeField] private Vector3 focusPoint = Vector3.zero;
        [SerializeField] private float yawDegrees = 0f;
        [SerializeField] private float pitchDegrees = 45f;
        [SerializeField] private float distance = 60f;

        [Header("ECS Integration")]
        [SerializeField] private byte playerId = 0;

        private World _ecsWorld;
        private EntityQuery _rtsInputQuery;
        private bool _rtsQueryValid;
        private bool _applierEnsured;

        private void OnEnable()
        {
            orbitAction?.action.Enable();
            panAction?.action.Enable();
            zoomAction?.action.Enable();
        }

        private void OnDisable()
        {
            orbitAction?.action.Disable();
            panAction?.action.Disable();
            zoomAction?.action.Disable();

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

        private void Update()
        {
            // TEMP sanity: remove after confirming horizon reacts
            // transform.Rotate(Vector3.up, 10f * Time.unscaledDeltaTime, Space.World);

            if (!_applierEnsured && targetCamera != null)
            {
                if (targetCamera.GetComponent<CameraRigApplier>() == null)
                {
                    targetCamera.gameObject.AddComponent<CameraRigApplier>();
                }
                _applierEnsured = true;
            }

            float dt = Time.deltaTime;

            var orbit = orbitAction != null && orbitAction.action != null ? orbitAction.action.ReadValue<Vector2>() : Vector2.zero;
            var pan = panAction != null && panAction.action != null ? panAction.action.ReadValue<Vector2>() : Vector2.zero;
            float zoom = zoomAction != null && zoomAction.action != null ? zoomAction.action.ReadValue<float>() : 0f;

            var orbitInput = orbitSemantics;
            if (orbitInput == Vector2InputSemantics.Auto)
            {
                orbitInput = (Mathf.Abs(orbit.x) > 1.5f || Mathf.Abs(orbit.y) > 1.5f)
                    ? Vector2InputSemantics.PointerDelta
                    : Vector2InputSemantics.NormalizedAxis;
            }

            if (orbitInput == Vector2InputSemantics.PointerDelta)
            {
                yawDegrees += orbit.x * orbitDegreesPerPixel;
                pitchDegrees = Mathf.Clamp(pitchDegrees + orbit.y * orbitDegreesPerPixel, minPitch, maxPitch);
            }
            else
            {
                yawDegrees += orbit.x * orbitDegreesPerSecond * dt;
                pitchDegrees = Mathf.Clamp(pitchDegrees + orbit.y * orbitDegreesPerSecond * dt, minPitch, maxPitch);
            }

            var zoomInput = zoomSemantics;
            if (zoomInput == FloatInputSemantics.Auto)
            {
                zoomInput = Mathf.Abs(zoom) > 5f ? FloatInputSemantics.ScrollDelta : FloatInputSemantics.NormalizedAxis;
            }

            if (zoomInput == FloatInputSemantics.ScrollDelta)
            {
                float unitsPerNotch = Mathf.Abs(scrollUnitsPerNotch) > 1e-4f ? scrollUnitsPerNotch : 120f;
                float notches = zoom / unitsPerNotch;
                distance = Mathf.Clamp(distance - notches * zoomUnitsPerNotch, minDistance, maxDistance);
            }
            else
            {
                distance = Mathf.Clamp(distance - zoom * zoomUnitsPerSecond * dt, minDistance, maxDistance);
            }

            var yawRotation = Quaternion.Euler(0f, yawDegrees, 0f);
            var right = yawRotation * Vector3.right;
            var forward = yawRotation * Vector3.forward;

            var panInput = panSemantics;
            if (panInput == Vector2InputSemantics.Auto)
            {
                panInput = (Mathf.Abs(pan.x) > 1.5f || Mathf.Abs(pan.y) > 1.5f)
                    ? Vector2InputSemantics.PointerDelta
                    : Vector2InputSemantics.NormalizedAxis;
            }

            if (panInput == Vector2InputSemantics.PointerDelta)
            {
                focusPoint += (right * pan.x + forward * pan.y) * panUnitsPerPixel;
            }
            else
            {
                focusPoint += (right * pan.x + forward * pan.y) * (panUnitsPerSecond * dt);
            }

            ConsumeCameraRequests();
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
                        focusPoint = new Vector3(req.WorldPosition.x, req.WorldPosition.y, req.WorldPosition.z);
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
                            yawDegrees = euler.y;
                            pitchDegrees = Mathf.Clamp(euler.x, minPitch, maxPitch);

                            var camPos = new Vector3(req.BookmarkPosition.x, req.BookmarkPosition.y, req.BookmarkPosition.z);
                            focusPoint = camPos + (rot * Vector3.forward * distance);
                            requests.RemoveAt(i);
                        }
                        break;
                }
            }
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

        private void LateUpdate()
        {
            var state = new CameraRigState
            {
                Focus = focusPoint,
                Pitch = pitchDegrees,
                Yaw = yawDegrees,
                Roll = 0f,
                Distance = distance,
                Mode = CameraRigMode.Orbit,
                PerspectiveMode = true,
                FieldOfView = targetCamera != null ? targetCamera.fieldOfView : 60f,
                RigType = CameraRigType.Space4X
            };

            CameraRigService.Publish(state);
        }
    }
}
