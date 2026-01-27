using PureDOTS.Input;
using PureDOTS.Runtime.Camera;
using PureDOTS.Runtime.Components;
#if GODGAME
using Godgame.Runtime;
#endif
#if DEVTOOLS_ENABLED
using PureDOTS.Runtime.Devtools;
#endif
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Systems.Input
{
    /// <summary>
    /// Tag component marking an ECS camera entity that should receive
    /// the copied input state. Host games can add this tag to whichever
    /// camera rig entity should be driven by PureDOTS input.
    /// </summary>
    public struct CameraTag : IComponentData
    {
        // Marker component – no fields.
    }

    /// <summary>
    /// Copies input snapshots from Mono bridge to ECS once per DOTS tick.
    /// Runs in CameraInputSystemGroup (before SimulationSystemGroup) to ensure input is processed
    /// with highest priority for instant camera/control response.
    /// Handles multi-tick catch-up by clamping deltas if multiple ticks occur in one frame.
    /// 
    /// Note: This system cannot be Burst-compiled because it accesses managed InputSnapshotBridge.
    /// Contract: Camera systems read `CameraInputState` + `CameraInputEdge` from ECS; any game-specific input
    /// pipeline can feed those components (this bridge copies from the Mono HandCameraInputRouter).
    /// ISystem structs must be unmanaged, so we cannot cache the bridge reference as a field.
    /// Using Object.FindFirstObjectByType is acceptable here since the system is non-Burst anyway.
    /// </summary>
    [UpdateInGroup(typeof(CameraInputSystemGroup))]
    public partial struct CopyInputToEcsSystem : ISystem
    {
        private Entity _cursorCacheEntity;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            
#if DEVTOOLS_ENABLED
            // Create cursor hit cache entity once in OnCreate
            _cursorCacheEntity = state.EntityManager.CreateEntity(typeof(CursorHitCache));
#endif
        }

        public void OnUpdate(ref SystemState state)
        {
            // Find Mono bridge (ISystem structs must be unmanaged, so we can't cache the managed reference)
            // This is acceptable since this system cannot be Burst-compiled anyway due to managed bridge access
            var bridge = Object.FindFirstObjectByType<InputSnapshotBridge>();
            if (bridge == null)
            {
                return; // No bridge found, skip
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint currentTick = timeState.Tick;

            // Find hand and camera entities
            Entity handEntity = Entity.Null;
            Entity cameraEntity = Entity.Null;
            Entity timeControlEntity = Entity.Null;

#if GODGAME
            // DivineHandTag is game-specific (Godgame) - only query if available
            using (var handQuery = state.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<DivineHandTag>()))
            {
                if (!handQuery.IsEmptyIgnoreFilter)
                {
                    handEntity = handQuery.GetSingletonEntity();
                }
            }
#endif

            using (var cameraQuery = state.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<CameraTag>()))
            {
                if (!cameraQuery.IsEmptyIgnoreFilter)
                {
                    cameraEntity = cameraQuery.GetSingletonEntity();
                }
            }

            timeControlEntity = SystemAPI.GetSingletonEntity<TimeControlSingletonTag>();

            // Flush snapshot to ECS
            bridge.FlushSnapshotToEcs(state.EntityManager, handEntity, cameraEntity, timeControlEntity, currentTick);

#if DEVTOOLS_ENABLED
            // Update cursor hit cache for devtools
            UpdateCursorHitCache(ref state, bridge, currentTick);
#endif
        }

#if DEVTOOLS_ENABLED
        private void UpdateCursorHitCache(ref SystemState state, InputSnapshotBridge bridge, uint currentTick)
        {
            if (bridge == null)
            {
                return;
            }

            // Use the entity created in OnCreate
            Entity cacheEntity = _cursorCacheEntity;
            if (!state.EntityManager.Exists(cacheEntity))
            {
                // Entity was destroyed, recreate it (shouldn't happen, but handle gracefully)
                cacheEntity = state.EntityManager.CreateEntity(typeof(CursorHitCache));
                _cursorCacheEntity = cacheEntity;
            }

            // Get raycast hit from bridge
            bridge.GetCursorHit(out var ray, out var hasHit, out var hit, out var modifierKeys);

            var cache = new CursorHitCache
            {
                SampleTick = currentTick,
                RayOrigin = new float3(ray.origin.x, ray.origin.y, ray.origin.z),
                RayDirection = new float3(ray.direction.x, ray.direction.y, ray.direction.z),
                HasHit = hasHit,
                ModifierKeys = modifierKeys
            };

            if (hasHit)
            {
                cache.HitPoint = new float3(hit.point.x, hit.point.y, hit.point.z);
                cache.HitNormal = new float3(hit.normal.x, hit.normal.y, hit.normal.z);
                // Try to get hit entity if it has a collider with a GameObject
                if (hit.collider != null && hit.collider.gameObject != null)
                {
                    // Try to find entity via GameObject conversion (if using hybrid renderer or conversion)
                    // For now, leave as null - would need GameObject-to-Entity mapping
                    cache.HitEntity = Entity.Null;
                }
            }

            state.EntityManager.SetComponentData(cacheEntity, cache);
        }
#endif

        public void OnDestroy(ref SystemState state)
        {
            // No cleanup needed - bridge is found each frame
        }
    }
}
