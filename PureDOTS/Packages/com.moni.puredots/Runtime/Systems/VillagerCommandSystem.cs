using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Hand;
using PureDOTS.Runtime.Time;
using PureDOTS.Runtime.Villager;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Processes player/creature commands for villagers (priority overrides, recruitment, movement).
    /// Integrates with hand/router system to respect shared interaction priorities.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(VillagerSystemGroup))]
    [UpdateAfter(typeof(VillagerAISystem))]
    public partial struct VillagerCommandSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
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

            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            // Process commands from command buffers
            foreach (var (commands, aiState, flags, entity) in SystemAPI.Query<DynamicBuffer<VillagerCommand>, RefRW<VillagerAIState>, RefRW<VillagerFlags>>()
                         .WithEntityAccess())
            {
                for (int i = commands.Length - 1; i >= 0; i--)
                {
                    var command = commands[i];

                    // Check if command is still valid (not expired)
                    var ticksSinceIssued = timeState.Tick >= command.IssuedTick
                        ? timeState.Tick - command.IssuedTick
                        : 0;
                    if (ticksSinceIssued > 600) // 10 seconds at 60Hz
                    {
                        commands.RemoveAt(i);
                        continue;
                    }

                    // Process command based on type
                    bool commandProcessed = false;
                    switch (command.Type)
                    {
                        case VillagerCommand.CommandType.MoveTo:
                            if (math.distancesq(aiState.ValueRO.TargetPosition, command.TargetPosition) > 0.01f)
                            {
                                aiState.ValueRW.TargetPosition = command.TargetPosition;
                                aiState.ValueRW.CurrentGoal = VillagerAIState.Goal.None;
                                aiState.ValueRW.CurrentState = VillagerAIState.State.Travelling;
                                commandProcessed = true;
                            }
                            break;

                        case VillagerCommand.CommandType.Attack:
                            if (command.TargetEntity != Entity.Null && state.EntityManager.Exists(command.TargetEntity))
                            {
                                aiState.ValueRW.TargetEntity = command.TargetEntity;
                                aiState.ValueRW.CurrentGoal = VillagerAIState.Goal.Fight;
                                aiState.ValueRW.CurrentState = VillagerAIState.State.Fighting;
                                flags.ValueRW.IsInCombat = true;
                                commandProcessed = true;
                            }
                            break;

                        case VillagerCommand.CommandType.Gather:
                            if (command.TargetEntity != Entity.Null && state.EntityManager.Exists(command.TargetEntity))
                            {
                                aiState.ValueRW.TargetEntity = command.TargetEntity;
                                aiState.ValueRW.CurrentGoal = VillagerAIState.Goal.Work;
                                aiState.ValueRW.CurrentState = VillagerAIState.State.Working;
                                commandProcessed = true;
                            }
                            break;

                        case VillagerCommand.CommandType.Flee:
                            aiState.ValueRW.CurrentGoal = VillagerAIState.Goal.Flee;
                            aiState.ValueRW.CurrentState = VillagerAIState.State.Fleeing;
                            commandProcessed = true;
                            break;

                        case VillagerCommand.CommandType.Guard:
                            aiState.ValueRW.CurrentGoal = VillagerAIState.Goal.None;
                            aiState.ValueRW.CurrentState = VillagerAIState.State.Idle;
                            flags.ValueRW.IsPlayerPriority = true;
                            commandProcessed = true;
                            break;
                    }

                    if (commandProcessed)
                    {
                        commands.RemoveAt(i);
                    }
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}

