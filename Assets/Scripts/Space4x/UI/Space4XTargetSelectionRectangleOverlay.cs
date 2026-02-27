using UnityEngine;

namespace Space4X.UI
{
    /// <summary>
    /// Shared drag-rectangle state for manual multi-target selection (modes 1/2).
    /// </summary>
    public static class Space4XTargetSelectionRectangleOverlay
    {
        public static bool IsSelectionDragActive { get; internal set; }
        public static bool IsPointerCaptureActive { get; internal set; }

        private static bool _hasPendingSelection;
        private static Rect _pendingScreenRect;
        private static bool _pendingAppend;

        public static bool TryConsumePendingSelection(out Rect screenRect, out bool append)
        {
            screenRect = default;
            append = false;
            if (!_hasPendingSelection)
            {
                return false;
            }

            _hasPendingSelection = false;
            screenRect = _pendingScreenRect;
            append = _pendingAppend;
            return true;
        }

        internal static void SetPendingSelection(in Rect screenRect, bool append)
        {
            _pendingScreenRect = screenRect;
            _pendingAppend = append;
            _hasPendingSelection = screenRect.width >= 1f && screenRect.height >= 1f;
        }

        internal static void Reset()
        {
            IsSelectionDragActive = false;
            IsPointerCaptureActive = false;
            _hasPendingSelection = false;
            _pendingScreenRect = default;
            _pendingAppend = false;
        }
    }
}
