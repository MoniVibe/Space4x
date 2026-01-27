using PureDOTS.Input;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Unity.Physics;
using Unity.Physics.Systems;

namespace PureDOTS.Systems.Input
{
    /// <summary>
    /// Processes selection click and box events, updating SelectedTag on selectable entities.
    /// Uses Unity Physics for raycasts (non-Burst) and spatial grid for box queries when available.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    // Removed invalid UpdateAfter: CopyInputToEcsSystem runs in CameraInputSystemGroup; Simulation systems already execute afterward.
    public partial struct SelectionSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RtsInputSingletonTag>();
        }

        public void OnUpdate(ref SystemState state)
        {
            Entity rtsInputEntity = SystemAPI.GetSingletonEntity<RtsInputSingletonTag>();

            if (!state.EntityManager.HasBuffer<SelectionClickEvent>(rtsInputEntity) ||
                !state.EntityManager.HasBuffer<SelectionBoxEvent>(rtsInputEntity))
            {
                return;
            }

            var clickBuffer = state.EntityManager.GetBuffer<SelectionClickEvent>(rtsInputEntity);
            var boxBuffer = state.EntityManager.GetBuffer<SelectionBoxEvent>(rtsInputEntity);
            var clickEvents = new NativeArray<SelectionClickEvent>(clickBuffer.Length, Allocator.Temp);
            var boxEvents = new NativeArray<SelectionBoxEvent>(boxBuffer.Length, Allocator.Temp);
            clickBuffer.AsNativeArray().CopyTo(clickEvents);
            boxBuffer.AsNativeArray().CopyTo(boxEvents);

            // Process click events
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            for (int i = 0; i < clickEvents.Length; i++)
            {
                var clickEvent = clickEvents[i];
                ProcessSelectionClick(ref state, ref ecb, clickEvent);
            }

            // Process box events
            for (int i = 0; i < boxEvents.Length; i++)
            {
                var boxEvent = boxEvents[i];
                ProcessSelectionBox(ref state, ref ecb, boxEvent);
            }

            // Clear buffers after processing
            clickBuffer.Clear();
            boxBuffer.Clear();
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            clickEvents.Dispose();
            boxEvents.Dispose();
        }

        private void ProcessSelectionClick(ref SystemState state, ref EntityCommandBuffer ecb, SelectionClickEvent clickEvent)
        {
            // Get camera for raycast
            Camera camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            // Raycast from screen position
            Vector3 screenPos = new Vector3(clickEvent.ScreenPos.x, clickEvent.ScreenPos.y, 0f);
            UnityEngine.Ray ray = camera.ScreenPointToRay(screenPos);

            Entity hitEntity = Entity.Null;
            hitEntity = RaycastForEntity(ref state, ray, 800f);

            // Fallback: nearest selectable under cursor within small radius
            if (hitEntity == Entity.Null)
            {
                hitEntity = FindNearestSelectableOnScreen(ref state, camera, screenPos, 32f, clickEvent.PlayerId);
            }

            // If no entity hit, clear selection (classic RTS behavior)
            if (hitEntity == Entity.Null)
            {
                if (clickEvent.Mode == SelectionClickMode.Replace)
                {
                    ClearAllSelections(ref state, ref ecb, clickEvent.PlayerId);
                }
                return;
            }

            // Check if hit entity is selectable and owned by player
            if (!state.EntityManager.HasComponent<SelectableTag>(hitEntity))
            {
                return;
            }

            byte ownerId = 0;
            if (state.EntityManager.HasComponent<SelectionOwner>(hitEntity))
            {
                ownerId = state.EntityManager.GetComponentData<SelectionOwner>(hitEntity).PlayerId;
            }

            if (ownerId != clickEvent.PlayerId)
            {
                return; // Not owned by this player
            }

            // Apply selection mode
            bool isSelected = state.EntityManager.HasComponent<SelectedTag>(hitEntity);

            if (clickEvent.Mode == SelectionClickMode.Replace)
            {
                ClearAllSelections(ref state, ref ecb, clickEvent.PlayerId);
                if (!isSelected)
                {
                    ecb.AddComponent<SelectedTag>(hitEntity);
                }
            }
            else if (clickEvent.Mode == SelectionClickMode.Toggle)
            {
                if (isSelected)
                {
                    ecb.RemoveComponent<SelectedTag>(hitEntity);
                }
                else
                {
                    ecb.AddComponent<SelectedTag>(hitEntity);
                }
            }
        }

        private Entity RaycastForEntity(ref SystemState state, UnityEngine.Ray ray, float maxDistance)
        {
            // Prefer Unity.Physics raycast (ECS entities)
            if (SystemAPI.TryGetSingleton<PhysicsWorldSingleton>(out var physicsWorld))
            {
                var input = new RaycastInput
                {
                    Start = ray.origin,
                    End = ray.origin + ray.direction * maxDistance,
                    Filter = CollisionFilter.Default
                };

                if (physicsWorld.CastRay(input, out var hit))
                {
                    return hit.Entity;
                }
            }

            // Fallback: classic Physics raycast and optional Mono bridge
            if (UnityEngine.Physics.Raycast(ray, out UnityEngine.RaycastHit hit3d, maxDistance))
            {
                var bridge = hit3d.collider.GetComponent<IEntityBridge>();
                if (bridge != null && bridge.TryGetEntity(out var bridged))
                {
                    return bridged;
                }
            }

            return Entity.Null;
        }

        private Entity FindNearestSelectableOnScreen(ref SystemState state, Camera camera, Vector3 screenPos, float maxPixelDistance, byte playerId)
        {
            Entity best = Entity.Null;
            float bestSqr = maxPixelDistance * maxPixelDistance;

            foreach (var (_, entity) in SystemAPI.Query<SelectableTag>().WithEntityAccess())
            {
                if (state.EntityManager.HasComponent<SelectionOwner>(entity))
                {
                    var ownerId = state.EntityManager.GetComponentData<SelectionOwner>(entity).PlayerId;
                    if (ownerId != playerId)
                    {
                        continue;
                    }
                }

                if (!state.EntityManager.HasComponent<Unity.Transforms.LocalTransform>(entity))
                {
                    continue;
                }

                var transform = state.EntityManager.GetComponentData<Unity.Transforms.LocalTransform>(entity);
                Vector3 worldPos = new Vector3(transform.Position.x, transform.Position.y, transform.Position.z);
                Vector3 projected = camera.WorldToScreenPoint(worldPos);
                if (projected.z < 0f)
                {
                    continue; // behind camera
                }

                float sqrDist = (new Vector2(projected.x, projected.y) - new Vector2(screenPos.x, screenPos.y)).sqrMagnitude;
                if (sqrDist <= bestSqr)
                {
                    bestSqr = sqrDist;
                    best = entity;
                }
            }

            return best;
        }

        private void ProcessSelectionBox(ref SystemState state, ref EntityCommandBuffer ecb, SelectionBoxEvent boxEvent)
        {
            // Get camera for frustum
            Camera camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            // Convert screen rect to world-space frustum
            Vector2 min = new Vector2(boxEvent.ScreenMin.x, boxEvent.ScreenMin.y);
            Vector2 max = new Vector2(boxEvent.ScreenMax.x, boxEvent.ScreenMax.y);

            // Use spatial grid if available, otherwise use physics overlap
            var selectableEntities = new NativeList<Entity>(Allocator.Temp);

            // For now, use a simple approach: query all selectable entities and check if they're in frustum
            // TODO: Use spatial grid OverlapAABB for better performance
            foreach (var (_, entity) in SystemAPI.Query<SelectableTag>()
                         .WithEntityAccess())
            {
                // Check ownership
                byte ownerId = 0;
                if (state.EntityManager.HasComponent<SelectionOwner>(entity))
                {
                    ownerId = state.EntityManager.GetComponentData<SelectionOwner>(entity).PlayerId;
                }

                if (ownerId != boxEvent.PlayerId)
                {
                    continue;
                }

                // Check if entity is in frustum (simplified: check if world position projects into screen rect)
                if (state.EntityManager.HasComponent<Unity.Transforms.LocalTransform>(entity))
                {
                    var transform = state.EntityManager.GetComponentData<Unity.Transforms.LocalTransform>(entity);
                    Vector3 worldPos = new Vector3(transform.Position.x, transform.Position.y, transform.Position.z);
                    Vector3 screenPos = camera.WorldToScreenPoint(worldPos);

                    if (screenPos.x >= min.x && screenPos.x <= max.x &&
                        screenPos.y >= min.y && screenPos.y <= max.y &&
                        screenPos.z > 0f) // In front of camera
                    {
                        selectableEntities.Add(entity);
                    }
                }
            }

            // Apply selection mode
            if (boxEvent.Mode == SelectionBoxMode.Replace)
            {
                ClearAllSelections(ref state, ref ecb, boxEvent.PlayerId);
                foreach (var entity in selectableEntities)
                {
                    ecb.AddComponent<SelectedTag>(entity);
                }
            }
            else if (boxEvent.Mode == SelectionBoxMode.AdditiveToggle)
            {
                foreach (var entity in selectableEntities)
                {
                    bool isSelected = state.EntityManager.HasComponent<SelectedTag>(entity);
                    if (isSelected)
                    {
                        ecb.RemoveComponent<SelectedTag>(entity);
                    }
                    else
                    {
                        ecb.AddComponent<SelectedTag>(entity);
                    }
                }
            }

            selectableEntities.Dispose();
        }

        private void ClearAllSelections(ref SystemState state, ref EntityCommandBuffer ecb, byte playerId)
        {
            // Clear all selected entities owned by this player
            foreach (var (_, owner, entity) in SystemAPI.Query<SelectedTag, SelectionOwner>()
                         .WithEntityAccess())
            {
                if (owner.PlayerId == playerId)
                {
                    ecb.RemoveComponent<SelectedTag>(entity);
                }
            }
        }
    }
}
