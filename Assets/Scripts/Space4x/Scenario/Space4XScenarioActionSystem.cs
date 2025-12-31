using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Interrupts;
using PureDOTS.Runtime.Physics;
using Space4X.Registry;
using Space4X.Presentation;
using Space4X.Runtime;
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
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<VesselMovement> _vesselMovementLookup;
        private byte _profileReady;
        private float _carrierRadius;
        private float _asteroidRadius;

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
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _vesselMovementLookup = state.GetComponentLookup<VesselMovement>(false);
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
            _transformLookup.Update(ref state);
            _vesselMovementLookup.Update(ref state);

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

                var targetPosition = action.TargetPosition;
                var safeDistance = 0f;
                if (_transformLookup.HasComponent(entity))
                {
                    targetPosition = ResolveSafeTarget(ref state, targetPosition, _transformLookup[entity].Position, out safeDistance);
                }

                var moveTarget = GetOrCreateMoveTarget(ref state, action.FleetId, targetPosition, tick);

                if (_carrierLookup.HasComponent(entity))
                {
                    var carrier = _carrierLookup.GetRefRW(entity);
                    carrier.ValueRW.PatrolCenter = targetPosition;
                    if (safeDistance > 0f)
                    {
                        carrier.ValueRW.ArrivalDistance = math.max(carrier.ValueRO.ArrivalDistance, safeDistance);
                    }
                }

                if (_patrolLookup.HasComponent(entity))
                {
                    var patrol = _patrolLookup.GetRefRW(entity);
                    patrol.ValueRW.CurrentWaypoint = targetPosition;
                    patrol.ValueRW.WaitTimer = 0f;
                }

                if (_movementLookup.HasComponent(entity))
                {
                    var movement = _movementLookup.GetRefRW(entity);
                    movement.ValueRW.TargetPosition = targetPosition;
                    if (safeDistance > 0f)
                    {
                        var arrivalThreshold = movement.ValueRO.ArrivalThreshold;
                        if (arrivalThreshold <= 0f || arrivalThreshold > 1f)
                        {
                            arrivalThreshold = 1f;
                        }
                        movement.ValueRW.ArrivalThreshold = arrivalThreshold;
                    }
                    else
                    {
                        movement.ValueRW.ArrivalThreshold = math.max(1f, movement.ValueRO.ArrivalThreshold);
                    }
                }

                if (_vesselMovementLookup.HasComponent(entity) && safeDistance > 0f)
                {
                    var vesselMovement = _vesselMovementLookup.GetRefRW(entity);
                    vesselMovement.ValueRW.ArrivalDistance = math.max(vesselMovement.ValueRO.ArrivalDistance, safeDistance);
                }

                if (_intentLookup.HasComponent(entity))
                {
                    var intent = _intentLookup[entity];
                    intent.Mode = IntentMode.MoveTo;
                    intent.TargetEntity = moveTarget;
                    intent.TargetPosition = targetPosition;
                    intent.TriggeringInterrupt = InterruptType.ObjectiveChanged;
                    intent.IntentSetTick = tick;
                    intent.Priority = InterruptPriority.High;
                    intent.IsValid = 1;
                    _intentLookup[entity] = intent;
                }
            }
        }

        private float3 ResolveSafeTarget(ref SystemState state, float3 targetPosition, float3 fleetPosition, out float safeDistance)
        {
            safeDistance = 0f;
            if (!TryResolveAsteroidTarget(ref state, targetPosition, out var asteroidPosition))
            {
                return targetPosition;
            }

            EnsureProfileRadii(ref state);
            var direction = math.normalizesafe(asteroidPosition - fleetPosition, new float3(1f, 0f, 0f));
            safeDistance = _profileReady != 0
                ? _carrierRadius + _asteroidRadius + 0.5f
                : 16f;
            return asteroidPosition - direction * safeDistance;
        }

        private void EnsureProfileRadii(ref SystemState state)
        {
            if (_profileReady != 0)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<PhysicsColliderProfileComponent>(out var profileComponent) ||
                !profileComponent.Profile.IsCreated)
            {
                return;
            }

            ref var entries = ref profileComponent.Profile.Value.Entries;
            if (!PhysicsColliderProfileHelpers.TryGetSpec(ref entries, Space4XRenderKeys.Carrier, out var carrierSpec))
            {
                return;
            }

            if (!PhysicsColliderProfileHelpers.TryGetSpec(ref entries, Space4XRenderKeys.Asteroid, out var asteroidSpec))
            {
                return;
            }

            _carrierRadius = ResolveRadius(carrierSpec);
            _asteroidRadius = ResolveRadius(asteroidSpec);
            if (_carrierRadius <= 0f || _asteroidRadius <= 0f)
            {
                return;
            }

            _profileReady = 1;
        }

        private static float ResolveRadius(in PhysicsColliderSpec spec)
        {
            return spec.Shape switch
            {
                PhysicsColliderShape.Box => math.cmax(spec.Dimensions) * 0.5f,
                PhysicsColliderShape.Capsule => spec.Dimensions.x,
                _ => spec.Dimensions.x
            };
        }

        private bool TryResolveAsteroidTarget(ref SystemState state, float3 targetPosition, out float3 asteroidPosition)
        {
            var found = false;
            var bestSq = 0f;
            var bestPos = float3.zero;

            foreach (var (_, transform) in SystemAPI.Query<RefRO<Asteroid>, RefRO<LocalTransform>>().WithNone<Prefab>())
            {
                var pos = transform.ValueRO.Position;
                var distSq = math.lengthsq(pos - targetPosition);
                if (!found || distSq < bestSq)
                {
                    bestSq = distSq;
                    bestPos = pos;
                    found = true;
                }
            }

            asteroidPosition = bestPos;
            return found && bestSq <= 0.25f;
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
