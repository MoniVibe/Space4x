using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using PureDOTS.Runtime.Components;

namespace Space4X.Presentation
{
    /// <summary>
    /// Command system that reads CommandInput and writes command components to selected entities.
    /// Follows PureDOTS canonical pattern for command handling.
    /// </summary>
    [UpdateInGroup(typeof(Unity.Entities.PresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XSelectionSystem))]
    // NOTE: Not Burst compiled because we construct FixedString64Bytes from managed strings (string interpolation)
    // This is fine - command handling is presentation/UI logic, not a hot inner loop
    public partial struct Space4XCommandSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<Asteroid> _asteroidLookup;
        private BufferLookup<Space4X.Presentation.AggregateMemberElement> _aggregateMembersLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CommandInput>();
            state.RequireForUpdate<SelectionState>();
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _asteroidLookup = state.GetComponentLookup<Asteroid>(true);
            _aggregateMembersLookup = state.GetBufferLookup<Space4X.Presentation.AggregateMemberElement>(true);
        }

        // NOTE: runs in managed mode because we construct FixedString64Bytes from string (e.g., $"Move_{entity}_{tick}")
        public void OnUpdate(ref SystemState state)
        {
            var commandInput = SystemAPI.GetSingleton<CommandInput>();
            var selectionState = SystemAPI.GetSingleton<SelectionState>();

            if (selectionState.PrimarySelected == Entity.Null)
            {
                return;
            }

            _transformLookup.Update(ref state);
            _asteroidLookup.Update(ref state);
            _aggregateMembersLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            uint currentTick = 0;
            if (SystemAPI.TryGetSingleton<TimeState>(out var timeState))
            {
                currentTick = timeState.Tick;
            }

            // Handle move command
            if (commandInput.IssueMoveCommand && 
                math.lengthsq(commandInput.CommandTargetPosition) > 0.001f)
            {
                var selectedEntity = selectionState.PrimarySelected;
                
                // Write PlayerCommand
                ecb.SetComponent(selectedEntity, new PlayerCommand
                {
                    CommandType = PlayerCommandType.Move,
                    TargetEntity = commandInput.CommandTargetEntity,
                    TargetPosition = commandInput.CommandTargetPosition,
                    IssuedTick = currentTick,
                    CommandId = new FixedString64Bytes($"Move_{selectedEntity.Index}_{currentTick}")
                });

                // Write MovementCommand for sim
                ecb.SetComponent(selectedEntity, new MovementCommand
                {
                    TargetPosition = commandInput.CommandTargetPosition,
                    ArrivalThreshold = 5f
                });

                // Write CommandFeedback for visual cue
                ecb.SetComponent(selectedEntity, new CommandFeedback
                {
                    CommandType = PlayerCommandType.Move,
                    TargetPosition = commandInput.CommandTargetPosition,
                    FeedbackTimer = 3f,
                    FeedbackDuration = 3f,
                    FeedbackColor = new float4(0.2f, 1f, 0.2f, 1f) // Green
                });

                // For fleets, write commands to all fleet members
                if (_aggregateMembersLookup.HasBuffer(selectedEntity))
                {
                    DynamicBuffer<Space4X.Presentation.AggregateMemberElement> members = _aggregateMembersLookup[selectedEntity];
                    for (int i = 0; i < members.Length; i++)
                    {
                        var member = members[i];
                        Entity memberEntity = member.MemberEntity;
                        
                        if (memberEntity != Entity.Null && SystemAPI.HasComponent<Carrier>(memberEntity))
                        {
                            ecb.SetComponent(memberEntity, new MovementCommand
                            {
                                TargetPosition = commandInput.CommandTargetPosition,
                                ArrivalThreshold = 5f
                            });
                        }
                    }
                }
            }

            // Handle attack command
            if (commandInput.IssueAttackCommand && commandInput.CommandTargetEntity != Entity.Null)
            {
                var selectedEntity = selectionState.PrimarySelected;
                float3 targetPosition = commandInput.CommandTargetPosition;

                // Get target position from entity if available
                if (_transformLookup.HasComponent(commandInput.CommandTargetEntity))
                {
                    targetPosition = _transformLookup[commandInput.CommandTargetEntity].Position;
                }

                // Write PlayerCommand
                ecb.SetComponent(selectedEntity, new PlayerCommand
                {
                    CommandType = PlayerCommandType.Attack,
                    TargetEntity = commandInput.CommandTargetEntity,
                    TargetPosition = targetPosition,
                    IssuedTick = currentTick,
                    CommandId = new FixedString64Bytes($"Attack_{selectedEntity.Index}_{currentTick}")
                });

                // Write MovementCommand toward target (placeholder for attack command)
                ecb.SetComponent(selectedEntity, new MovementCommand
                {
                    TargetPosition = targetPosition,
                    ArrivalThreshold = 10f
                });

                // Write CommandFeedback
                ecb.SetComponent(selectedEntity, new CommandFeedback
                {
                    CommandType = PlayerCommandType.Attack,
                    TargetPosition = targetPosition,
                    FeedbackTimer = 3f,
                    FeedbackDuration = 3f,
                    FeedbackColor = new float4(1f, 0.2f, 0.2f, 1f) // Red
                });
            }

            // Handle mine command
            if (commandInput.IssueMineCommand && commandInput.CommandTargetEntity != Entity.Null)
            {
                var selectedEntity = selectionState.PrimarySelected;
                
                if (_asteroidLookup.HasComponent(commandInput.CommandTargetEntity))
                {
                    var asteroid = _asteroidLookup[commandInput.CommandTargetEntity];
                    float3 targetPosition = commandInput.CommandTargetPosition;
                    
                    if (_transformLookup.HasComponent(commandInput.CommandTargetEntity))
                    {
                        targetPosition = _transformLookup[commandInput.CommandTargetEntity].Position;
                    }

                    // Write PlayerCommand
                    ecb.SetComponent(selectedEntity, new PlayerCommand
                    {
                        CommandType = PlayerCommandType.Mine,
                        TargetEntity = commandInput.CommandTargetEntity,
                        TargetPosition = targetPosition,
                        IssuedTick = currentTick,
                        CommandId = new FixedString64Bytes($"Mine_{selectedEntity.Index}_{currentTick}")
                    });

                    // Write MiningOrder for sim
                    ecb.SetComponent(selectedEntity, new MiningOrder
                    {
                        ResourceId = new FixedString64Bytes("space4x.resource.minerals"), // Default, would read from asteroid
                        Source = MiningOrderSource.Scripted,
                        Status = MiningOrderStatus.Pending,
                        TargetEntity = commandInput.CommandTargetEntity,
                        PreferredTarget = commandInput.CommandTargetEntity,
                        IssuedTick = currentTick
                    });

                    // Write MovementCommand toward asteroid
                    ecb.SetComponent(selectedEntity, new MovementCommand
                    {
                        TargetPosition = targetPosition,
                        ArrivalThreshold = 15f
                    });

                    // Write CommandFeedback
                    ecb.SetComponent(selectedEntity, new CommandFeedback
                    {
                        CommandType = PlayerCommandType.Mine,
                        TargetPosition = targetPosition,
                        FeedbackTimer = 3f,
                        FeedbackDuration = 3f,
                        FeedbackColor = new float4(1f, 0.6f, 0.2f, 1f) // Orange
                    });
                }
            }

            // Handle cancel command
            if (commandInput.CancelCommand)
            {
                var selectedEntity = selectionState.PrimarySelected;
                
                if (SystemAPI.HasComponent<MovementCommand>(selectedEntity))
                {
                    ecb.RemoveComponent<MovementCommand>(selectedEntity);
                }
                if (SystemAPI.HasComponent<MiningOrder>(selectedEntity))
                {
                    ecb.RemoveComponent<MiningOrder>(selectedEntity);
                }
                if (SystemAPI.HasComponent<PlayerCommand>(selectedEntity))
                {
                    ecb.RemoveComponent<PlayerCommand>(selectedEntity);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}

