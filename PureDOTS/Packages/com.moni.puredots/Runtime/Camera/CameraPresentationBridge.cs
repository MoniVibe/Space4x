using PureDOTS.Runtime.Camera;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Runtime.Camera
{
    /// <summary>
    /// Presentation bridge MonoBehaviour that reads CameraState from ECS
    /// and applies visual-only smoothing to the GameObject camera.
    /// This is a reference implementation - game-specific bridges can extend this pattern.
    /// </summary>
    [RequireComponent(typeof(UnityEngine.Camera))]
    [RequireComponent(typeof(CameraRigApplier))]
    public sealed class CameraPresentationBridge : MonoBehaviour
    {
        [Header("Smoothing")]
        [SerializeField] float positionSmoothing = 0.1f;
        [SerializeField] float rotationSmoothing = 0.1f;

        [Header("Configuration")]
        [SerializeField] byte playerId = 0; // Default player
        [SerializeField] CameraRigType rigType = CameraRigType.Godgame;

        private World _world;
        private EntityQuery _cameraQuery;
        private UnityEngine.Camera _targetCamera;
        private bool _queryValid;

        private bool _smoothingInitialized;
        private Vector3 _smoothedFocus;
        private float _smoothedYaw;
        private float _smoothedPitch;
        private float _smoothedDistance;

        void Awake()
        {
            _targetCamera = GetComponent<UnityEngine.Camera>();
            _smoothedFocus = _targetCamera.transform.position;
            var euler = _targetCamera.transform.rotation.eulerAngles;
            _smoothedYaw = euler.y;
            _smoothedPitch = euler.x;
            _smoothedDistance = 0f;
            EnsureWorld();
        }

        void LateUpdate()
        {
            EnsureWorld();
            if (_world == null || !_world.IsCreated || !_queryValid || _cameraQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            Entity cameraEntity;
            try
            {
                // Find camera entity matching PlayerId
                using (var entities = _cameraQuery.ToEntityArray(Allocator.Temp))
                {
                    cameraEntity = Entity.Null;
                    var entityManager = _world.EntityManager;
                    for (int i = 0; i < entities.Length; i++)
                    {
                        if (entityManager.HasComponent<CameraState>(entities[i]))
                        {
                            var state = entityManager.GetComponentData<CameraState>(entities[i]);
                            if (state.PlayerId == playerId)
                            {
                                cameraEntity = entities[i];
                                break;
                            }
                        }
                    }
                }

                if (cameraEntity == Entity.Null)
                {
                    return;
                }

                var cameraState = _world.EntityManager.GetComponentData<CameraState>(cameraEntity);

                // Read authoritative state from ECS
                Vector3 targetFocus = new Vector3(
                    cameraState.PivotPosition.x,
                    cameraState.PivotPosition.y,
                    cameraState.PivotPosition.z);
                float targetYaw = cameraState.Yaw;
                float targetPitch = cameraState.Pitch;
                float targetDistance = cameraState.Distance;

                // Apply visual-only smoothing (presentation layer, not gameplay)
                float deltaTime = UnityEngine.Time.deltaTime;
                float posT = positionSmoothing > 1e-4f ? Mathf.Clamp01(deltaTime / positionSmoothing) : 1f;
                float rotT = rotationSmoothing > 1e-4f ? Mathf.Clamp01(deltaTime / rotationSmoothing) : 1f;

                if (!_smoothingInitialized)
                {
                    _smoothedFocus = targetFocus;
                    _smoothedYaw = targetYaw;
                    _smoothedPitch = targetPitch;
                    _smoothedDistance = targetDistance;
                    _smoothingInitialized = true;
                }
                else
                {
                    _smoothedFocus = Vector3.Lerp(_smoothedFocus, targetFocus, posT);
                    _smoothedYaw = Mathf.LerpAngle(_smoothedYaw, targetYaw, rotT);
                    _smoothedPitch = Mathf.LerpAngle(_smoothedPitch, targetPitch, rotT);
                    _smoothedDistance = Mathf.Lerp(_smoothedDistance, targetDistance, posT);
                }

                CameraRigService.Publish(new CameraRigState
                {
                    Focus = _smoothedFocus,
                    Pitch = _smoothedPitch,
                    Yaw = _smoothedYaw,
                    Roll = 0f,
                    Distance = _smoothedDistance,
                    Mode = CameraRigMode.Orbit,
                    PerspectiveMode = true,
                    FieldOfView = cameraState.FOV,
                    RigType = rigType
                });
            }
            catch
            {
                // Query may be invalid during world teardown
                _queryValid = false;
            }
        }

        private void EnsureWorld()
        {
            if (_world != null && _world.IsCreated)
            {
                return;
            }

            _world = World.DefaultGameObjectInjectionWorld;
            if (_world == null || !_world.IsCreated)
            {
                _queryValid = false;
                return;
            }

            var entityManager = _world.EntityManager;
            _cameraQuery = entityManager.CreateEntityQuery(typeof(CameraState), typeof(CameraTag));
            _queryValid = true;
        }

        void OnDestroy()
        {
            if (_cameraQuery != default)
            {
                _cameraQuery.Dispose();
            }
        }
    }
}




















