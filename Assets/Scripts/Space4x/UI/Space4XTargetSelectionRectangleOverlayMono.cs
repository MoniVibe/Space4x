using PureDOTS.Runtime.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Space4X.UI
{
    /// <summary>
    /// Lightweight drag-rectangle overlay for manual multi-target lock (modes 1/2).
    /// </summary>
    [DefaultExecutionOrder(-884)]
    [DisallowMultipleComponent]
    public sealed class Space4XTargetSelectionRectangleOverlayMono : MonoBehaviour
    {
        [SerializeField] private bool visible = true;
        [SerializeField] private bool requireShiftModifier = true;
        [SerializeField] private float dragThresholdPixels = 10f;
        [SerializeField] private Color fillColor = new Color(0.15f, 0.62f, 0.95f, 0.14f);
        [SerializeField] private Color borderColor = new Color(0.46f, 0.86f, 1f, 0.92f);
        [SerializeField] private float borderThickness = 1f;

        private static Texture2D _pixel;

        private bool _pointerDown;
        private bool _dragging;
        private Vector2 _dragStart;
        private Vector2 _dragCurrent;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Application.isBatchMode || !RuntimeMode.IsRenderingEnabled)
            {
                return;
            }

            if (FindFirstObjectByType<Space4XTargetSelectionRectangleOverlayMono>() != null)
            {
                return;
            }

            var go = new GameObject("Space4X Target Selection Rectangle Overlay");
            DontDestroyOnLoad(go);
            go.AddComponent<Space4XTargetSelectionRectangleOverlayMono>();
        }

        private void OnEnable()
        {
            Space4XTargetSelectionRectangleOverlay.Reset();
            _pointerDown = false;
            _dragging = false;
            _dragStart = Vector2.zero;
            _dragCurrent = Vector2.zero;
        }

        private void OnDisable()
        {
            Space4XTargetSelectionRectangleOverlay.Reset();
        }

        private void Update()
        {
            if (!RuntimeMode.IsRenderingEnabled || !IsManualSelectionMode())
            {
                ResetDrag();
                return;
            }

            var mouse = Mouse.current;
            if (mouse == null)
            {
                ResetDrag();
                return;
            }

            var keyboard = Keyboard.current;
            var shiftHeld = keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
            var ctrlHeld = keyboard != null && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed);
            var canStart = !requireShiftModifier || shiftHeld;

            if (mouse.leftButton.wasPressedThisFrame && canStart && !IsPointerOverUi())
            {
                _pointerDown = true;
                _dragging = false;
                _dragStart = mouse.position.ReadValue();
                _dragCurrent = _dragStart;
            }

            if (_pointerDown && mouse.leftButton.isPressed)
            {
                _dragCurrent = mouse.position.ReadValue();
                if (!_dragging)
                {
                    var threshold = Mathf.Max(0f, dragThresholdPixels);
                    _dragging = (_dragCurrent - _dragStart).sqrMagnitude >= (threshold * threshold);
                }
            }

            if (_pointerDown && mouse.leftButton.wasReleasedThisFrame)
            {
                if (_dragging)
                {
                    var rect = ToScreenRect(_dragStart, _dragCurrent);
                    Space4XTargetSelectionRectangleOverlay.SetPendingSelection(rect, ctrlHeld);
                }

                ResetDrag();
            }

            Space4XTargetSelectionRectangleOverlay.IsPointerCaptureActive = _pointerDown;
            Space4XTargetSelectionRectangleOverlay.IsSelectionDragActive = _dragging;
        }

        private void OnGUI()
        {
            if (!visible || !_dragging || !IsManualSelectionMode())
            {
                return;
            }

            EnsurePixel();
            var rect = ToGuiRect(_dragStart, _dragCurrent);
            if (rect.width < 1f || rect.height < 1f)
            {
                return;
            }

            DrawRect(rect, fillColor);
            DrawBorder(rect, borderColor, Mathf.Max(1f, borderThickness));
        }

        private static bool IsPointerOverUi()
        {
            var eventSystem = EventSystem.current;
            return eventSystem != null && eventSystem.IsPointerOverGameObject();
        }

        private static bool IsManualSelectionMode()
        {
            var mode = Space4XControlModeState.CurrentMode;
            return mode == Space4XControlMode.CursorOrient || mode == Space4XControlMode.CruiseLook;
        }

        private void ResetDrag()
        {
            _pointerDown = false;
            _dragging = false;
            Space4XTargetSelectionRectangleOverlay.IsSelectionDragActive = false;
            Space4XTargetSelectionRectangleOverlay.IsPointerCaptureActive = false;
        }

        private static Rect ToScreenRect(Vector2 start, Vector2 end)
        {
            var minX = Mathf.Min(start.x, end.x);
            var maxX = Mathf.Max(start.x, end.x);
            var minY = Mathf.Min(start.y, end.y);
            var maxY = Mathf.Max(start.y, end.y);
            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        private static Rect ToGuiRect(Vector2 start, Vector2 end)
        {
            var screenRect = ToScreenRect(start, end);
            return new Rect(
                screenRect.xMin,
                Screen.height - screenRect.yMax,
                screenRect.width,
                screenRect.height);
        }

        private static void EnsurePixel()
        {
            if (_pixel != null)
            {
                return;
            }

            _pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            _pixel.SetPixel(0, 0, Color.white);
            _pixel.Apply();
        }

        private static void DrawRect(Rect rect, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, _pixel);
            GUI.color = previous;
        }

        private static void DrawBorder(Rect rect, Color color, float thickness)
        {
            DrawRect(new Rect(rect.xMin, rect.yMin, rect.width, thickness), color);
            DrawRect(new Rect(rect.xMin, rect.yMax - thickness, rect.width, thickness), color);
            DrawRect(new Rect(rect.xMin, rect.yMin, thickness, rect.height), color);
            DrawRect(new Rect(rect.xMax - thickness, rect.yMin, thickness, rect.height), color);
        }
    }
}
