using PureDOTS.Runtime.AI;
using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Bridges the generic AI pipeline (AIUtilityState/AICommand) to villager-specific goals.
    /// Reads AI decisions and translates them into VillagerAIState.Goal values.
    /// </summary>
    /// <remarks>
    /// This system runs before VillagerAISystem to provide AI pipeline decisions.
    /// Villagers with AIUtilityState and VillagerAIUtilityBinding will have their goals
    /// set from the AI pipeline. Villagers without these components fall back to
    /// the simple need-based logic in VillagerAISystem.
    /// </remarks>
    [BurstCompile]
    [UpdateInGroup(typeof(VillagerSystemGroup))]
    [UpdateBefore(typeof(VillagerAISystem))]
    public partial struct VillagerAIPipelineBridgeSystem : ISystem
    {
        private BufferLookup<AICommand> _commandLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _commandLookup = state.GetBufferLookup<AICommand>(isReadOnly: true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            _commandLookup.Update(ref state);

            // Find the AI command queue entity
            Entity commandQueueEntity = Entity.Null;
            foreach (var (_, entity) in SystemAPI.Query<RefRO<AICommandQueueTag>>().WithEntityAccess())
            {
                commandQueueEntity = entity;
                break;
            }

            // Process villagers with AI pipeline bindings
            var job = new BridgeAIToVillagerGoalsJob
            {
                CurrentTick = timeState.Tick,
                CommandQueueEntity = commandQueueEntity,
                CommandLookup = _commandLookup
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        public partial struct BridgeAIToVillagerGoalsJob : IJobEntity
        {
            public uint CurrentTick;
            public Entity CommandQueueEntity;
            [ReadOnly] public BufferLookup<AICommand> CommandLookup;

            public void Execute(
                Entity entity,
                ref VillagerAIState aiState,
                ref VillagerAIPipelineBridgeState bridgeState,
                in AIUtilityState utilityState,
                in VillagerAIUtilityBinding binding)
            {
                // Check if utility evaluation has updated since last bridge
                if (utilityState.LastEvaluationTick <= bridgeState.LastBridgedTick)
                {
                    return;
                }

                // Map the AI action index to a villager goal
                var actionIndex = utilityState.BestActionIndex;
                var goal = VillagerAIState.Goal.None;

                if (actionIndex < binding.Goals.Length)
                {
                    goal = binding.Goals[actionIndex];
                }

                // Also check for any pending commands for this agent
                if (CommandQueueEntity != Entity.Null && CommandLookup.HasBuffer(CommandQueueEntity))
                {
                    var commands = CommandLookup[CommandQueueEntity];
                    for (var i = 0; i < commands.Length; i++)
                    {
                        var cmd = commands[i];
                        if (cmd.Agent != entity)
                        {
                            continue;
                        }

                        // Command takes precedence - update target
                        if (cmd.ActionIndex < binding.Goals.Length)
                        {
                            goal = binding.Goals[cmd.ActionIndex];
                        }

                        aiState.TargetEntity = cmd.TargetEntity;
                        aiState.TargetPosition = cmd.TargetPosition;
                        break;
                    }
                }

                // Only update if goal changed
                if (goal != aiState.CurrentGoal && goal != VillagerAIState.Goal.None)
                {
                    aiState.CurrentGoal = goal;
                    aiState.CurrentState = GoalToState(goal);
                    aiState.StateTimer = 0f;
                    aiState.StateStartTick = CurrentTick;
                }

                bridgeState.LastBridgedTick = utilityState.LastEvaluationTick;
                bridgeState.LastActionIndex = actionIndex;
                bridgeState.LastScore = utilityState.BestScore;
            }

            private static VillagerAIState.State GoalToState(VillagerAIState.Goal goal)
            {
                return goal switch
                {
                    VillagerAIState.Goal.SurviveHunger => VillagerAIState.State.Eating,
                    VillagerAIState.Goal.Work => VillagerAIState.State.Working,
                    VillagerAIState.Goal.Rest => VillagerAIState.State.Sleeping,
                    VillagerAIState.Goal.Flee => VillagerAIState.State.Fleeing,
                    VillagerAIState.Goal.Fight => VillagerAIState.State.Fighting,
                    VillagerAIState.Goal.Socialize => VillagerAIState.State.Idle,
                    VillagerAIState.Goal.Reproduce => VillagerAIState.State.Idle,
                    _ => VillagerAIState.State.Idle
                };
            }
        }
    }

    /// <summary>
    /// Tracks the state of the AI pipeline bridge for a villager.
    /// </summary>
    public struct VillagerAIPipelineBridgeState : IComponentData
    {
        /// <summary>
        /// Tick when the last AI decision was bridged.
        /// </summary>
        public uint LastBridgedTick;

        /// <summary>
        /// Last action index that was bridged.
        /// </summary>
        public byte LastActionIndex;

        /// <summary>
        /// Last utility score that was bridged.
        /// </summary>
        public float LastScore;

        /// <summary>
        /// Whether the AI pipeline is currently controlling this villager.
        /// </summary>
        public byte IsAIPipelineActive;

        public readonly bool IsActive => IsAIPipelineActive != 0;
    }
}

