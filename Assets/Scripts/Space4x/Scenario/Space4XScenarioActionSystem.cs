using PureDOTS.Runtime.Components;
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
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<Carrier> _carrierLookup;
        private ComponentLookup<PatrolBehavior> _patrolLookup;
        private ComponentLookup<MovementCommand> _movementLookup;
        private ComponentLookup<FleetMovementBroadcast> _broadcastLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<Space4XScenarioRuntime>();
            state.RequireForUpdate<Space4XScenarioAction>();
            state.RequireForUpdate<RewindState>();

            _transformLookup = state.GetComponentLookup<LocalTransform>(false);
            _carrierLookup = state.GetComponentLookup<Carrier>(false);
            _patrolLookup = state.GetComponentLookup<PatrolBehavior>(false);
            _movementLookup = state.GetComponentLookup<MovementCommand>(false);
            _broadcastLookup = state.GetComponentLookup<FleetMovementBroadcast>(false);
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

            _transformLookup.Update(ref state);
            _carrierLookup.Update(ref state);
            _patrolLookup.Update(ref state);
            _movementLookup.Update(ref state);
            _broadcastLookup.Update(ref state);

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

            foreach (var (fleet, entity) in SystemAPI.Query<RefRO<Space4XFleet>>().WithEntityAccess())
            {
                if (!fleet.ValueRO.FleetId.Equals(action.FleetId))
                {
                    continue;
                }

                if (_transformLookup.HasComponent(entity))
                {
                    var transform = _transformLookup.GetRefRW(entity);
                    transform.ValueRW.Position = action.TargetPosition;
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

                if (_broadcastLookup.HasComponent(entity))
                {
                    var broadcast = _broadcastLookup.GetRefRW(entity);
                    broadcast.ValueRW.Position = action.TargetPosition;
                    broadcast.ValueRW.Velocity = float3.zero;
                    broadcast.ValueRW.LastUpdateTick = tick;
                }
            }
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
