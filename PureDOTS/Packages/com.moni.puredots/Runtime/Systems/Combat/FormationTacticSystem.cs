using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Formation;
using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Combat
{
    /// <summary>
    /// Executes formation tactics and manages tactic state transitions.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct FormationTacticSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var currentTick = timeState.Tick;
            var deltaTime = SystemAPI.Time.DeltaTime;

            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(false);

            // Process tactic execution requests
            foreach (var (request, tactic, formationState, entity) in SystemAPI.Query<
                RefRO<TacticExecutionRequest>,
                RefRW<FormationTactic>,
                RefRO<FormationState>>()
                .WithEntityAccess())
            {
                FormationTacticService.ExecuteTactic(
                    ref tactic.ValueRW,
                    request.ValueRO.RequestedTactic,
                    request.ValueRO.TargetPosition,
                    request.ValueRO.TargetEntity,
                    currentTick);

                if (request.ValueRO.Immediate)
                {
                    tactic.ValueRW.State = TacticState.Executing;
                }

                ecb.RemoveComponent<TacticExecutionRequest>(entity);
            }

            // Update tactic execution
            foreach (var (tactic, formationState, transform, entity) in SystemAPI.Query<
                RefRW<FormationTactic>,
                RefRO<FormationState>,
                RefRW<LocalTransform>>()
                .WithEntityAccess())
            {
                if (tactic.ValueRO.TacticType == FormationTacticType.None)
                    continue;

                float timeSinceStart = (currentTick - tactic.ValueRO.TacticStartTick) * deltaTime;

                // State transitions
                switch (tactic.ValueRO.State)
                {
                    case TacticState.Preparing:
                        // Prepare for 1 second
                        if (timeSinceStart > 1f)
                        {
                            tactic.ValueRW.State = TacticState.Executing;
                        }
                        break;

                    case TacticState.Executing:
                        // Execute movement pattern
                        if (tactic.ValueRO.TacticType != FormationTacticType.Hold)
                        {
                            float3 movement = FormationTacticService.CalculateTacticMovement(
                                tactic.ValueRO.TacticType,
                                transform.ValueRO.Position,
                                tactic.ValueRO.TargetPosition,
                                deltaTime);

                            transform.ValueRW.Position += movement * deltaTime;

                            // Check if reached target
                            float distanceToTarget = math.distance(
                                transform.ValueRO.Position,
                                tactic.ValueRO.TargetPosition);

                            if (distanceToTarget < 2f)
                            {
                                tactic.ValueRW.State = TacticState.Completing;
                            }
                        }
                        else
                        {
                            // Hold: stay in position
                            tactic.ValueRW.State = TacticState.Completing;
                        }
                        break;

                    case TacticState.Completing:
                        // Complete after 0.5 seconds
                        if (timeSinceStart > 2f)
                        {
                            tactic.ValueRW.State = TacticState.Idle;
                            tactic.ValueRW.TacticType = FormationTacticType.None;
                        }
                        break;

                    case TacticState.Failed:
                        // Reset after failure
                        if (timeSinceStart > 1f)
                        {
                            tactic.ValueRW.State = TacticState.Idle;
                            tactic.ValueRW.TacticType = FormationTacticType.None;
                        }
                        break;
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}

