using PureDOTS.Runtime.Components;
using Space4X.Registry;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Systems.AI
{
    /// <summary>
    /// Mirrors existing mining/target state into explicit routine phases for headless AI validation.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4X.Mining.Space4XMiningSystem))]
    public partial struct Space4XRoutinePlannerSystem : ISystem
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
            if (timeState.IsPaused)
            {
                return;
            }

            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (transform, entity) in SystemAPI.Query<RefRO<LocalTransform>>()
                         .WithAny<Carrier, MiningVessel>()
                         .WithEntityAccess())
            {
                var goal = Space4XRoutineGoal.Standby;
                var phase = Space4XRoutinePhase.Idle;
                var directive = Space4XRoutineDirectiveKind.HoldPosition;
                var targetEntity = Entity.Null;
                var targetPosition = float3.zero;

                if (SystemAPI.HasComponent<MiningVessel>(entity))
                {
                    goal = Space4XRoutineGoal.Mining;
                    if (SystemAPI.HasComponent<MiningState>(entity))
                    {
                        var miningState = SystemAPI.GetComponentRO<MiningState>(entity).ValueRO;
                        targetEntity = miningState.ActiveTarget;
                        phase = miningState.Phase switch
                        {
                            MiningPhase.Undocking => Space4XRoutinePhase.Depart,
                            MiningPhase.ApproachTarget => Space4XRoutinePhase.Transit,
                            MiningPhase.Latching => Space4XRoutinePhase.Approach,
                            MiningPhase.Mining => Space4XRoutinePhase.Work,
                            MiningPhase.Detaching => Space4XRoutinePhase.Return,
                            MiningPhase.ReturnApproach => Space4XRoutinePhase.Return,
                            MiningPhase.Docking => Space4XRoutinePhase.Dock,
                            _ => Space4XRoutinePhase.Idle
                        };
                        directive = miningState.Phase switch
                        {
                            MiningPhase.Undocking => Space4XRoutineDirectiveKind.Undock,
                            MiningPhase.ApproachTarget => Space4XRoutineDirectiveKind.ApproachTarget,
                            MiningPhase.Latching => Space4XRoutineDirectiveKind.ApproachTarget,
                            MiningPhase.Mining => Space4XRoutineDirectiveKind.Mine,
                            MiningPhase.Detaching => Space4XRoutineDirectiveKind.ReturnToCarrier,
                            MiningPhase.ReturnApproach => Space4XRoutineDirectiveKind.ReturnToCarrier,
                            MiningPhase.Docking => Space4XRoutineDirectiveKind.Dock,
                            _ => Space4XRoutineDirectiveKind.HoldPosition
                        };
                    }
                }
                else if (SystemAPI.HasComponent<Carrier>(entity))
                {
                    if (SystemAPI.HasComponent<CarrierMiningTarget>(entity))
                    {
                        var target = SystemAPI.GetComponentRO<CarrierMiningTarget>(entity).ValueRO;
                        targetEntity = target.TargetEntity;
                        targetPosition = target.TargetPosition;
                        if (targetEntity != Entity.Null && SystemAPI.HasComponent<LocalTransform>(targetEntity))
                        {
                            targetPosition = SystemAPI.GetComponentRO<LocalTransform>(targetEntity).ValueRO.Position;
                        }

                        goal = Space4XRoutineGoal.MiningSupport;
                        directive = Space4XRoutineDirectiveKind.ApproachTarget;
                        phase = Space4XRoutinePhase.Approach;

                        if (math.lengthsq(targetPosition) > 0f)
                        {
                            var distance = math.length(targetPosition - transform.ValueRO.Position);
                            if (distance <= 22f)
                            {
                                directive = Space4XRoutineDirectiveKind.HoldPosition;
                                phase = Space4XRoutinePhase.Hold;
                            }
                        }
                    }
                }

                if (!SystemAPI.HasComponent<Space4XRoutineState>(entity))
                {
                    ecb.AddComponent(entity, new Space4XRoutineState
                    {
                        Goal = goal,
                        Phase = phase,
                        Directive = directive,
                        TargetEntity = targetEntity,
                        TargetPosition = targetPosition,
                        PhaseStartTick = timeState.Tick,
                        LastDirectiveTick = timeState.Tick
                    });
                }
                else
                {
                    var routine = SystemAPI.GetComponentRW<Space4XRoutineState>(entity);
                    if (routine.ValueRO.Phase != phase)
                    {
                        routine.ValueRW.Phase = phase;
                        routine.ValueRW.PhaseStartTick = timeState.Tick;
                    }

                    if (routine.ValueRO.Directive != directive)
                    {
                        routine.ValueRW.Directive = directive;
                        routine.ValueRW.LastDirectiveTick = timeState.Tick;
                    }

                    routine.ValueRW.Goal = goal;
                    routine.ValueRW.TargetEntity = targetEntity;
                    routine.ValueRW.TargetPosition = targetPosition;
                }
            }
        }
    }
}
