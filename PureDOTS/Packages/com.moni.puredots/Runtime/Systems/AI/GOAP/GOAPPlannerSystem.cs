using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using PureDOTS.Runtime.AI.GOAP;
using PureDOTS.Runtime.Time;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Scenarios;
using EffectiveDeltaTime = PureDOTS.Runtime.Time.EffectiveDeltaTime;

namespace PureDOTS.Runtime.Systems.AI.GOAP
{
    /// <summary>
    /// System that creates and manages GOAP action plans.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [BurstCompile]
    public partial struct GOAPPlannerSystem : ISystem
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
            // Phase 2: Enable planning in headless ScenarioRunner sims
            // Remove Record-mode-only restriction to allow planning in all sim modes
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState))
                return;
            
            // Allow planning in Record mode (normal gameplay) and in headless scenarios
            // Skip planning only in Rewind/CatchUp modes where determinism is critical
            if (rewindState.Mode == RewindMode.Rewind || rewindState.Mode == RewindMode.CatchUp)
                return;

            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint currentTick = timeState.Tick;
            float globalDelta = timeState.DeltaTime;

            // Resolve scenario entity once for Burst-compatible metrics
            Entity scenarioEntity = Entity.Null;
            if (SystemAPI.TryGetSingleton<ScenarioEntitySingleton>(out var scenarioSingleton))
            {
                scenarioEntity = scenarioSingleton.Value;
            }
            else if (SystemAPI.HasSingleton<ScenarioInfo>())
            {
                scenarioEntity = SystemAPI.GetSingletonEntity<ScenarioInfo>();
            }

            // Get metric buffer lookup
            var metricLookup = SystemAPI.GetBufferLookup<ScenarioMetricSample>(isReadOnly: false);
            metricLookup.Update(ref state);

            // Update goal insistence (use effective delta for time-aware entities)
            var effectiveDeltaLookup = SystemAPI.GetComponentLookup<EffectiveDeltaTime>(true);
            effectiveDeltaLookup.Update(ref state);
            
            foreach (var (goals, entity) in 
                SystemAPI.Query<DynamicBuffer<AIGoal>>()
                    .WithEntityAccess())
            {
                var goalsBuffer = goals;
                // Use effective delta if available, otherwise global delta
                float delta = TimeAwareHelpers.GetEffectiveDelta(effectiveDeltaLookup, entity, globalDelta);
                GOAPHelpers.UpdateGoalInsistence(ref goalsBuffer, delta);
            }

            // Create/update plans
            foreach (var (planner, goals, actions, worldState, plan, entity) in 
                SystemAPI.Query<RefRW<AIPlanner>, DynamicBuffer<AIGoal>, DynamicBuffer<AIAction>, DynamicBuffer<WorldStateFact>, DynamicBuffer<PlannedAction>>()
                    .WithEntityAccess())
            {
                var goalsBuffer = goals;
                var actionsBuffer = actions;
                var worldStateBuffer = worldState;
                var planBuffer = plan;

                // Check if replan needed
                bool needsReplan = planner.ValueRO.NeedsReplan;
                if (!needsReplan && currentTick - planner.ValueRO.PlanCreatedTick >= planner.ValueRO.ReplanInterval)
                {
                    needsReplan = true;
                }
                if (!needsReplan && planner.ValueRO.PlanConfidence < 0.3f)
                {
                    needsReplan = true;
                }

                if (needsReplan)
                {
                    // Find highest priority goal
                    if (GOAPHelpers.TryGetHighestPriorityGoal(goalsBuffer, out var bestGoal))
                    {
                        // Create plan for goal
                        int planLength = GOAPHelpers.CreateSimplePlan(
                            bestGoal,
                            actionsBuffer,
                            worldStateBuffer,
                            ref planBuffer,
                            bestGoal.TargetEntity);

                        planner.ValueRW.CurrentGoal = bestGoal.GoalId;
                        planner.ValueRW.PlanLength = (byte)planLength;
                        planner.ValueRW.PlanProgress = 0;
                        planner.ValueRW.PlanCreatedTick = currentTick;
                        planner.ValueRW.PlanConfidence = planLength > 0 ? 1f : 0f;
                        planner.ValueRW.NeedsReplan = false;

                        if (planLength > 0)
                        {
                            planner.ValueRW.CurrentAction = planBuffer[0].ActionId;
                            if (scenarioEntity != Entity.Null && metricLookup.HasBuffer(scenarioEntity))
                            {
                                ScenarioMetricsUtility.AddMetric(ref metricLookup, scenarioEntity, "goap.plans.created", 1.0);
                            }
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// System that evaluates utility options and selects best.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(GOAPPlannerSystem))]
    [BurstCompile]
    public partial struct UtilityEvaluationSystem : ISystem
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
            // Phase 2: Enable utility evaluation in headless ScenarioRunner sims
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState))
                return;
            
            // Allow utility evaluation in Record mode and headless scenarios
            if (rewindState.Mode == RewindMode.Rewind || rewindState.Mode == RewindMode.CatchUp)
                return;

            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint currentTick = timeState.Tick;

            // Evaluate utility options
            foreach (var (config, options, aiState, entity) in 
                SystemAPI.Query<RefRO<UtilityConfig>, DynamicBuffer<UtilityOption>, RefRW<AIState>>()
                    .WithEntityAccess())
            {
                var optionsBuffer = options;

                // Check evaluation interval
                bool shouldEvaluate = false;
                if (optionsBuffer.Length > 0 && currentTick - optionsBuffer[0].EvaluatedTick >= config.ValueRO.EvaluationInterval)
                {
                    shouldEvaluate = true;
                }

                if (shouldEvaluate)
                {
                    // Calculate final scores
                    uint seed = (uint)(entity.Index ^ entity.Version ^ currentTick);
                    
                    for (int i = 0; i < optionsBuffer.Length; i++)
                    {
                        var option = optionsBuffer[i];
                        option.FinalScore = GOAPHelpers.CalculateFinalScore(option, config.ValueRO, seed + (uint)i);
                        option.EvaluatedTick = currentTick;
                        optionsBuffer[i] = option;
                    }

                    // Select best option
                    if (GOAPHelpers.TryGetBestOption(optionsBuffer, config.ValueRO, out var best))
                    {
                        // Update AI state if different action
                        if (!aiState.ValueRO.CurrentState.Equals(best.OptionId))
                        {
                            aiState.ValueRW.PreviousState = aiState.ValueRO.CurrentState;
                            aiState.ValueRW.CurrentState = best.OptionId;
                            aiState.ValueRW.StateEnteredTick = currentTick;
                            aiState.ValueRW.StateUtility = best.FinalScore;
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// System that propagates directives from commanders to subordinates.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(UtilityEvaluationSystem))]
    [BurstCompile]
    public partial struct DirectiveSystem : ISystem
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
            // Phase 2: Enable directive processing in headless ScenarioRunner sims
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState))
                return;
            
            // Allow directive processing in Record mode and headless scenarios
            if (rewindState.Mode == RewindMode.Rewind || rewindState.Mode == RewindMode.CatchUp)
                return;

            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint currentTick = timeState.Tick;

            // Process directives and update utility options
            foreach (var (subordinate, directives, options, entity) in 
                SystemAPI.Query<RefRW<AISubordinate>, DynamicBuffer<AIDirective>, DynamicBuffer<UtilityOption>>()
                    .WithEntityAccess())
            {
                var directivesBuffer = directives;
                var optionsBuffer = options;

                // Get active directive
                if (GOAPHelpers.TryGetActiveDirective(directivesBuffer, currentTick, out var activeDirective))
                {
                    subordinate.ValueRW.HasStandingOrders = true;
                    subordinate.ValueRW.LastOrderReceivedTick = activeDirective.IssuedTick;

                    // Boost utility score for directive-matching options
                    float effectivePriority = GOAPHelpers.GetEffectiveDirectivePriority(
                        activeDirective.Priority,
                        subordinate.ValueRO.Compliance);

                    for (int i = 0; i < optionsBuffer.Length; i++)
                    {
                        var option = optionsBuffer[i];
                        if (option.OptionId.Equals(activeDirective.DirectiveType))
                        {
                            option.DirectiveScore = effectivePriority;
                            optionsBuffer[i] = option;
                        }
                    }
                }
                else
                {
                    subordinate.ValueRW.HasStandingOrders = false;
                }

                // Clean up expired/completed directives
                for (int i = directivesBuffer.Length - 1; i >= 0; i--)
                {
                    var directive = directivesBuffer[i];
                    if (directive.IsCompleted || directive.IsCancelled)
                    {
                        directivesBuffer.RemoveAt(i);
                        continue;
                    }
                    if (directive.ExpiryTick > 0 && currentTick > directive.ExpiryTick)
                    {
                        directivesBuffer.RemoveAt(i);
                    }
                }
            }
        }
    }
}

