using PureDOTS.Input;
using PureDOTS.Runtime.Core;
using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using UnityEngine;

namespace Space4X.Systems
{
    /// <summary>
    /// Space4X-specific contextual orders: harvest asteroids, attack enemies, move otherwise.
    /// Consumes RtsInput RightClickEvent and applies OrderQueueElement to selected entities.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PureDOTS.Systems.Input.SelectionSystem))]
    [UpdateBefore(typeof(PureDOTS.Systems.Input.ContextOrderSystem))]
    public partial struct Space4XContextOrderSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RtsInputSingletonTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (RuntimeMode.IsHeadless)
                return;

            var inputEntity = SystemAPI.GetSingletonEntity<RtsInputSingletonTag>();
            if (!state.EntityManager.HasBuffer<RightClickEvent>(inputEntity))
                return;

            var rightClicks = state.EntityManager.GetBuffer<RightClickEvent>(inputEntity);
            if (rightClicks.Length == 0)
                return;

            foreach (var evt in rightClicks)
            {
                ProcessRightClick(ref state, evt);
            }

            rightClicks.Clear();
        }

        private void ProcessRightClick(ref SystemState state, RightClickEvent evt)
        {
            UnityEngine.Camera camera = UnityEngine.Camera.main;
            if (camera == null)
                return;

            UnityEngine.Ray ray = camera.ScreenPointToRay(new Vector3(evt.ScreenPos.x, evt.ScreenPos.y, 0f));

            Entity hitEntity;
            float3 hitPos;
            bool hasHit = RaycastForEntity(ref state, ray, 2000f, out hitEntity, out hitPos);

            // Default ground plane hit if no collider hit
            if (!hasHit)
            {
                var plane = new UnityEngine.Plane(Vector3.up, 0f);
                if (plane.Raycast(ray, out var enter))
                {
                    hasHit = true;
                    hitPos = ray.origin + ray.direction * enter;
                }
            }

            var selected = new NativeList<Entity>(Allocator.Temp);
            foreach (var (_, entity) in SystemAPI.Query<SelectedTag>().WithEntityAccess())
            {
                if (state.EntityManager.HasComponent<SelectionOwner>(entity))
                {
                    var owner = state.EntityManager.GetComponentData<SelectionOwner>(entity);
                    if (owner.PlayerId != evt.PlayerId)
                        continue;
                }

                selected.Add(entity);
            }

            if (selected.Length == 0)
            {
                selected.Dispose();
                return;
            }

            Entity ownerAff = GetPrimaryAffiliation(ref state, selected[0]);
            Entity targetAff = GetPrimaryAffiliation(ref state, hitEntity);

            var orderKind = DetermineOrderKind(ref state, hitEntity, hasHit, ownerAff, targetAff);
            var order = new Order
            {
                Kind = orderKind,
                TargetEntity = hitEntity,
                TargetPosition = hitPos,
                Flags = 0
            };

            bool queue = evt.Queue != 0;
            foreach (var entity in selected)
            {
                ApplyOrder(ref state, entity, order, queue);
            }

            selected.Dispose();
        }

        private Entity GetPrimaryAffiliation(ref SystemState state, Entity entity)
        {
            if (entity == Entity.Null)
                return Entity.Null;

            if (state.EntityManager.HasBuffer<AffiliationTag>(entity))
            {
                var buffer = state.EntityManager.GetBuffer<AffiliationTag>(entity);
                if (buffer.Length > 0)
                {
                    return buffer[0].Target;
                }
            }

            if (state.EntityManager.HasComponent<Carrier>(entity))
            {
                return state.EntityManager.GetComponentData<Carrier>(entity).AffiliationEntity;
            }

            return Entity.Null;
        }

        private OrderKind DetermineOrderKind(ref SystemState state, Entity hitEntity, bool hasHit, Entity ownerAff, Entity targetAff)
        {
            if (!hasHit)
                return OrderKind.Move;

            if (hitEntity != Entity.Null)
            {
                if (state.EntityManager.HasComponent<Asteroid>(hitEntity))
                    return OrderKind.Harvest;

                if (ownerAff != Entity.Null && targetAff != Entity.Null && ownerAff != targetAff)
                    return OrderKind.Attack;
            }

            return OrderKind.Move;
        }

        private void ApplyOrder(ref SystemState state, Entity entity, Order order, bool queue)
        {
            if (!state.EntityManager.HasBuffer<OrderQueueElement>(entity))
            {
                state.EntityManager.AddBuffer<OrderQueueElement>(entity);
            }

            var buffer = state.EntityManager.GetBuffer<OrderQueueElement>(entity);
            if (!queue)
            {
                buffer.Clear();
            }
            buffer.Add(new OrderQueueElement { Order = order });
        }

        private bool RaycastForEntity(ref SystemState state, UnityEngine.Ray ray, float maxDistance, out Entity entity, out float3 position)
        {
            entity = Entity.Null;
            position = float3.zero;

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
    }
}
