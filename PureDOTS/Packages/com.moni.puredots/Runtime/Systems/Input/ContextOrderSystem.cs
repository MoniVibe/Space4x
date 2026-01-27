using PureDOTS.Input;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Unity.Physics;

namespace PureDOTS.Systems.Input
{
    /// <summary>
    /// Processes right-click events and creates contextual orders (Move/Attack/Harvest/etc.)
    /// based on what was clicked. Appends orders to selected entities' OrderQueueElement buffers.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(SelectionSystem))]
    public partial struct ContextOrderSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RtsInputSingletonTag>();
        }

        public void OnUpdate(ref SystemState state)
        {
            Entity rtsInputEntity = SystemAPI.GetSingletonEntity<RtsInputSingletonTag>();

            if (!state.EntityManager.HasBuffer<RightClickEvent>(rtsInputEntity))
            {
                return;
            }

            var rightClickBuffer = state.EntityManager.GetBuffer<RightClickEvent>(rtsInputEntity);

            for (int i = 0; i < rightClickBuffer.Length; i++)
            {
                var rightClickEvent = rightClickBuffer[i];
                ProcessRightClick(ref state, rightClickEvent);
            }

            // Clear buffer after processing
            rightClickBuffer.Clear();
        }

        private void ProcessRightClick(ref SystemState state, RightClickEvent rightClickEvent)
        {
            // Get camera for raycast
            Camera camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            // Raycast from screen position
            Vector3 screenPos = new Vector3(rightClickEvent.ScreenPos.x, rightClickEvent.ScreenPos.y, 0f);
            UnityEngine.Ray ray = camera.ScreenPointToRay(screenPos);

            Entity hitEntity = Entity.Null;
            float3 hitPosition = float3.zero;
            bool hasHit = RaycastForEntity(ref state, ray, 800f, out hitEntity, out hitPosition);

            // Get all selected entities
            var selectedEntities = new NativeList<Entity>(Allocator.Temp);
            foreach (var (_, entity) in SystemAPI.Query<SelectedTag>()
                         .WithEntityAccess())
            {
                // Check ownership
                byte ownerId = 0;
                if (state.EntityManager.HasComponent<SelectionOwner>(entity))
                {
                    ownerId = state.EntityManager.GetComponentData<SelectionOwner>(entity).PlayerId;
                }

                if (ownerId == rightClickEvent.PlayerId)
                {
                    selectedEntities.Add(entity);
                }
            }

            if (selectedEntities.Length == 0)
            {
                selectedEntities.Dispose();
                return; // No selection
            }

            // Determine order kind based on context
            OrderKind orderKind = DetermineOrderKind(ref state, hitEntity, hasHit);
            bool attackMove = rightClickEvent.Ctrl != 0 && orderKind == OrderKind.Move;
            if (attackMove)
            {
                orderKind = OrderKind.Attack;
                hitEntity = Entity.Null;
            }
            Order order = new Order
            {
                Kind = orderKind,
                TargetPosition = hitPosition,
                TargetEntity = hitEntity,
                Flags = (byte)(attackMove ? OrderFlags.AttackMove : OrderFlags.None)
            };

            // Apply orders to selected entities
            bool queue = rightClickEvent.Queue != 0;
            foreach (var entity in selectedEntities)
            {
                ApplyOrderToEntity(ref state, entity, order, queue);
            }

            selectedEntities.Dispose();
        }

        private bool RaycastForEntity(ref SystemState state, UnityEngine.Ray ray, float maxDistance, out Entity entity, out float3 position)
        {
            entity = Entity.Null;
            position = float3.zero;

            // Try Unity Physics ECS raycast
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
                    entity = hit.Entity;
                    position = hit.Position;
                    return true;
                }
            }

            // Fallback: classic Physics raycast + bridge
            if (UnityEngine.Physics.Raycast(ray, out UnityEngine.RaycastHit hit3d, maxDistance))
            {
                position = new float3(hit3d.point.x, hit3d.point.y, hit3d.point.z);

                var bridge = hit3d.collider.GetComponent<IEntityBridge>();
                if (bridge != null && bridge.TryGetEntity(out var bridged))
                {
                    entity = bridged;
                }

                return true;
            }

            return false;
        }

        private OrderKind DetermineOrderKind(ref SystemState state, Entity hitEntity, bool hasHit)
        {
            if (!hasHit)
            {
                return OrderKind.Move; // Default to move on empty ground
            }

            if (hitEntity == Entity.Null)
            {
                return OrderKind.Move; // Ground hit, no entity
            }

            // TODO: Game-specific logic to determine if hitEntity is:
            // - Enemy (Attack)
            // - Resource (Harvest)
            // - Ally/Building (Defend/Interact)
            // For now, default to Move
            // Games should extend this with their own context resolution

            return OrderKind.Move;
        }

        private void ApplyOrderToEntity(ref SystemState state, Entity entity, Order order, bool queue)
        {
            // Check if entity belongs to a group (band/fleet)
            // TODO: Query for group membership and push orders to group entity instead
            // For now, apply directly to entity

            if (!state.EntityManager.HasBuffer<OrderQueueElement>(entity))
            {
                state.EntityManager.AddBuffer<OrderQueueElement>(entity);
            }

            var orderBuffer = state.EntityManager.GetBuffer<OrderQueueElement>(entity);

            if (!queue)
            {
                // Clear queue and add new order
                orderBuffer.Clear();
            }

            orderBuffer.Add(new OrderQueueElement { Order = order });
        }
    }
}
