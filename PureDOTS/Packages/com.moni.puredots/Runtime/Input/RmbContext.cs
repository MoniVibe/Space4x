using PureDOTS.Runtime.Camera;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Input
{
    /// <summary>
    /// Right-mouse-button interaction context containing camera state, screen position, and world hit info.
    /// </summary>
    public readonly struct RmbContext
    {
        public readonly CameraRigState CameraState;
        public readonly bool HasCameraState;

        public readonly float2 PointerPosition;
        public readonly Ray PointerRay;
        public readonly bool PointerOverUI;
        public readonly bool HasWorldHit;
        public readonly RaycastHit WorldHit;
        public readonly float3 WorldPoint;
        public readonly int WorldLayer;
        public readonly float HitDistance;
        public readonly float DeltaTime;
        public readonly float UnscaledDeltaTime;
        public readonly bool HandHasCargo;
        public readonly bool HitStorehouse;
        public readonly bool HitPile;
        public readonly bool HitDraggable;
        public readonly bool HitGround;
        public readonly Entity HitEntity;

        // Constructor used by router (full hit info)
        public RmbContext(
            float2 pointerPosition,
            Ray pointerRay,
            bool pointerOverUI,
            bool hasWorldHit,
            RaycastHit worldHit,
            float3 worldPoint,
            int worldLayer,
            float deltaTime,
            float unscaledDeltaTime,
            bool handHasCargo,
            bool hitStorehouse,
            bool hitPile,
            bool hitDraggable,
            bool hitGround,
            Entity hitEntity = default)
        {
            HasCameraState = false;
            CameraState = default;
            PointerPosition = pointerPosition;
            PointerRay = pointerRay;
            PointerOverUI = pointerOverUI;
            HasWorldHit = hasWorldHit;
            WorldHit = worldHit;
            WorldPoint = worldPoint;
            WorldLayer = worldLayer;
            HitDistance = hasWorldHit ? worldHit.distance : 0f;
            DeltaTime = deltaTime;
            UnscaledDeltaTime = unscaledDeltaTime;
            HandHasCargo = handHasCargo;
            HitStorehouse = hitStorehouse;
            HitPile = hitPile;
            HitDraggable = hitDraggable;
            HitGround = hitGround;
            HitEntity = hitEntity;
        }

        // Constructor used by camera controllers (camera state + basic hit info)
        public RmbContext(
            in CameraRigState cameraState,
            float2 pointerPosition,
            Ray pointerRay,
            bool hasWorldHit,
            Entity hitEntity,
            float3 worldPoint,
            float hitDistance)
        {
            HasCameraState = true;
            CameraState = cameraState;
            PointerPosition = pointerPosition;
            PointerRay = pointerRay;
            PointerOverUI = false;
            HasWorldHit = hasWorldHit;
            WorldHit = default;
            WorldPoint = worldPoint;
            WorldLayer = -1;
            HitDistance = hitDistance;
            DeltaTime = UnityEngine.Time.deltaTime;
            UnscaledDeltaTime = UnityEngine.Time.unscaledDeltaTime;
            HandHasCargo = false;
            HitStorehouse = false;
            HitPile = false;
            HitDraggable = false;
            HitGround = hasWorldHit;
            HitEntity = hitEntity;
        }
    }
}

