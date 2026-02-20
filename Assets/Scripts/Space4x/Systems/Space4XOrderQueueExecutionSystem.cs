using PureDOTS.Input;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Interrupts;
using Space4X.Registry;
using Space4X.Runtime;
using Space4X.Systems.AI;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Systems
{
    /// <summary>
    /// Consumes queued RTS orders and maps them into intent/movement components that vessel AI systems execute.
    /// </summary>
    [UpdateInGroup(typeof(Space4XTransportAISystemGroup))]
    [UpdateAfter(typeof(Space4XCarrierIntentBridgeSystem))]
    [UpdateAfter(typeof(Space4XVesselAICommandBridgeSystem))]
    public partial struct Space4XOrderQueueExecutionSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<VesselMovement> _movementLookup;
        private ComponentLookup<VesselAIState> _aiStateLookup;
        private ComponentLookup<EntityIntent> _intentLookup;
        private ComponentLookup<MovementCommand> _movementCommandLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _movementLookup = state.GetComponentLookup<VesselMovement>(true);
            _aiStateLookup = state.GetComponentLookup<VesselAIState>(false);
            _intentLookup = state.GetComponentLookup<EntityIntent>(false);
            _movementCommandLookup = state.GetComponentLookup<MovementCommand>(false);
        }

        public void OnUpdate(ref SystemState state)
        {
            _transformLookup.Update(ref state);
            _movementLookup.Update(ref state);
            _aiStateLookup.Update(ref state);
            _intentLookup.Update(ref state);
            _movementCommandLookup.Update(ref state);

            uint tick = SystemAPI.GetSingleton<TimeState>().Tick;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (orders, entity) in SystemAPI.Query<DynamicBuffer<OrderQueueElement>>().WithEntityAccess())
            {
                if (orders.Length == 0)
                {
                    continue;
                }

                if (!_transformLookup.HasComponent(entity))
                {
                    orders.Clear();
                    continue;
                }

                while (orders.Length > 0)
                {
                    var queuedOrder = orders[0].Order;
                    if (!IsOrderCompleted(ref state, entity, queuedOrder))
                    {
                        break;
                    }

                    orders.RemoveAt(0);
                }

                if (orders.Length == 0)
                {
                    continue;
                }

                var activeOrder = orders[0].Order;
                var intent = BuildIntentFromOrder(activeOrder, tick);
                if (_intentLookup.HasComponent(entity))
                {
                    _intentLookup[entity] = intent;
                }
                else
                {
                    ecb.AddComponent(entity, intent);
                }

                float arrivalThreshold = ResolveArrivalThreshold(entity);
                if (_movementCommandLookup.HasComponent(entity))
                {
                    var movementCommand = _movementCommandLookup[entity];
                    movementCommand.TargetPosition = activeOrder.TargetPosition;
                    movementCommand.ArrivalThreshold = arrivalThreshold;
                    _movementCommandLookup[entity] = movementCommand;
                }
                else
                {
                    ecb.AddComponent(entity, new MovementCommand
                    {
                        TargetPosition = activeOrder.TargetPosition,
                        ArrivalThreshold = arrivalThreshold
                    });
                }

                if (_aiStateLookup.HasComponent(entity))
                {
                    var aiState = _aiStateLookup[entity];
                    aiState.TargetEntity = activeOrder.TargetEntity;
                    aiState.TargetPosition = activeOrder.TargetPosition;
                    aiState.StateTimer = 0f;
                    aiState.StateStartTick = tick;
                    MapOrderToAiState(activeOrder, ref aiState);
                    _aiStateLookup[entity] = aiState;
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private bool IsOrderCompleted(ref SystemState state, Entity entity, Order order)
        {
            if (order.TargetEntity != Entity.Null)
            {
                if (!state.EntityManager.Exists(order.TargetEntity))
                {
                    return true;
                }

                // Track entity-target orders until target is gone.
                if (order.Kind == OrderKind.Attack || order.Kind == OrderKind.Harvest)
                {
                    return false;
                }
            }

            float3 currentPosition = _transformLookup[entity].Position;
            float arrival = ResolveArrivalThreshold(entity);
            float3 toTarget = order.TargetPosition - currentPosition;
            return math.lengthsq(toTarget) <= arrival * arrival;
        }

        private float ResolveArrivalThreshold(Entity entity)
        {
            if (_movementLookup.HasComponent(entity))
            {
                return math.max(0.25f, _movementLookup[entity].ArrivalDistance);
            }

            return 1f;
        }

        private static EntityIntent BuildIntentFromOrder(Order order, uint tick)
        {
            var intentMode = order.Kind switch
            {
                OrderKind.Move => IntentMode.MoveTo,
                OrderKind.Attack => IntentMode.Attack,
                OrderKind.Harvest => IntentMode.Gather,
                _ => IntentMode.ExecuteOrder
            };

            var priority = order.Kind == OrderKind.Attack
                ? InterruptPriority.High
                : InterruptPriority.Normal;

            return new EntityIntent
            {
                Mode = intentMode,
                TargetEntity = order.TargetEntity,
                TargetPosition = order.TargetPosition,
                TriggeringInterrupt = InterruptType.NewOrder,
                IntentSetTick = tick,
                Priority = priority,
                IsValid = 1
            };
        }

        private static void MapOrderToAiState(Order order, ref VesselAIState aiState)
        {
            switch (order.Kind)
            {
                case OrderKind.Harvest:
                    aiState.CurrentGoal = VesselAIState.Goal.Mining;
                    aiState.CurrentState = VesselAIState.State.MovingToTarget;
                    break;

                case OrderKind.Move:
                    aiState.CurrentGoal = VesselAIState.Goal.Patrol;
                    aiState.CurrentState = VesselAIState.State.MovingToTarget;
                    break;

                case OrderKind.Attack:
                    aiState.CurrentGoal = VesselAIState.Goal.Patrol;
                    aiState.CurrentState = VesselAIState.State.MovingToTarget;
                    break;

                default:
                    aiState.CurrentGoal = VesselAIState.Goal.Patrol;
                    aiState.CurrentState = VesselAIState.State.MovingToTarget;
                    break;
            }
        }
    }
}
