using Unity.Mathematics;
using Unity.Entities;
using Unity.Collections;
using Unity.Burst;

namespace PureDOTS.Runtime.AI.GOAP
{
    /// <summary>
    /// Static helpers for GOAP and Utility AI.
    /// </summary>
    [BurstCompile]
    public static class GOAPHelpers
    {
        /// <summary>
        /// Default utility configuration.
        /// </summary>
        public static UtilityConfig DefaultUtilityConfig => new UtilityConfig
        {
            NeedWeight = 0.4f,
            OpportunityWeight = 0.3f,
            DirectiveWeight = 0.2f,
            RandomnessWeight = 0.1f,
            EvaluationInterval = 60,
            MinScoreToAct = 10f
        };

        /// <summary>
        /// Finds the highest priority active goal.
        /// </summary>
        public static bool TryGetHighestPriorityGoal(in DynamicBuffer<AIGoal> goals, out AIGoal highestGoal)
        {
            highestGoal = default;
            float highestPriority = float.MinValue;
            bool found = false;

            for (int i = 0; i < goals.Length; i++)
            {
                var goal = goals[i];
                if (goal.Priority > highestPriority && goal.CurrentSatisfaction < goal.SatisfactionThreshold)
                {
                    highestPriority = goal.Priority;
                    highestGoal = goal;
                    found = true;
                }
            }

            return found;
        }

        /// <summary>
        /// Updates goal priorities based on insistence.
        /// </summary>
        public static void UpdateGoalInsistence(ref DynamicBuffer<AIGoal> goals, float deltaTime)
        {
            for (int i = 0; i < goals.Length; i++)
            {
                var goal = goals[i];
                if (goal.CurrentSatisfaction < goal.SatisfactionThreshold)
                {
                    goal.Priority = math.min(100f, goal.Priority + goal.Insistence * deltaTime);
                    goals[i] = goal;
                }
            }
        }

        /// <summary>
        /// Finds actions that satisfy a goal.
        /// </summary>
        public static int FindActionsForGoal(
            in DynamicBuffer<AIAction> actions,
            FixedString32Bytes goalId,
            ref NativeList<AIAction> matchingActions)
        {
            int count = 0;
            for (int i = 0; i < actions.Length; i++)
            {
                if (actions[i].GoalId.Equals(goalId))
                {
                    matchingActions.Add(actions[i]);
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Checks if action preconditions are met.
        /// </summary>
        public static bool CheckPreconditions(
            in AIAction action,
            in DynamicBuffer<WorldStateFact> worldState,
            Entity targetEntity)
        {
            // Check target requirement
            if (action.RequiresTarget && targetEntity == Entity.Null)
                return false;

            // Check required state
            if (action.RequiredState.Length > 0)
            {
                bool stateFound = false;
                for (int i = 0; i < worldState.Length; i++)
                {
                    if (worldState[i].FactKey.Equals(action.RequiredState) && worldState[i].FactValue > 0.5f)
                    {
                        stateFound = true;
                        break;
                    }
                }
                if (!stateFound) return false;
            }

            // Check required resource
            if (action.RequiresResource && action.RequiredResource.Length > 0)
            {
                bool resourceFound = false;
                for (int i = 0; i < worldState.Length; i++)
                {
                    if (worldState[i].FactKey.Equals(action.RequiredResource) && worldState[i].FactValue > 0)
                    {
                        resourceFound = true;
                        break;
                    }
                }
                if (!resourceFound) return false;
            }

            return true;
        }

        /// <summary>
        /// Calculates action utility considering cost and goal satisfaction.
        /// </summary>
        public static float CalculateActionUtility(in AIAction action, in AIGoal goal)
        {
            // Utility = (satisfaction gain * goal priority) / cost
            float satisfactionGain = action.GoalSatisfactionDelta;
            float priorityWeight = goal.Priority / 100f;
            float costFactor = math.max(0.1f, action.Cost);
            
            return (satisfactionGain * priorityWeight * action.Utility) / costFactor;
        }

        /// <summary>
        /// Calculates final utility score for an option.
        /// </summary>
        public static float CalculateFinalScore(in UtilityOption option, in UtilityConfig config, uint randomSeed)
        {
            float score = option.BaseScore;
            score += option.NeedScore * config.NeedWeight;
            score += option.OpportunityScore * config.OpportunityWeight;
            score += option.DirectiveScore * config.DirectiveWeight;
            
            // Add randomness
            float random = (DeterministicRandom(randomSeed) / (float)uint.MaxValue) * 2f - 1f; // -1 to 1
            score += random * config.RandomnessWeight * score;
            
            return math.max(0f, score);
        }

        /// <summary>
        /// Finds the best utility option.
        /// </summary>
        public static bool TryGetBestOption(in DynamicBuffer<UtilityOption> options, in UtilityConfig config, out UtilityOption best)
        {
            best = default;
            float bestScore = config.MinScoreToAct;
            bool found = false;

            for (int i = 0; i < options.Length; i++)
            {
                if (options[i].FinalScore > bestScore)
                {
                    bestScore = options[i].FinalScore;
                    best = options[i];
                    found = true;
                }
            }

            return found;
        }

        /// <summary>
        /// Gets active directive with highest priority.
        /// </summary>
        public static bool TryGetActiveDirective(in DynamicBuffer<AIDirective> directives, uint currentTick, out AIDirective active)
        {
            active = default;
            float highestPriority = float.MinValue;
            bool found = false;

            for (int i = 0; i < directives.Length; i++)
            {
                var directive = directives[i];
                
                // Skip completed, cancelled, or expired
                if (directive.IsCompleted || directive.IsCancelled)
                    continue;
                if (directive.ExpiryTick > 0 && currentTick > directive.ExpiryTick)
                    continue;
                
                if (directive.Priority > highestPriority)
                {
                    highestPriority = directive.Priority;
                    active = directive;
                    found = true;
                }
            }

            return found;
        }

        /// <summary>
        /// Calculates compliance-adjusted directive priority.
        /// </summary>
        public static float GetEffectiveDirectivePriority(float directivePriority, float compliance)
        {
            return directivePriority * compliance;
        }

        /// <summary>
        /// Updates world state fact.
        /// </summary>
        public static void SetWorldFact(ref DynamicBuffer<WorldStateFact> worldState, FixedString32Bytes key, float value, uint tick)
        {
            for (int i = 0; i < worldState.Length; i++)
            {
                if (worldState[i].FactKey.Equals(key))
                {
                    var fact = worldState[i];
                    fact.FactValue = value;
                    fact.LastUpdatedTick = tick;
                    worldState[i] = fact;
                    return;
                }
            }

            // Add new fact
            worldState.Add(new WorldStateFact
            {
                FactKey = key,
                FactValue = value,
                LastUpdatedTick = tick
            });
        }

        /// <summary>
        /// Gets world state fact value.
        /// </summary>
        public static float GetWorldFact(in DynamicBuffer<WorldStateFact> worldState, FixedString32Bytes key, float defaultValue = 0f)
        {
            for (int i = 0; i < worldState.Length; i++)
            {
                if (worldState[i].FactKey.Equals(key))
                {
                    return worldState[i].FactValue;
                }
            }
            return defaultValue;
        }

        /// <summary>
        /// Creates a simple plan from goal to actions.
        /// </summary>
        public static int CreateSimplePlan(
            in AIGoal goal,
            in DynamicBuffer<AIAction> availableActions,
            in DynamicBuffer<WorldStateFact> worldState,
            ref DynamicBuffer<PlannedAction> plan,
            Entity targetEntity)
        {
            plan.Clear();
            int addedActions = 0;

            // Find all actions for this goal
            for (int i = 0; i < availableActions.Length; i++)
            {
                var action = availableActions[i];
                if (!action.GoalId.Equals(goal.GoalId))
                    continue;

                // Check if preconditions are met
                if (!CheckPreconditions(action, worldState, targetEntity))
                    continue;

                // Add to plan
                plan.Add(new PlannedAction
                {
                    ActionId = action.ActionId,
                    TargetEntity = targetEntity,
                    ExpectedCost = action.Cost,
                    ExpectedUtility = CalculateActionUtility(action, goal),
                    SequenceIndex = (byte)addedActions,
                    IsCompleted = false
                });
                addedActions++;

                // Simple planner: just take best single action
                // TODO: Implement proper GOAP chain planning
                break;
            }

            return addedActions;
        }

        /// <summary>
        /// Simple deterministic random.
        /// </summary>
        private static uint DeterministicRandom(uint seed)
        {
            seed ^= seed << 13;
            seed ^= seed >> 17;
            seed ^= seed << 5;
            return seed;
        }

        /// <summary>
        /// Creates default AI planner.
        /// </summary>
        public static AIPlanner CreateDefaultPlanner()
        {
            return new AIPlanner
            {
                ReplanInterval = 300, // Every 5 seconds at 60 ticks/sec
                PlanConfidence = 1f,
                NeedsReplan = true
            };
        }

        /// <summary>
        /// Creates default AI state.
        /// </summary>
        public static AIState CreateDefaultState()
        {
            return new AIState
            {
                CurrentState = new FixedString32Bytes("idle"),
                IsInterruptible = true
            };
        }
    }
}

