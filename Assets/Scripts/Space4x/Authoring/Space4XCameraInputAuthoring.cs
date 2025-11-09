using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Space4X.Registry
{
    /// <summary>
    /// Pure DOTS authoring component that bakes camera input configuration.
    /// No MonoBehaviour update - purely for conversion to DOTS.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Space4XCameraInputAuthoring : MonoBehaviour
    {
        [Header("Input Actions")]
        [SerializeField]
        private InputActionAsset inputActions;

        [Header("Enable Flags")]
        [SerializeField]
        private bool enablePan = true;

        [SerializeField]
        private bool enableZoom = true;

        [SerializeField]
        private bool enableVerticalMove = true;

        [SerializeField]
        private bool enableRotation = true;

        [SerializeField]
        [Tooltip("If true, rotation requires right mouse button to be held. If false, rotation works with mouse movement alone.")]
        private bool requireRightMouseForRotation = true;

        public InputActionAsset InputActions => inputActions;

        internal Space4XCameraInputConfig BuildConfigData()
        {
            return new Space4XCameraInputConfig
            {
                EnablePan = enablePan,
                EnableZoom = enableZoom,
                EnableVerticalMove = enableVerticalMove,
                EnableRotation = enableRotation,
                RequireRightMouseForRotation = requireRightMouseForRotation
            };
        }

        private sealed class Baker : Unity.Entities.Baker<Space4XCameraInputAuthoring>
        {
            public override void Bake(Space4XCameraInputAuthoring authoring)
            {
                if (authoring.inputActions == null)
                {
                    Debug.LogWarning("Space4XCameraInputAuthoring: InputActionAsset is not assigned. Camera input will not work.", authoring);
                    return;
                }

                var entity = GetEntity(TransformUsageFlags.None);
                var config = authoring.BuildConfigData();
                
                // AddComponent will replace if it already exists
                AddComponent(entity, config);
            }
        }
    }
}

