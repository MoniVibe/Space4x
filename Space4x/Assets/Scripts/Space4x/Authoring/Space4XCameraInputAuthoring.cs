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
        private bool enableRotation = false;

        public InputActionAsset InputActions => inputActions;

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
                if (HasComponent<Space4XCameraInputConfig>(entity))
                {
                    SetComponent(entity, new Space4XCameraInputConfig
                    {
                        EnablePan = authoring.enablePan,
                        EnableZoom = authoring.enableZoom,
                        EnableRotation = authoring.enableRotation
                    });
                }
                else
                {
                    AddComponent(entity, new Space4XCameraInputConfig
                    {
                        EnablePan = authoring.enablePan,
                        EnableZoom = authoring.enableZoom,
                        EnableRotation = authoring.enableRotation
                    });
                }
            }
        }
    }
}

