using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Interrupts;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4x.Scenario
{
    /// <summary>
    /// Clears scenario MoveFleet intents once the carrier reaches its temporary target.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4X.Systems.AI.VesselMovementSystem))]
    public partial struct Space4XScenarioMoveTargetArrivalSystem : ISystem
    {
        private ComponentLookup<Space4XScenarioMoveTarget> _moveTargetLookup;
        private ComponentLookup<LocalTransform> _transformLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            _moveTargetLookup = state.GetComponentLookup<Space4XScenarioMoveTarget>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            _moveTargetLookup.Update(ref state);
            _transformLookup.Update(ref state);

            foreach (var (intent, aiState, movement, transform, entity) in SystemAPI
                         .Query<RefRW<EntityIntent>, RefRW<VesselAIState>, RefRO<VesselMovement>, RefRO<LocalTransform>>()
                         .WithEntityAccess())
            {
                if (intent.ValueRO.IsValid == 0 || intent.ValueRO.Mode != IntentMode.MoveTo)
                {
                    continue;
                }

                var target = intent.ValueRO.TargetEntity;
                if (target == Entity.Null || !_moveTargetLookup.HasComponent(target) || !_transformLookup.HasComponent(target))
                {
                    continue;
                }

                var targetPosition = _transformLookup[target].Position;
                var distance = math.distance(transform.ValueRO.Position, targetPosition);
                var arrivalDistance = movement.ValueRO.ArrivalDistance > 0f ? movement.ValueRO.ArrivalDistance : 2f;
                if (distance > arrivalDistance)
                {
                    continue;
                }

                intent.ValueRW.Mode = IntentMode.Idle;
                intent.ValueRW.IsValid = 0;
                intent.ValueRW.TargetEntity = Entity.Null;
                intent.ValueRW.TargetPosition = float3.zero;
                intent.ValueRW.TriggeringInterrupt = InterruptType.None;
                intent.ValueRW.Priority = InterruptPriority.Low;
                intent.ValueRW.IntentSetTick = timeState.Tick;

                aiState.ValueRW.CurrentGoal = VesselAIState.Goal.None;
                aiState.ValueRW.CurrentState = VesselAIState.State.Idle;
                aiState.ValueRW.TargetEntity = Entity.Null;
                aiState.ValueRW.TargetPosition = float3.zero;
                aiState.ValueRW.StateTimer = 0f;
                aiState.ValueRW.StateStartTick = timeState.Tick;
            }
        }
    }
}
