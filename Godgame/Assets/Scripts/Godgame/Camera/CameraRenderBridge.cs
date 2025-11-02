using Godgame.Camera;
using Unity.Entities;
using UnityEngine;

namespace Godgame.Camera
{
    /// <summary>
    /// MonoBehaviour bridge that reads CameraTransform singleton and updates Unity Camera GameObject transform.
    /// Syncs DOTS camera state to Unity rendering.
    /// </summary>
    [RequireComponent(typeof(UnityEngine.Camera))]
    public class CameraRenderBridge : MonoBehaviour
    {
        private UnityEngine.Camera _unityCamera;
        private World _world;
        private EntityQuery _cameraQuery;
        private bool _queryInitialized;

        private void Start()
        {
            _unityCamera = GetComponent<UnityEngine.Camera>();
            
            // Find the default world
            _world = World.DefaultGameObjectInjectionWorld;
            
            if (_world == null)
            {
                Debug.LogWarning("[CameraRenderBridge] No default world found. Camera will not sync.");
                enabled = false;
                return;
            }

            // Create query for CameraTransform singleton
            InitializeQuery();
        }

        private void InitializeQuery()
        {
            if (_world == null || !_world.IsCreated)
            {
                return;
            }

            var entityManager = _world.EntityManager;
            _cameraQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<CameraTransform>());
            _queryInitialized = true;
        }

        private void OnDestroy()
        {
            if (_queryInitialized && _world != null && _world.IsCreated)
            {
                _cameraQuery.Dispose();
            }
        }

        private void LateUpdate()
        {
            if (_world == null || !_world.IsCreated)
            {
                return;
            }

            // Reinitialize query if world was recreated
            if (!_queryInitialized)
            {
                InitializeQuery();
            }

            if (!_queryInitialized || _cameraQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var entityManager = _world.EntityManager;
            var cameraEntity = _cameraQuery.GetSingletonEntity();
            var cameraTransform = entityManager.GetComponentData<CameraTransform>(cameraEntity);

            // Update Unity Camera transform to match DOTS camera state
            transform.position = cameraTransform.Position;
            transform.rotation = cameraTransform.Rotation;
        }
    }
}

