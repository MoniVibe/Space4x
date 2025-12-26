using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Swarms;
using Space4X.Registry;
using Space4X.Runtime;
using Space4X.Runtime.Breakables;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Systems.AI
{
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(SwarmBehaviorSystem))]
    public partial struct Space4XSwarmDemoSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<SwarmThrustState> _swarmThrustLookup;
        private ComponentLookup<Space4XShipCapabilityState> _capabilityLookup;
        private ComponentLookup<PureDOTS.Runtime.Agency.ControlLinkState> _controlLinkLookup;
        private const uint OrderLeaseTicks = 120;
        private const uint RescueReissueTicks = 60;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<Space4XSwarmDemoState>();
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _swarmThrustLookup = state.GetComponentLookup<SwarmThrustState>(false);
            _capabilityLookup = state.GetComponentLookup<Space4XShipCapabilityState>(true);
            _controlLinkLookup = state.GetComponentLookup<PureDOTS.Runtime.Agency.ControlLinkState>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            _transformLookup.Update(ref state);
            _swarmThrustLookup.Update(ref state);
            _capabilityLookup.Update(ref state);
            _controlLinkLookup.Update(ref state);

            var tick = timeState.Tick;
            var fixedDt = math.max(1e-6f, timeState.FixedDeltaTime);

            foreach (var (demoState, anchorTransform, anchorEntity) in SystemAPI.Query<RefRW<Space4XSwarmDemoState>, RefRO<LocalTransform>>()
                         .WithEntityAccess())
            {
                var anchorPos = anchorTransform.ValueRO.Position;
                var rescueTarget = FindRescueTarget(anchorEntity, ref state);
                UpdateRescueRequest(anchorEntity, rescueTarget, tick, ref state);
                var phase = demoState.ValueRO.Phase;
                var nextTick = demoState.ValueRO.NextPhaseTick;

                if (rescueTarget != Entity.Null)
                {
                    phase = Space4XSwarmDemoPhase.Tug;
                    nextTick = tick + SecondsToTicks(PhaseDurationSeconds(phase), fixedDt);
                    demoState.ValueRW.TugDirection = ResolveRescueDirection(anchorPos, ref state);
                }
                else if (nextTick == 0u)
                {
                    phase = Space4XSwarmDemoPhase.Screen;
                    nextTick = tick + SecondsToTicks(12f, fixedDt);
                    demoState.ValueRW.AttackTarget = FindAttackTarget(anchorPos, ref state);
                    demoState.ValueRW.TugDirection = ResolveTugDirection(anchorPos, demoState.ValueRO.AttackTarget);
                }
                else if (tick >= nextTick)
                {
                    phase = NextPhase(phase);
                    nextTick = tick + SecondsToTicks(PhaseDurationSeconds(phase), fixedDt);

                    if (phase == Space4XSwarmDemoPhase.Attack && demoState.ValueRO.AttackTarget == Entity.Null)
                    {
                        demoState.ValueRW.AttackTarget = FindAttackTarget(anchorPos, ref state);
                    }

                    if (phase == Space4XSwarmDemoPhase.Tug)
                    {
                        demoState.ValueRW.TugDirection = ResolveTugDirection(anchorPos, demoState.ValueRO.AttackTarget);
                    }
                }

                demoState.ValueRW.Phase = phase;
                demoState.ValueRW.NextPhaseTick = nextTick;

                if (phase == Space4XSwarmDemoPhase.Tug && rescueTarget != Entity.Null)
                {
                    EmitTowRescueRequest(anchorEntity, rescueTarget, tick, ref state);
                }

                ApplyPhase(ref state, anchorEntity, phase, rescueTarget != Entity.Null, demoState.ValueRO, tick);
            }
        }

        private void ApplyPhase(ref SystemState state, Entity anchorEntity, Space4XSwarmDemoPhase phase, bool canTug, in Space4XSwarmDemoState demo, uint tick)
        {
            ToggleSwarmThrust(anchorEntity, phase, canTug, demo, ref state);

            foreach (var (orderState, droneOrbit, droneEntity) in SystemAPI.Query<RefRW<PureDOTS.Runtime.Agency.ControlOrderState>, RefRO<DroneOrbit>>()
                         .WithAll<DroneTag>())
            {
                if (droneOrbit.ValueRO.AnchorShip != anchorEntity)
                {
                    continue;
                }

                if (_controlLinkLookup.HasComponent(droneEntity) && _controlLinkLookup[droneEntity].IsLost != 0)
                {
                    continue;
                }

                var updated = orderState.ValueRO;
                updated.Kind = phase switch
                {
                    Space4XSwarmDemoPhase.Screen => PureDOTS.Runtime.Agency.ControlOrderKind.Screen,
                    Space4XSwarmDemoPhase.Tug => canTug ? PureDOTS.Runtime.Agency.ControlOrderKind.Tow : PureDOTS.Runtime.Agency.ControlOrderKind.Screen,
                    Space4XSwarmDemoPhase.Attack => PureDOTS.Runtime.Agency.ControlOrderKind.Attack,
                    Space4XSwarmDemoPhase.Return => PureDOTS.Runtime.Agency.ControlOrderKind.Return,
                    _ => PureDOTS.Runtime.Agency.ControlOrderKind.Screen
                };
                updated.TargetEntity = updated.Kind == PureDOTS.Runtime.Agency.ControlOrderKind.Attack ? demo.AttackTarget : Entity.Null;
                updated.AnchorEntity = anchorEntity;
                updated.AnchorPosition = float3.zero;
                updated.IssuedTick = tick;
                updated.LastUpdatedTick = tick;
                updated.Sequence += 1u;
                updated.RequiresHeartbeat = 1;
                updated.FallbackKind = PureDOTS.Runtime.Agency.ControlOrderKind.Return;
                updated.ExpiryTick = tick + OrderLeaseTicks;
                orderState.ValueRW = updated;
            }
        }

        private void ToggleSwarmThrust(Entity anchorEntity, Space4XSwarmDemoPhase phase, bool canTug, in Space4XSwarmDemoState demo, ref SystemState state)
        {
            if (!_swarmThrustLookup.HasComponent(anchorEntity))
            {
                return;
            }

            var thrust = _swarmThrustLookup[anchorEntity];
            if (phase == Space4XSwarmDemoPhase.Tug && canTug)
            {
                thrust.Active = true;
                thrust.DesiredDirection = math.normalizesafe(demo.TugDirection, new float3(1f, 0f, 0f));
            }
            else
            {
                thrust.Active = false;
                thrust.CurrentThrust = 0f;
            }

            _swarmThrustLookup[anchorEntity] = thrust;
        }

        private Entity FindAttackTarget(float3 anchorPos, ref SystemState state)
        {
            Entity best = Entity.Null;
            float bestDistance = float.MaxValue;

            foreach (var (asteroid, transform, entity) in SystemAPI.Query<RefRO<Asteroid>, RefRO<LocalTransform>>()
                         .WithEntityAccess())
            {
                float distance = math.distance(anchorPos, transform.ValueRO.Position);
                if (distance < bestDistance || (math.abs(distance - bestDistance) < 0.01f && entity.Index < best.Index))
                {
                    best = entity;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private float3 ResolveTugDirection(float3 anchorPos, Entity attackTarget)
        {
            if (attackTarget != Entity.Null && _transformLookup.HasComponent(attackTarget))
            {
                var targetPos = _transformLookup[attackTarget].Position;
                return math.normalizesafe(anchorPos - targetPos, new float3(1f, 0f, 0f));
            }

            return new float3(1f, 0f, 0f);
        }

        private Entity FindRescueTarget(Entity anchorEntity, ref SystemState state)
        {
            if (_capabilityLookup.HasComponent(anchorEntity))
            {
                var capability = _capabilityLookup[anchorEntity];
                if (capability.IsAlive != 0 && capability.IsMobile == 0)
                {
                    return anchorEntity;
                }
            }

            return Entity.Null;
        }

        private void UpdateRescueRequest(Entity anchorEntity, Entity target, uint tick, ref SystemState state)
        {
            if (target == Entity.Null || !state.EntityManager.Exists(target))
            {
                if (state.EntityManager.HasComponent<Space4XTowRescueRequest>(anchorEntity))
                {
                    state.EntityManager.RemoveComponent<Space4XTowRescueRequest>(anchorEntity);
                }
                return;
            }

            if (!_capabilityLookup.HasComponent(target))
            {
                if (state.EntityManager.HasComponent<Space4XTowRescueRequest>(anchorEntity))
                {
                    state.EntityManager.RemoveComponent<Space4XTowRescueRequest>(anchorEntity);
                }
                return;
            }

            var capability = _capabilityLookup[target];
            if (capability.IsAlive == 0 || capability.IsMobile != 0)
            {
                if (state.EntityManager.HasComponent<Space4XTowRescueRequest>(anchorEntity))
                {
                    state.EntityManager.RemoveComponent<Space4XTowRescueRequest>(anchorEntity);
                }
                return;
            }

            EmitTowRescueRequest(anchorEntity, target, tick, ref state);
        }

        private static void EmitTowRescueRequest(Entity anchorEntity, Entity target, uint tick, ref SystemState state)
        {
            if (state.EntityManager.HasComponent<Space4XTowRescueRequest>(anchorEntity))
            {
                var request = state.EntityManager.GetComponentData<Space4XTowRescueRequest>(anchorEntity);
                if (request.Target != target || tick >= request.LastUpdatedTick + RescueReissueTicks)
                {
                    request.Target = target;
                    request.Priority = 1;
                    request.LastUpdatedTick = tick;
                    request.ExpireTick = tick + RescueReissueTicks;
                    state.EntityManager.SetComponentData(anchorEntity, request);
                }
                else if (tick >= request.ExpireTick)
                {
                    request.ExpireTick = tick + RescueReissueTicks;
                    state.EntityManager.SetComponentData(anchorEntity, request);
                }

                return;
            }

            state.EntityManager.AddComponentData(anchorEntity, new Space4XTowRescueRequest
            {
                Target = target,
                IssuedTick = tick,
                LastUpdatedTick = tick,
                ExpireTick = tick + RescueReissueTicks,
                Priority = 1
            });
        }

        private float3 ResolveRescueDirection(float3 anchorPos, ref SystemState state)
        {
            Entity nearest = Entity.Null;
            float bestDistance = float.MaxValue;

            foreach (var (asteroid, transform, entity) in SystemAPI.Query<RefRO<Asteroid>, RefRO<LocalTransform>>()
                         .WithEntityAccess())
            {
                float distance = math.distance(anchorPos, transform.ValueRO.Position);
                if (distance < bestDistance || (math.abs(distance - bestDistance) < 0.01f && entity.Index < nearest.Index))
                {
                    bestDistance = distance;
                    nearest = entity;
                }
            }

            if (nearest != Entity.Null && _transformLookup.HasComponent(nearest))
            {
                var targetPos = _transformLookup[nearest].Position;
                return math.normalizesafe(anchorPos - targetPos, new float3(1f, 0f, 0f));
            }

            return new float3(1f, 0f, 0f);
        }

        private static uint SecondsToTicks(float seconds, float fixedDt)
        {
            return (uint)math.max(1, math.ceil(seconds / fixedDt));
        }

        private static float PhaseDurationSeconds(Space4XSwarmDemoPhase phase)
        {
            return phase switch
            {
                Space4XSwarmDemoPhase.Screen => 12f,
                Space4XSwarmDemoPhase.Tug => 6f,
                Space4XSwarmDemoPhase.Attack => 8f,
                Space4XSwarmDemoPhase.Return => 4f,
                _ => 8f
            };
        }

        private static Space4XSwarmDemoPhase NextPhase(Space4XSwarmDemoPhase phase)
        {
            return phase switch
            {
                Space4XSwarmDemoPhase.Screen => Space4XSwarmDemoPhase.Tug,
                Space4XSwarmDemoPhase.Tug => Space4XSwarmDemoPhase.Attack,
                Space4XSwarmDemoPhase.Attack => Space4XSwarmDemoPhase.Return,
                _ => Space4XSwarmDemoPhase.Screen
            };
        }
    }
}
