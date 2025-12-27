using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Interrupts;
using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4x.Scenario
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial struct Space4XScenarioActionSystem : ISystem
    {
        private ComponentLookup<Carrier> _carrierLookup;
        private ComponentLookup<PatrolBehavior> _patrolLookup;
        private ComponentLookup<MovementCommand> _movementLookup;
        private ComponentLookup<EntityIntent> _intentLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<Space4XScenarioRuntime>();
            state.RequireForUpdate<Space4XScenarioAction>();
            state.RequireForUpdate<RewindState>();

            _carrierLookup = state.GetComponentLookup<Carrier>(false);
            _patrolLookup = state.GetComponentLookup<PatrolBehavior>(false);
            _movementLookup = state.GetComponentLookup<MovementCommand>(false);
            _intentLookup = state.GetComponentLookup<EntityIntent>(false);
        }

        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            if (time.IsPaused)
            {
                return;
            }

            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (rewind.Mode != RewindMode.Record)
            {
                return;
            }

            var tick = time.Tick;

            _carrierLookup.Update(ref state);
            _patrolLookup.Update(ref state);
            _movementLookup.Update(ref state);
            _intentLookup.Update(ref state);

            foreach (var actions in SystemAPI.Query<DynamicBuffer<Space4XScenarioAction>>())
            {
                var actionBuffer = actions;
                for (int i = 0; i < actionBuffer.Length; i++)
                {
                    var action = actionBuffer[i];
                    if (action.Executed != 0 || action.ExecuteTick > tick)
                    {
                        continue;
                    }

                    switch (action.Kind)
                    {
                        case Space4XScenarioActionKind.MoveFleet:
                            ProcessMoveFleet(ref state, action, tick);
                            break;
                        case Space4XScenarioActionKind.TriggerIntercept:
                            ProcessTriggerIntercept(ref state, action, tick);
                            break;
                    }

                    action.Executed = 1;
                    actionBuffer[i] = action;
                }
            }
        }

        private void ProcessMoveFleet(ref SystemState state, in Space4XScenarioAction action, uint tick)
        {
            if (action.FleetId.IsEmpty)
            {
                return;
            }

            var moveTarget = GetOrCreateMoveTarget(ref state, action.FleetId, action.TargetPosition, tick);

            foreach (var (fleet, entity) in SystemAPI.Query<RefRO<Space4XFleet>>().WithEntityAccess())
            {
                if (!fleet.ValueRO.FleetId.Equals(action.FleetId))
                {
                    continue;
                }

                if (_carrierLookup.HasComponent(entity))
                {
                    var carrier = _carrierLookup.GetRefRW(entity);
                    carrier.ValueRW.PatrolCenter = action.TargetPosition;
                }

                if (_patrolLookup.HasComponent(entity))
                {
                    var patrol = _patrolLookup.GetRefRW(entity);
                    patrol.ValueRW.CurrentWaypoint = action.TargetPosition;
                    patrol.ValueRW.WaitTimer = 0f;
                }

                if (_movementLookup.HasComponent(entity))
                {
                    var movement = _movementLookup.GetRefRW(entity);
                    movement.ValueRW.TargetPosition = action.TargetPosition;
                    movement.ValueRW.ArrivalThreshold = math.max(1f, movement.ValueRO.ArrivalThreshold);
                }

                if (_intentLookup.HasComponent(entity))
                {
                    var intent = _intentLookup[entity];
                    intent.Mode = IntentMode.MoveTo;
                    intent.TargetEntity = moveTarget;
                    intent.TargetPosition = action.TargetPosition;
                    intent.TriggeringInterrupt = InterruptType.ObjectiveChanged;
                    intent.IntentSetTick = tick;
                    intent.Priority = InterruptPriority.High;
                    intent.IsValid = 1;
                    _intentLookup[entity] = intent;
                }
            }
        }

        private Entity GetOrCreateMoveTarget(ref SystemState state, FixedString64Bytes fleetId, float3 position, uint tick)
        {
            foreach (var (target, transform, entity) in SystemAPI.Query<RefRW<Space4XScenarioMoveTarget>, RefRW<LocalTransform>>()
                         .WithEntityAccess())
            {
                if (!target.ValueRO.FleetId.Equals(fleetId))
                {
                    continue;
                }

                transform.ValueRW = LocalTransform.FromPosition(position);
                return entity;
            }

            var targetEntity = state.EntityManager.CreateEntity(typeof(Space4XScenarioMoveTarget), typeof(LocalTransform));
            state.EntityManager.SetComponentData(targetEntity, new Space4XScenarioMoveTarget
            {
                FleetId = fleetId,
                CreatedTick = tick
            });
            state.EntityManager.SetComponentData(targetEntity, LocalTransform.FromPosition(position));
            return targetEntity;
        }

        private void ProcessTriggerIntercept(ref SystemState state, in Space4XScenarioAction action, uint tick)
        {
            if (action.FleetId.IsEmpty || action.TargetFleetId.IsEmpty)
            {
                return;
            }

            if (!SystemAPI.TryGetSingletonEntity<Space4XFleetInterceptQueue>(out var queueEntity))
            {
                return;
            }

            if (!TryResolveFleetEntity(ref state, action.FleetId, out var requester) ||
                !TryResolveFleetEntity(ref state, action.TargetFleetId, out var target))
            {
                return;
            }

            var requests = state.EntityManager.GetBuffer<InterceptRequest>(queueEntity);
            requests.Add(new InterceptRequest
            {
                Requester = requester,
                Target = target,
                Priority = 0,
                RequestTick = tick,
                RequireRendezvous = 0
            });
        }

        private bool TryResolveFleetEntity(ref SystemState state, FixedString64Bytes fleetId, out Entity entity)
        {
            foreach (var (fleet, fleetEntity) in SystemAPI.Query<RefRO<Space4XFleet>>().WithEntityAccess())
            {
                if (fleet.ValueRO.FleetId.Equals(fleetId))
                {
                    entity = fleetEntity;
                    return true;
                }
            }

            entity = Entity.Null;
            return false;
        }
    }
}
