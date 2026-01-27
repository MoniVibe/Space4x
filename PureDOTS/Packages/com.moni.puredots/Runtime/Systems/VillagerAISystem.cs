using PureDOTS.Config;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Villager;
using PureDOTS.Runtime.Villagers;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Minimal behaviour layer that evaluates high level villager goals based on needs and job data.
    /// For villagers with AI pipeline components (AIUtilityState, VillagerAIUtilityBinding),
    /// goals are set by VillagerAIPipelineBridgeSystem. This system provides fallback logic
    /// for villagers without AI pipeline components or when the pipeline hasn't made a decision.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(VillagerSystemGroup))]
    [UpdateBefore(typeof(VillagerTargetingSystem))]
    [UpdateAfter(typeof(VillagerAIPipelineBridgeSystem))]
    public partial struct VillagerAISystem : ISystem
    {
        private EntityQuery _villagerQuery;
        private ComponentLookup<VillagerBehavior> _behaviorLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<VillagerAIPipelineBridgeState> _bridgeStateLookup;
        private BufferLookup<VillagerCommand> _commandLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _villagerQuery = SystemAPI.QueryBuilder()
                .WithAll<VillagerAIState, VillagerNeeds, VillagerJob, VillagerJobTicket, VillagerFlags, LocalTransform, VillagerArchetypeResolved>()
                .WithNone<PlaybackGuardTag>()
                .Build();

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            // Don't require the query - it's optional (system will just do nothing if no villagers exist)

            _behaviorLookup = state.GetComponentLookup<VillagerBehavior>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _bridgeStateLookup = state.GetComponentLookup<VillagerAIPipelineBridgeState>(true);
            _commandLookup = state.GetBufferLookup<VillagerCommand>(true);
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

            // Get villager behavior config and archetypes (if any)
            var config = SystemAPI.HasSingleton<VillagerBehaviorConfig>()
                ? SystemAPI.GetSingleton<VillagerBehaviorConfig>()
                : VillagerBehaviorConfig.CreateDefaults();

            // Early exit if no villagers exist
            if (_villagerQuery.IsEmpty)
            {
                return;
            }

            _behaviorLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _bridgeStateLookup.Update(ref state);
            _commandLookup.Update(ref state);

            VillagerArchetypeDefaults.CreateFallback(out var fallbackArchetype);

            var job = new EvaluateVillagerAIJob
            {
                DeltaTime = timeState.FixedDeltaTime,
                CurrentTick = timeState.Tick,
                BehaviorConfig = config,
                BehaviorLookup = _behaviorLookup,
                TransformLookup = _transformLookup,
                BridgeStateLookup = _bridgeStateLookup,
                CommandLookup = _commandLookup,
                FallbackArchetype = fallbackArchetype
            };

            state.Dependency = job.ScheduleParallel(_villagerQuery, state.Dependency);
        }

        [BurstCompile]
        public partial struct EvaluateVillagerAIJob : IJobEntity
        {
            public float DeltaTime;
            public uint CurrentTick;
            public VillagerBehaviorConfig BehaviorConfig;
            [ReadOnly] public ComponentLookup<VillagerBehavior> BehaviorLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
            [ReadOnly] public ComponentLookup<VillagerAIPipelineBridgeState> BridgeStateLookup;
            [ReadOnly] public BufferLookup<VillagerCommand> CommandLookup;
            public VillagerArchetypeData FallbackArchetype;

            public void Execute(Entity entity, ref VillagerAIState aiState, ref VillagerNeeds needs, in VillagerJob job, in VillagerJobTicket ticket, in VillagerFlags flags, in LocalTransform transform, in VillagerArchetypeResolved resolved)
            {
                // Skip dead villagers
                if (flags.IsDead)
                {
                    return;
                }
                
                aiState.StateTimer += DeltaTime;

                if (BridgeStateLookup.HasComponent(entity))
                {
                    var bridgeState = BridgeStateLookup[entity];
                    if (bridgeState.IsAIPipelineActive != 0 || bridgeState.LastBridgedTick == CurrentTick)
                    {
                        return;
                    }
                }

                if (CommandLookup.HasBuffer(entity) && CommandLookup[entity].Length > 0)
                {
                    return;
                }

                if (ticket.ResourceEntity != Entity.Null || ticket.Phase != (byte)VillagerJob.JobPhase.Idle)
                {
                    return;
                }

                var behavior = BehaviorLookup.HasComponent(entity)
                    ? BehaviorLookup[entity]
                    : default;

                var archetype = resolved.Data.ArchetypeName.Length > 0
                    ? resolved.Data
                    : FallbackArchetype;

                // Fallback need-based goal selection for villagers without AI pipeline
                // or when the pipeline hasn't made a decision this tick.
                var desiredGoal = VillagerAIState.Goal.None;

                // Simple need-based goal selection (will be replaced by AI pipeline bridge)
                if (needs.HungerFloat < BehaviorConfig.HungerThreshold)
                {
                    desiredGoal = VillagerAIState.Goal.SurviveHunger;
                }
                else if (needs.EnergyFloat < BehaviorConfig.RestEnergyThreshold)
                {
                    desiredGoal = VillagerAIState.Goal.Rest;
                }
                else if (HasActiveJob(job, ticket))
                {
                    // Prefer work if there's an active job and basic needs are met
                    desiredGoal = VillagerAIState.Goal.Work;
                }

                if (needs.Health < BehaviorConfig.FleeHealthThreshold)
                {
                    desiredGoal = VillagerAIState.Goal.Flee;
                }

                if (desiredGoal != aiState.CurrentGoal)
                {
                    aiState.CurrentGoal = desiredGoal;
                    aiState.CurrentState = GoalToState(desiredGoal);
                    aiState.StateTimer = 0f;
                    aiState.StateStartTick = CurrentTick;
                }

                // Clamp states when conditions change.
                switch (aiState.CurrentState)
                {
                    case VillagerAIState.State.Working:
                        if (ticket.ResourceEntity == Entity.Null)
                        {
                            SetIdle(ref aiState);
                        }
                        break;
                    case VillagerAIState.State.Eating:
                        if (needs.HungerFloat < BehaviorConfig.HungerThreshold * BehaviorConfig.EatingHungerThresholdMultiplier ||
                            aiState.StateTimer >= BehaviorConfig.EatingDuration)
                        {
                            SetIdle(ref aiState);
                        }
                        break;
                    case VillagerAIState.State.Sleeping:
                        if (needs.EnergyFloat > BehaviorConfig.RestEnergyThreshold)
                        {
                            SetIdle(ref aiState);
                        }
                        break;
                    case VillagerAIState.State.Fleeing:
                        if (aiState.StateTimer >= BehaviorConfig.FleeDuration)
                        {
                            SetIdle(ref aiState);
                        }
                        break;
                }

                if (aiState.CurrentState != VillagerAIState.State.Working)
                {
                    aiState.TargetEntity = Entity.Null;
                }
                else if (ticket.ResourceEntity != Entity.Null)
                {
                    aiState.TargetEntity = ticket.ResourceEntity;
                }
            }

            private void HandleStateTransitions(ref VillagerAIState aiState, in VillagerNeeds needs, in VillagerJob job, in VillagerJobTicket ticket)
            {
                // Handle state transitions even when AI pipeline is controlling
                switch (aiState.CurrentState)
                {
                    case VillagerAIState.State.Working:
                        if (ticket.ResourceEntity == Entity.Null)
                        {
                            SetIdle(ref aiState);
                        }
                        break;
                    case VillagerAIState.State.Eating:
                        if (needs.HungerFloat < BehaviorConfig.HungerThreshold * BehaviorConfig.EatingHungerThresholdMultiplier ||
                            aiState.StateTimer >= BehaviorConfig.EatingDuration)
                        {
                            SetIdle(ref aiState);
                        }
                        break;
                    case VillagerAIState.State.Sleeping:
                        if (needs.EnergyFloat > BehaviorConfig.RestEnergyThreshold)
                        {
                            SetIdle(ref aiState);
                        }
                        break;
                    case VillagerAIState.State.Fleeing:
                        if (aiState.StateTimer >= BehaviorConfig.FleeDuration)
                        {
                            SetIdle(ref aiState);
                        }
                        break;
                }

                // Update target for working state
                if (aiState.CurrentState == VillagerAIState.State.Working && ticket.ResourceEntity != Entity.Null)
                {
                    aiState.TargetEntity = ticket.ResourceEntity;
                }
            }

            private static bool HasActiveJob(in VillagerJob job, in VillagerJobTicket ticket)
            {
                return job.Type != VillagerJob.JobType.None
                       && ticket.ResourceEntity != Entity.Null
                       && (VillagerJob.JobPhase)ticket.Phase != VillagerJob.JobPhase.Idle;
            }

            private float EstimateJobDistance(float3 origin, Entity target)
            {
                if (target == Entity.Null)
                {
                    return 100f;
                }

                if (TransformLookup.HasComponent(target))
                {
                    var destination = TransformLookup[target].Position;
                    return math.distance(origin, destination);
                }

                return 100f;
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
                    _ => VillagerAIState.State.Idle
                };
            }

            private static void SetIdle(ref VillagerAIState aiState)
            {
                aiState.CurrentGoal = VillagerAIState.Goal.None;
                aiState.CurrentState = VillagerAIState.State.Idle;
                aiState.TargetEntity = Entity.Null;
                aiState.TargetPosition = float3.zero;
                aiState.StateTimer = 0f;
            }
        }

    }
}
