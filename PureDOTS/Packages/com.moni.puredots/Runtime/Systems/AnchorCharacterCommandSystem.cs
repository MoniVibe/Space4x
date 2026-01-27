using PureDOTS.Runtime.Commands;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Registry;
using PureDOTS.Runtime.Rendering;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Processes anchor and unanchor commands for characters.
    /// Validates budget constraints and manages component lifecycle.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct AnchorCharacterCommandSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var currentTick = timeState.Tick;

            // Get or create budget singleton
            AnchoredCharacterBudget budget;
            Entity budgetEntity;
            if (!SystemAPI.TryGetSingletonEntity<AnchoredCharacterBudgetTag>(out budgetEntity))
            {
                budgetEntity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponent<AnchoredCharacterBudgetTag>(budgetEntity);
                budget = AnchoredCharacterBudget.Default;
                state.EntityManager.AddComponentData(budgetEntity, budget);
            }
            else
            {
                budget = SystemAPI.GetComponent<AnchoredCharacterBudget>(budgetEntity);
            }

            // Process anchor commands
            foreach (var (command, commandEntity) in SystemAPI.Query<RefRO<AnchorCharacterCommand>>()
                .WithEntityAccess())
            {
                var cmd = command.ValueRO;
                
                // Validate target exists and isn't already anchored
                if (!state.EntityManager.Exists(cmd.TargetEntity))
                {
                    ecb.DestroyEntity(commandEntity);
                    continue;
                }

                if (state.EntityManager.HasComponent<AnchoredCharacter>(cmd.TargetEntity))
                {
                    // Already anchored - skip
                    ecb.DestroyEntity(commandEntity);
                    continue;
                }

                // Validate budget (if player-specific)
                if (cmd.PlayerEntity != Entity.Null && 
                    state.EntityManager.HasBuffer<PlayerAnchoredCharacter>(cmd.PlayerEntity))
                {
                    var playerBuffer = state.EntityManager.GetBuffer<PlayerAnchoredCharacter>(cmd.PlayerEntity);
                    if (playerBuffer.Length >= budget.MaxPerPlayer)
                    {
                        // Budget exceeded - skip command
                        ecb.DestroyEntity(commandEntity);
                        continue;
                    }
                }

                // Add anchor components to target
                ecb.AddComponent(cmd.TargetEntity, new AnchoredCharacter
                {
                    Priority = cmd.Priority,
                    AnchoredBy = cmd.PlayerEntity,
                    Reason = cmd.Reason,
                    AnchoredAtTick = currentTick
                });

                ecb.AddComponent(cmd.TargetEntity, new AnchoredRenderConfig
                {
                    MinLODLevel = cmd.MinLODLevel,
                    AlwaysCastShadows = cmd.AlwaysCastShadows,
                    AlwaysRenderVFX = cmd.AlwaysRenderVFX,
                    MaxRenderDistance = cmd.MaxRenderDistance
                });

                ecb.AddComponent(cmd.TargetEntity, new AnchoredSimConfig
                {
                    TickRateDivisor = cmd.TickRateDivisor,
                    DistanceForReduced = cmd.DistanceForReduced,
                    AlwaysFullSimulation = cmd.AlwaysFullSimulation
                });

                // Update player's anchor buffer (if player-specific)
                if (cmd.PlayerEntity != Entity.Null)
                {
                    if (state.EntityManager.HasBuffer<PlayerAnchoredCharacter>(cmd.PlayerEntity))
                    {
                        var playerBuffer = state.EntityManager.GetBuffer<PlayerAnchoredCharacter>(cmd.PlayerEntity);
                        playerBuffer.Add(new PlayerAnchoredCharacter
                        {
                            AnchoredEntity = cmd.TargetEntity,
                            AnchoredAtTick = currentTick,
                            Priority = cmd.Priority
                        });
                    }
                }

                // Update global count
                budget.TotalCount++;
                
                // Destroy processed command
                ecb.DestroyEntity(commandEntity);
            }

            // Process unanchor commands
            foreach (var (command, commandEntity) in SystemAPI.Query<RefRO<UnanchorCharacterCommand>>()
                .WithEntityAccess())
            {
                var cmd = command.ValueRO;

                // Validate target exists and is anchored
                if (!state.EntityManager.Exists(cmd.TargetEntity) ||
                    !state.EntityManager.HasComponent<AnchoredCharacter>(cmd.TargetEntity))
                {
                    ecb.DestroyEntity(commandEntity);
                    continue;
                }

                var anchor = state.EntityManager.GetComponentData<AnchoredCharacter>(cmd.TargetEntity);

                // Validate permissions (only anchoring player or shared anchors can unanchor)
                if (anchor.AnchoredBy != Entity.Null && anchor.AnchoredBy != cmd.PlayerEntity)
                {
                    // Different player owns this anchor - skip
                    ecb.DestroyEntity(commandEntity);
                    continue;
                }

                // Remove anchor components
                ecb.RemoveComponent<AnchoredCharacter>(cmd.TargetEntity);
                ecb.RemoveComponent<AnchoredRenderConfig>(cmd.TargetEntity);
                ecb.RemoveComponent<AnchoredSimConfig>(cmd.TargetEntity);

                // Update player's anchor buffer
                if (anchor.AnchoredBy != Entity.Null && 
                    state.EntityManager.HasBuffer<PlayerAnchoredCharacter>(anchor.AnchoredBy))
                {
                    var playerBuffer = state.EntityManager.GetBuffer<PlayerAnchoredCharacter>(anchor.AnchoredBy);
                    for (int i = playerBuffer.Length - 1; i >= 0; i--)
                    {
                        if (playerBuffer[i].AnchoredEntity == cmd.TargetEntity)
                        {
                            playerBuffer.RemoveAt(i);
                            break;
                        }
                    }
                }

                // Update global count
                budget.TotalCount = math.max(0, budget.TotalCount - 1);

                // Destroy processed command
                ecb.DestroyEntity(commandEntity);
            }

            // Write back budget changes
            SystemAPI.SetComponent(budgetEntity, budget);

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}

