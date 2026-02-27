using System;
using PureDOTS.Input;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Interrupts;
using Space4X.Registry;
using Space4X.Runtime;
using Space4X.UI;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Space4X.Systems
{
    /// <summary>
    /// Space4X-specific contextual orders: harvest asteroids, attack enemies, move otherwise.
    /// Consumes RtsInput RightClickEvent and applies OrderQueueElement to selected entities.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PureDOTS.Systems.Input.SelectionSystem))]
    [UpdateBefore(typeof(PureDOTS.Systems.Input.ContextOrderSystem))]
    public partial struct Space4XContextOrderSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RtsInputSingletonTag>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!RuntimeMode.IsRenderingEnabled)
                return;

            var inputEntity = SystemAPI.GetSingletonEntity<RtsInputSingletonTag>();
            if (!state.EntityManager.HasBuffer<RightClickEvent>(inputEntity))
                return;

            var rightClicks = state.EntityManager.GetBuffer<RightClickEvent>(inputEntity);
            if (Space4XControlModeState.CurrentMode != Space4XControlMode.Rts)
            {
                if (rightClicks.Length > 0)
                {
                    rightClicks.Clear();
                }

                return;
            }

            if (rightClicks.Length == 0)
                return;
            using var rightClickEvents = rightClicks.ToNativeArray(Allocator.Temp);
            rightClicks.Clear();

            uint tick = 0;
            if (SystemAPI.TryGetSingleton<TimeState>(out var timeState))
            {
                tick = timeState.Tick;
            }

            foreach (var evt in rightClickEvents)
            {
                ProcessRightClick(ref state, inputEntity, evt, tick);
            }

        }

        private void ProcessRightClick(ref SystemState state, Entity inputEntity, RightClickEvent evt, uint tick)
        {
            UnityEngine.Camera camera = ResolveOrderCamera();
            bool hasRay = camera != null;
            if (!hasRay && evt.HasWorldPoint == 0)
            {
                return;
            }

            UnityEngine.Ray ray = default;
            if (hasRay)
            {
                ray = camera.ScreenPointToRay(new Vector3(evt.ScreenPos.x, evt.ScreenPos.y, 0f));
            }

            Entity hitEntity = evt.HasHitEntity != 0 ? evt.HitEntity : Entity.Null;
            float3 hitPos = evt.WorldPoint;
            bool hasHit = evt.HasWorldPoint != 0;

            if (hasRay && hitEntity == Entity.Null)
            {
                if (RaycastForEntity(ref state, ray, 2000f, out var resolvedEntity, out var resolvedPosition))
                {
                    hitEntity = resolvedEntity;
                    if (!hasHit)
                    {
                        hasHit = true;
                        hitPos = resolvedPosition;
                    }
                }
            }

            if (!hasHit && hasRay)
            {
                hasHit = TryProjectOnCommandPlane(ref state, inputEntity, ray, out hitPos);
            }

            // Legacy fallback: default world up plane at y = 0 when no other hit/projection is available.
            if (!hasHit && hasRay)
            {
                var plane = new UnityEngine.Plane(Vector3.up, 0f);
                if (plane.Raycast(ray, out var enter))
                {
                    hasHit = true;
                    var world = ray.GetPoint(enter);
                    hitPos = new float3(world.x, world.y, world.z);
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
            var keyboard = Keyboard.current;
            var attackMoveModifier = evt.Ctrl != 0 ||
                                     (keyboard != null && Space4XControlModeState.CurrentMode == Space4XControlMode.Rts && keyboard.aKey.isPressed);
            bool attackMove = attackMoveModifier && orderKind == OrderKind.Move;
            if (attackMove)
            {
                orderKind = OrderKind.Attack;
                hitEntity = Entity.Null;
            }
            var order = new Order
            {
                Kind = orderKind,
                TargetEntity = hitEntity,
                TargetPosition = hitPos,
                Flags = (byte)(attackMove ? OrderFlags.AttackMove : OrderFlags.None)
            };

            bool queue = evt.Queue != 0;
            foreach (var entity in selected)
            {
                ApplyOrder(ref state, entity, order, queue, tick);
                if (attackMove)
                {
                    var source = evt.Ctrl != 0 ? AttackMoveSource.CtrlConvert : AttackMoveSource.AttackTerrain;
                    ApplyAttackMoveSourceHint(ref state, entity, tick, source);
                }
            }

            if (hasHit)
            {
                UpdateCommandPlaneStateFromOrder(ref state, inputEntity, hitPos);
            }

            selected.Dispose();
        }

        private bool TryProjectOnCommandPlane(ref SystemState state, Entity inputEntity, UnityEngine.Ray ray, out float3 projectedPoint)
        {
            projectedPoint = float3.zero;
            if (!state.EntityManager.HasComponent<RtsCommandPlaneState>(inputEntity))
            {
                return false;
            }

            var planeState = state.EntityManager.GetComponentData<RtsCommandPlaneState>(inputEntity);
            float3 normal = planeState.PlaneNormal;
            if (math.lengthsq(normal) < 1e-6f)
            {
                normal = new float3(0f, 1f, 0f);
            }
            else
            {
                normal = math.normalize(normal);
            }

            float3 origin = planeState.PlaneOrigin;
            if (math.abs(normal.y) > 0.999f)
            {
                origin = new float3(origin.x, planeState.PlaneHeight, origin.z);
            }

            var plane = new UnityEngine.Plane(
                new Vector3(normal.x, normal.y, normal.z),
                new Vector3(origin.x, origin.y, origin.z));
            if (!plane.Raycast(ray, out float enter))
            {
                return false;
            }

            Vector3 world = ray.GetPoint(enter);
            projectedPoint = new float3(world.x, world.y, world.z);
            return true;
        }

        private void UpdateCommandPlaneStateFromOrder(ref SystemState state, Entity inputEntity, float3 commandPoint)
        {
            if (!state.EntityManager.HasComponent<RtsCommandPlaneState>(inputEntity))
            {
                return;
            }

            var planeState = state.EntityManager.GetComponentData<RtsCommandPlaneState>(inputEntity);
            planeState.LastCommandPoint = commandPoint;
            planeState.HasLastCommandPoint = 1;
            planeState.PlaneHeight = commandPoint.y;
            planeState.PlaneOrigin = commandPoint;
            state.EntityManager.SetComponentData(inputEntity, planeState);
        }

        private void ApplyAttackMoveSourceHint(ref SystemState state, Entity entity, uint tick, AttackMoveSource source)
        {
            var hint = new AttackMoveSourceHint
            {
                Source = source,
                IssuedTick = tick
            };

            if (state.EntityManager.HasComponent<AttackMoveSourceHint>(entity))
            {
                state.EntityManager.SetComponentData(entity, hint);
            }
            else
            {
                try
                {
                    state.EntityManager.AddComponentData(entity, hint);
                }
                catch (Exception)
                {
                    // Archetype can be at chunk capacity for player-heavy vessels.
                    // Skip optional source hint rather than failing RTS command dispatch.
                }
            }
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

        private void ApplyOrder(ref SystemState state, Entity entity, Order order, bool queue, uint tick)
        {
            TryApplyQueuedOrder(ref state, entity, order, queue);
            ApplyImmediateOrder(ref state, entity, order, tick);
        }

        private bool TryApplyQueuedOrder(ref SystemState state, Entity entity, Order order, bool queue)
        {
            try
            {
                if (!state.EntityManager.HasBuffer<OrderQueueElement>(entity))
                {
                    state.EntityManager.AddBuffer<OrderQueueElement>(entity);
                }

                if (!state.EntityManager.HasBuffer<OrderQueueElement>(entity))
                {
                    return false;
                }

                var buffer = state.EntityManager.GetBuffer<OrderQueueElement>(entity);
                if (!queue)
                {
                    buffer.Clear();
                }

                buffer.Add(new OrderQueueElement { Order = order });
                return true;
            }
            catch (Exception)
            {
                // Some vessels are at archetype chunk-size limits and cannot accept new buffers.
                // Fall back to direct non-structural order application for RTS responsiveness.
                return false;
            }
        }

        private void ApplyImmediateOrder(ref SystemState state, Entity entity, Order order, uint tick)
        {
            var entityManager = state.EntityManager;
            var targetPosition = ResolveOrderTargetPosition(ref state, order);

            if (entityManager.HasComponent<EntityIntent>(entity))
            {
                var intent = entityManager.GetComponentData<EntityIntent>(entity);
                intent.Mode = ResolveIntentMode(order.Kind);
                intent.TargetEntity = order.TargetEntity;
                intent.TargetPosition = targetPosition;
                intent.TriggeringInterrupt = InterruptType.NewOrder;
                intent.IntentSetTick = tick;
                intent.Priority = InterruptPriority.High;
                intent.IsValid = 1;
                entityManager.SetComponentData(entity, intent);
            }

            if (entityManager.HasComponent<VesselAIState>(entity))
            {
                var aiState = entityManager.GetComponentData<VesselAIState>(entity);
                ApplyOrderToVesselAi(ref aiState, order.Kind, order.TargetEntity, targetPosition, tick, entityManager.HasComponent<MiningVessel>(entity));
                entityManager.SetComponentData(entity, aiState);
            }

            if (entityManager.HasComponent<MovementCommand>(entity))
            {
                var movement = entityManager.GetComponentData<MovementCommand>(entity);
                movement.TargetPosition = targetPosition;
                movement.ArrivalThreshold = order.Kind == OrderKind.Attack ? 4f : 1f;
                entityManager.SetComponentData(entity, movement);
            }

            if (entityManager.HasComponent<Space4XEngagement>(entity))
            {
                var engagement = entityManager.GetComponentData<Space4XEngagement>(entity);
                if (order.Kind == OrderKind.Attack)
                {
                    engagement.PrimaryTarget = order.TargetEntity;
                    if (order.TargetEntity != Entity.Null)
                    {
                        engagement.Phase = EngagementPhase.Approaching;
                    }
                }
                else
                {
                    engagement.PrimaryTarget = Entity.Null;
                    if (engagement.Phase != EngagementPhase.Destroyed && engagement.Phase != EngagementPhase.Disabled)
                    {
                        engagement.Phase = EngagementPhase.None;
                    }
                }

                entityManager.SetComponentData(entity, engagement);
            }

            // Keep attack-move state updates non-structural in RTS mode.
            if (order.Kind == OrderKind.Attack && entityManager.HasComponent<AttackMoveIntent>(entity))
            {
                var attackMove = entityManager.GetComponentData<AttackMoveIntent>(entity);
                attackMove.Destination = targetPosition;
                attackMove.DestinationRadius = math.max(attackMove.DestinationRadius, 0f);
                attackMove.EngageTarget = order.TargetEntity;
                attackMove.AcquireTargetsAlongRoute = order.TargetEntity == Entity.Null ? (byte)1 : (byte)0;
                attackMove.KeepFiringWhileInRange = 1;
                attackMove.StartTick = tick;
                entityManager.SetComponentData(entity, attackMove);
            }
        }

        private float3 ResolveOrderTargetPosition(ref SystemState state, in Order order)
        {
            if (math.lengthsq(order.TargetPosition) > 1e-6f)
            {
                return order.TargetPosition;
            }

            if (order.TargetEntity != Entity.Null && state.EntityManager.HasComponent<LocalTransform>(order.TargetEntity))
            {
                return state.EntityManager.GetComponentData<LocalTransform>(order.TargetEntity).Position;
            }

            return float3.zero;
        }

        private static IntentMode ResolveIntentMode(OrderKind kind)
        {
            return kind switch
            {
                OrderKind.Attack => IntentMode.Attack,
                OrderKind.Harvest => IntentMode.Gather,
                OrderKind.Defend => IntentMode.Defend,
                OrderKind.Patrol => IntentMode.Patrol,
                OrderKind.UseAbility => IntentMode.UseAbility,
                _ => IntentMode.MoveTo
            };
        }

        private static void ApplyOrderToVesselAi(
            ref VesselAIState aiState,
            OrderKind kind,
            Entity targetEntity,
            float3 targetPosition,
            uint tick,
            bool isMiningVessel)
        {
            VesselAIState.Goal goal = kind switch
            {
                OrderKind.Defend => VesselAIState.Goal.Escort,
                OrderKind.Patrol => VesselAIState.Goal.Patrol,
                OrderKind.Harvest when isMiningVessel => VesselAIState.Goal.Mining,
                _ => VesselAIState.Goal.Patrol
            };

            aiState.CurrentGoal = goal;
            aiState.CurrentState = goal == VesselAIState.Goal.Mining
                ? VesselAIState.State.MovingToTarget
                : VesselAIState.State.MovingToTarget;
            aiState.TargetEntity = targetEntity;
            aiState.TargetPosition = targetPosition;
            aiState.StateTimer = 0f;
            aiState.StateStartTick = tick;
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

        private static UnityEngine.Camera ResolveOrderCamera()
        {
            var main = UnityEngine.Camera.main;
            if (main != null)
            {
                return main;
            }

            // RTS camera can be untagged in some scene slices; fall back to first active camera.
            return UnityEngine.Object.FindFirstObjectByType<UnityEngine.Camera>();
        }
    }
}
