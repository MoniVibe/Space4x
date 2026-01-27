using UnityEngine;

namespace PureDOTS.Runtime.Camera
{
    /// <summary>
    /// Context information for right-mouse-button interactions, including pointer position,
    /// raycast hit information, and interaction flags. Used by camera controllers and input handlers.
    /// </summary>
    public readonly struct RmbContext
    {
        public readonly Vector2 PointerScreenPosition;
        public readonly Ray PointerRay;
        public readonly bool PointerOverUI;
        public readonly bool HasWorldHit;
        public readonly RaycastHit WorldHit;
        public readonly Vector3 WorldPoint;
        public readonly int WorldLayer;
        public readonly float DeltaTime;
        public readonly float UnscaledDeltaTime;
        public readonly bool HandHasCargo;
        public readonly bool HitStorehouse;
        public readonly bool HitPile;
        public readonly bool HitDraggable;
        public readonly bool HitGround;

        public RmbContext(
            Vector2 pointerScreenPosition,
            Ray pointerRay,
            bool pointerOverUI,
            bool hasWorldHit,
            RaycastHit worldHit,
            Vector3 worldPoint,
            int worldLayer,
            float deltaTime,
            float unscaledDeltaTime,
            bool handHasCargo,
            bool hitStorehouse,
            bool hitPile,
            bool hitDraggable,
            bool hitGround)
        {
            PointerScreenPosition = pointerScreenPosition;
            PointerRay = pointerRay;
            PointerOverUI = pointerOverUI;
            HasWorldHit = hasWorldHit;
            WorldHit = worldHit;
            WorldPoint = worldPoint;
            WorldLayer = worldLayer;
            DeltaTime = deltaTime;
            UnscaledDeltaTime = unscaledDeltaTime;
            HandHasCargo = handHasCargo;
            HitStorehouse = hitStorehouse;
            HitPile = hitPile;
            HitDraggable = hitDraggable;
            HitGround = hitGround;
        }
    }
}

