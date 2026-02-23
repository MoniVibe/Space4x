using PureDOTS.Runtime.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Space4X.UI
{
    /// <summary>
    /// Lightweight RTS drag-rectangle overlay for mode 3.
    /// </summary>
    [DefaultExecutionOrder(-885)]
    [DisallowMultipleComponent]
    public sealed class Space4XRtsSelectionRectangleOverlayMono : MonoBehaviour
    {
        [SerializeField] private bool visible = true;
        [SerializeField] private float dragThresholdPixels = 8f;
        [SerializeField] private Color fillColor = new Color(0.25f, 0.85f, 1f, 0.18f);
        [SerializeField] private Color borderColor = new Color(0.42f, 0.95f, 1f, 0.92f);
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

            if (FindFirstObjectByType<Space4XRtsSelectionRectangleOverlayMono>() != null)
            {
                return;
            }

            var go = new GameObject("Space4X RTS Selection Rectangle Overlay");
            DontDestroyOnLoad(go);
            go.AddComponent<Space4XRtsSelectionRectangleOverlayMono>();
        }

        private void OnEnable()
        {
            Space4XRtsSelectionRectangleOverlay.IsSelectionDragActive = false;
            _pointerDown = false;
            _dragging = false;
            _dragStart = Vector2.zero;
            _dragCurrent = Vector2.zero;
        }

        private void OnDisable()
        {
            Space4XRtsSelectionRectangleOverlay.IsSelectionDragActive = false;
        }

        private void Update()
        {
            if (!RuntimeMode.IsRenderingEnabled || Space4XControlModeState.CurrentMode != Space4XControlMode.Rts)
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

            if (mouse.leftButton.wasPressedThisFrame && !IsPointerOverUi())
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
                ResetDrag();
            }

            Space4XRtsSelectionRectangleOverlay.IsSelectionDragActive = _dragging;
        }

        private void OnGUI()
        {
            if (!visible || !_dragging || Space4XControlModeState.CurrentMode != Space4XControlMode.Rts)
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

        private void ResetDrag()
        {
            _pointerDown = false;
            _dragging = false;
            Space4XRtsSelectionRectangleOverlay.IsSelectionDragActive = false;
        }

        private static Rect ToGuiRect(Vector2 start, Vector2 end)
        {
            float minX = Mathf.Min(start.x, end.x);
            float maxX = Mathf.Max(start.x, end.x);
            float minY = Mathf.Min(start.y, end.y);
            float maxY = Mathf.Max(start.y, end.y);

            // Input mouse uses bottom-left origin, IMGUI uses top-left.
            float guiY = Screen.height - maxY;
            return new Rect(minX, guiY, maxX - minX, maxY - minY);
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
