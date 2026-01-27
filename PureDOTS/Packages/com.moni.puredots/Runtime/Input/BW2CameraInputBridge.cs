using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PureDOTS.Input
{
    /// <summary>
    /// Captures high-frequency BW2 camera input once per frame and exposes a deterministic snapshot.
    /// </summary>
    [DefaultExecutionOrder(-10050)]
    public sealed class BW2CameraInputBridge : MonoBehaviour
    {
        public struct Snapshot
        {
            public Vector2 PointerPosition;
            public Vector2 PointerDelta;
            public float Scroll;
            public bool LeftHeld;
            public bool LeftPressed;
            public bool LeftReleased;
            public bool RightHeld;
            public bool RightPressed;
            public bool RightReleased;
            public bool MiddleHeld;
            public bool MiddlePressed;
            public bool MiddleReleased;
            public bool EdgeLeft;
            public bool EdgeRight;
            public bool EdgeTop;
            public bool EdgeBottom;
            public int Frame;
        }

        private static Snapshot s_snapshot;
        private static bool s_hasSnapshot;
        private static bool s_prevLeft;
        private static bool s_prevRight;
        private static bool s_prevMiddle;

        public static bool TryGetSnapshot(out Snapshot snapshot)
        {
            snapshot = s_snapshot;
            return s_hasSnapshot;
        }

        private void OnDisable()
        {
            s_hasSnapshot = false;
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse == null)
            {
                s_hasSnapshot = false;
                return;
            }

            var pointer = mouse.position.ReadValue();
            var delta = mouse.delta.ReadValue();
            var scroll = mouse.scroll.ReadValue().y;

            bool left = mouse.leftButton.isPressed;
            bool right = mouse.rightButton.isPressed;
            bool middle = mouse.middleButton.isPressed;

            var snapshot = new Snapshot
            {
                PointerPosition = pointer,
                PointerDelta = delta,
                Scroll = scroll,
                LeftHeld = left,
                LeftPressed = left && !s_prevLeft,
                LeftReleased = !left && s_prevLeft,
                RightHeld = right,
                RightPressed = right && !s_prevRight,
                RightReleased = !right && s_prevRight,
                MiddleHeld = middle,
                MiddlePressed = middle && !s_prevMiddle,
                MiddleReleased = !middle && s_prevMiddle,
                Frame = UnityEngine.Time.frameCount
            };

            const float edgeThickness = 12f;
            float width = Screen.width <= 0 ? 1f : Screen.width;
            float height = Screen.height <= 0 ? 1f : Screen.height;
            snapshot.EdgeLeft = pointer.x <= edgeThickness;
            snapshot.EdgeRight = pointer.x >= width - edgeThickness;
            snapshot.EdgeBottom = pointer.y <= edgeThickness;
            snapshot.EdgeTop = pointer.y >= height - edgeThickness;

            s_snapshot = snapshot;
            s_prevLeft = left;
            s_prevRight = right;
            s_prevMiddle = middle;
            s_hasSnapshot = true;
#else
            s_hasSnapshot = false;
#endif
        }
    }
}
