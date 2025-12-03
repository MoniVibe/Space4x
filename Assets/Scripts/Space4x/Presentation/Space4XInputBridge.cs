using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Space4X.Presentation
{
    public class Space4XInputBridge : MonoBehaviour
    {
        public InputActionAsset InputActions;
        
        private InputAction _panAction;
        private InputAction _zoomAction;
        private InputAction _rotateAction;
        private InputAction _selectAction;
        private InputAction _toggleDebugAction;

        private void OnEnable()
        {
            if (InputActions == null) return;

            var map = InputActions.FindActionMap("Camera"); // Assuming "Camera" map exists
            if (map != null)
            {
                _panAction = map.FindAction("Pan");
                _zoomAction = map.FindAction("Zoom");
                _rotateAction = map.FindAction("Rotate");
                _selectAction = map.FindAction("Select"); // Or "Click"
                _toggleDebugAction = map.FindAction("ToggleDebug");

                map.Enable();
            }
        }

        private void Update()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;

            var em = world.EntityManager;

            // Update CameraInput singleton
            if (!em.CreateEntityQuery(typeof(CameraInput)).IsEmptyIgnoreFilter)
            {
                var entity = em.CreateEntityQuery(typeof(CameraInput)).GetSingletonEntity();
                var input = em.GetComponentData<CameraInput>(entity);

                input.Pan = _panAction?.ReadValue<Vector2>() ?? Vector2.zero;
                input.Zoom = _zoomAction?.ReadValue<float>() ?? 0f;
                input.Rotate = _rotateAction?.ReadValue<Vector2>() ?? Vector2.zero;
                
                em.SetComponentData(entity, input);
            }

            // Update SelectionInput singleton
            if (!em.CreateEntityQuery(typeof(SelectionInput)).IsEmptyIgnoreFilter)
            {
                var entity = em.CreateEntityQuery(typeof(SelectionInput)).GetSingletonEntity();
                var input = em.GetComponentData<SelectionInput>(entity);

                input.IsSelectPressed = _selectAction?.WasPressedThisFrame() ?? false;
                input.PointerPosition = Mouse.current?.position.ReadValue() ?? Vector2.zero;

                em.SetComponentData(entity, input);
            }
            
            // Update DebugOverlayConfig singleton (if toggle pressed)
            if (_toggleDebugAction != null && _toggleDebugAction.WasPressedThisFrame())
            {
                if (!em.CreateEntityQuery(typeof(DebugOverlayConfig)).IsEmptyIgnoreFilter)
                {
                    var entity = em.CreateEntityQuery(typeof(DebugOverlayConfig)).GetSingletonEntity();
                    var config = em.GetComponentData<DebugOverlayConfig>(entity);
                    config.ShowMetrics = !config.ShowMetrics; // Toggle metrics for now
                    em.SetComponentData(entity, config);
                }
            }
        }
    }

    public struct CameraInput : IComponentData
    {
        public float2 Pan;
        public float Zoom;
        public float2 Rotate;
    }

    public struct SelectionInput : IComponentData
    {
        public bool IsSelectPressed;
        public float2 PointerPosition;
        public float2 ClickPosition;
        public bool ClickPressed;
        public bool ClickHeld;
        public float2 BoxStart;
        public float2 BoxEnd;
        public bool BoxActive;
        public bool ShiftHeld;
        public bool DeselectRequested;
    }
    
    public struct CommandInput : IComponentData
    {
        public bool CycleFleetsPressed;
        public bool ToggleOverlaysPressed;
        public bool DebugViewPressed;
        public bool IssueMoveCommand;
        public bool IssueAttackCommand;
        public bool IssueMineCommand;
        public bool CancelCommand;
        public float3 CommandTargetPosition;
        public Entity CommandTargetEntity;
    }
    
    public struct SelectionState : IComponentData
    {
        public int SelectedCount;
        public Entity PrimarySelected;
        public SelectionType Type;
    }
    
    public enum SelectionType
    {
        None,
        Carrier,
        Craft,
        Asteroid,
        Fleet
    }
    
    public struct DebugOverlayConfig : IComponentData
    {
        public bool ShowResourceFields;
        public bool ShowFactionZones;
        public bool ShowDebugPaths;
        public bool ShowLODVisualization;
        public bool ShowMetrics;
        public bool ShowInspector;
        public bool ShowLogisticsOverlay;
    }
}
