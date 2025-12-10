using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Presentation
{
    /// <summary>
    /// System that bridges player commands from input to PureDOTS sim commands.
    /// Reads CommandInput and SelectionState, writes PlayerCommand and PureDOTS command components.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XSelectionSystem))]
    public partial struct Space4XCommandBridgeSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<Asteroid> _asteroidLookup;
        private ComponentLookup<Carrier> _carrierLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CommandInput>();
            state.RequireForUpdate<SelectionState>();
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _asteroidLookup = state.GetComponentLookup<Asteroid>(true);
            _carrierLookup = state.GetComponentLookup<Carrier>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<CommandInput>(out var commandInput))
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<SelectionState>(out var selectionState))
            {
                return;
            }

            if (selectionState.SelectedCount == 0)
            {
                return;
            }

            _transformLookup.Update(ref state);
            _asteroidLookup.Update(ref state);
            _carrierLookup.Update(ref state);

            // Presentation can be driven by wall-clock simulation time
            double currentTime = SystemAPI.Time.ElapsedTime;
            // For command IDs, use a time-based identifier instead of tick
            uint commandIdSuffix = (uint)(currentTime * 1000) % 1000000; // Use milliseconds as suffix

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Handle move command (right-click on ground)
            if (commandInput.IssueMoveCommand)
            {
                // Get target position from selection input (would need to convert screen to world)
                // For now, use a placeholder - actual implementation would use camera raycast
                float3 targetPosition = float3.zero; // Placeholder

                // Issue move command to all selected carriers
                foreach (var (selected, carrier, entity) in SystemAPI
                             .Query<RefRO<SelectedTag>, RefRO<Carrier>>()
                             .WithEntityAccess())
                {
                    var command = new PlayerCommand
                    {
                        CommandType = PlayerCommandType.Move,
                        TargetEntity = Entity.Null,
                        TargetPosition = targetPosition,
                        IssuedTick = 0, // Not used for presentation
                        CommandId = new FixedString64Bytes($"Move_{entity.Index}_{commandIdSuffix}")
                    };

                    ecb.AddComponent(entity, command);

                    // Write PureDOTS MovementCommand
                    ecb.AddComponent(entity, new MovementCommand
                    {
                        TargetPosition = targetPosition,
                        ArrivalThreshold = 5f
                    });

                    // Create command feedback
                    ecb.AddComponent(entity, new CommandFeedback
                    {
                        CommandType = PlayerCommandType.Move,
                        TargetPosition = targetPosition,
                        FeedbackTimer = 3f,
                        FeedbackDuration = 3f,
                        FeedbackColor = new float4(0.2f, 1f, 0.2f, 1f) // Green
                    });
                }
            }

            // Handle attack command (right-click on enemy)
            if (commandInput.IssueAttackCommand)
            {
                // Get target entity from selection input
                Entity targetEntity = Entity.Null; // Placeholder - would come from input

                if (targetEntity != Entity.Null)
                {
                    // Issue attack command to all selected carriers
                    foreach (var (selected, carrier, entity) in SystemAPI
                                 .Query<RefRO<SelectedTag>, RefRO<Carrier>>()
                                 .WithEntityAccess())
                    {
                        var command = new PlayerCommand
                        {
                            CommandType = PlayerCommandType.Attack,
                            TargetEntity = targetEntity,
                            TargetPosition = float3.zero,
                            IssuedTick = 0, // Not used for presentation
                            CommandId = new FixedString64Bytes($"Attack_{entity.Index}_{commandIdSuffix}")
                        };

                        ecb.AddComponent(entity, command);

                        // Write PureDOTS InterceptRequest (if available)
                        // This would go into the intercept queue buffer
                    }
                }
            }

            // Handle mine command (right-click on asteroid)
            if (commandInput.IssueMineCommand)
            {
                // Get target asteroid from selection input
                Entity targetAsteroid = Entity.Null; // Placeholder

                if (targetAsteroid != Entity.Null && _asteroidLookup.HasComponent(targetAsteroid))
                {
                    var asteroid = _asteroidLookup[targetAsteroid];

                    // Issue mine command to all selected carriers
                    foreach (var (selected, carrier, entity) in SystemAPI
                                 .Query<RefRO<SelectedTag>, RefRO<Carrier>>()
                                 .WithEntityAccess())
                    {
                        var command = new PlayerCommand
                        {
                            CommandType = PlayerCommandType.Mine,
                            TargetEntity = targetAsteroid,
                            TargetPosition = float3.zero,
                            IssuedTick = 0, // Not used for presentation
                            CommandId = new FixedString64Bytes($"Mine_{entity.Index}_{commandIdSuffix}")
                        };

                        ecb.AddComponent(entity, command);

                        // Write PureDOTS MiningOrder
                        ecb.AddComponent(entity, new MiningOrder
                        {
                            TargetEntity = targetAsteroid,
                            ResourceId = new FixedString64Bytes("space4x.resource.minerals"), // Default
                            Source = MiningOrderSource.Scripted,
                            Status = MiningOrderStatus.Pending
                        });

                        // Create command feedback
                        if (_transformLookup.HasComponent(targetAsteroid))
                        {
                            var asteroidTransform = _transformLookup[targetAsteroid];
                            ecb.AddComponent(entity, new CommandFeedback
                            {
                                CommandType = PlayerCommandType.Mine,
                                TargetPosition = asteroidTransform.Position,
                                FeedbackTimer = 3f,
                                FeedbackDuration = 3f,
                                FeedbackColor = new float4(1f, 0.6f, 0.2f, 1f) // Orange
                            });
                        }
                    }
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// System that updates command feedback visuals (markers, lines).
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XCommandBridgeSystem))]
    public partial struct Space4XCommandFeedbackSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CommandFeedback>();
        }

        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Update command feedback timers
            foreach (var (feedback, entity) in SystemAPI.Query<RefRW<CommandFeedback>>().WithEntityAccess())
            {
                feedback.ValueRW.FeedbackTimer -= deltaTime;

                // Remove feedback when timer expires
                if (feedback.ValueRW.FeedbackTimer <= 0f)
                {
                    ecb.RemoveComponent<CommandFeedback>(entity);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}

