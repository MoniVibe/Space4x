using PureDOTS.Runtime.Patterns;
using PureDOTS.Runtime.Groups;
using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Patterns
{
    /// <summary>
    /// Evaluates pattern definitions against groups and writes generic modifiers + tags.
    /// Game-agnostic: relies only on GroupAggregate/GroupConfig data.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PureDOTS.Systems.Groups.GroupAggregationSystem))]
    public partial struct PatternSystem : ISystem
    {
        private static readonly PatternDefinition[] s_GroupPatterns = new[]
        {
            new PatternDefinition
            {
                Id = PatternId.HardworkingVillage,
                Scope = PatternScope.Group,
                MinCohesion = 0.75f,
                MinMorale = 450f,
                MinMembers = 3,
                WorkRateMultiplier = 1.15f,
                WanderRateMultiplier = 0.90f,
                DisbandRiskDelta = 0f
            },
            new PatternDefinition
            {
                Id = PatternId.ChaoticBand,
                Scope = PatternScope.Group,
                MaxCohesion = 0.35f,
                MinMembers = 3,
                WorkRateMultiplier = 0.80f,
                WanderRateMultiplier = 1.25f,
                DisbandRiskDelta = 0.0005f
            },
            new PatternDefinition
            {
                Id = PatternId.OverstressedGroup,
                Scope = PatternScope.Group,
                MaxCohesion = 0.5f,
                MaxMorale = 350f,
                MinMembers = 2,
                WorkRateMultiplier = 0.70f,
                WanderRateMultiplier = 1.10f,
                DisbandRiskDelta = 0.0015f
            }
        };

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GroupAggregate>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (timeState.IsPaused || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Ensure required components/buffers exist.
            foreach (var (aggregate, entity) in SystemAPI.Query<RefRO<GroupAggregate>>().WithEntityAccess())
            {
                if (!state.EntityManager.HasComponent<GroupPatternModifiers>(entity))
                {
                    ecb.AddComponent(entity, GroupPatternModifiers.Identity);
                }

                if (!state.EntityManager.HasBuffer<ActivePatternTag>(entity))
                {
                    ecb.AddBuffer<ActivePatternTag>(entity);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            // Evaluate patterns and apply modifiers.
            foreach (var (aggregate, config, modifiers, tags, entity) in SystemAPI
                         .Query<RefRO<GroupAggregate>, RefRO<GroupConfig>, RefRW<GroupPatternModifiers>, DynamicBuffer<ActivePatternTag>>()
                         .WithEntityAccess())
            {
                var result = EvaluatePatterns(in aggregate.ValueRO, in config.ValueRO);
                modifiers.ValueRW = result.Modifiers;

                tags.Clear();
                for (int i = 0; i < result.ActivePatternIds.Length; i++)
                {
                    tags.Add(new ActivePatternTag { Id = result.ActivePatternIds[i] });
                }
                result.ActivePatternIds.Dispose();
            }
        }

        private static PatternEvaluationResult EvaluatePatterns(in GroupAggregate aggregate, in GroupConfig config)
        {
            var modifiers = GroupPatternModifiers.Identity;
            var activeTags = new NativeList<PatternId>(Allocator.Temp);

            for (int i = 0; i < s_GroupPatterns.Length; i++)
            {
                ref readonly var pattern = ref s_GroupPatterns[i];
                if (pattern.Scope != PatternScope.Group)
                {
                    continue;
                }

                if (!PassesGroupConditions(in pattern, in aggregate, in config))
                {
                    continue;
                }

                // Accumulate modifiers multiplicatively for rates and additively for risks.
                var work = pattern.WorkRateMultiplier > 0f ? pattern.WorkRateMultiplier : 1f;
                var wander = pattern.WanderRateMultiplier > 0f ? pattern.WanderRateMultiplier : 1f;
                modifiers.WorkRateMultiplier = math.max(0.1f, modifiers.WorkRateMultiplier * work);
                modifiers.WanderRateMultiplier = math.max(0f, modifiers.WanderRateMultiplier * wander);
                modifiers.DisbandRiskPerTick = math.max(0f, modifiers.DisbandRiskPerTick + math.max(0f, pattern.DisbandRiskDelta));

                activeTags.Add(pattern.Id);
            }

            return new PatternEvaluationResult
            {
                Modifiers = modifiers,
                ActivePatternIds = activeTags
            };
        }

        private static bool PassesGroupConditions(in PatternDefinition pattern, in GroupAggregate aggregate, in GroupConfig config)
        {
            if (pattern.MinMembers > 0 && aggregate.MemberCount < pattern.MinMembers)
            {
                return false;
            }

            if (pattern.MinCohesion > 0f && aggregate.Cohesion < pattern.MinCohesion)
            {
                return false;
            }

            if (pattern.MaxCohesion > 0f && aggregate.Cohesion > pattern.MaxCohesion)
            {
                return false;
            }

            if (pattern.MinMorale > 0f && (aggregate.AverageMorale <= 0f || aggregate.AverageMorale < pattern.MinMorale))
            {
                return false;
            }

            if (pattern.MaxMorale > 0f)
            {
                if (aggregate.AverageMorale <= 0f || aggregate.AverageMorale > pattern.MaxMorale)
                {
                    return false;
                }
            }

            // Example type hints: ChaoticBand should favor military/social bands; HardworkingVillage leans toward social/guild.
            if (pattern.Id == PatternId.HardworkingVillage)
            {
                if (!(config.Type == GroupType.Social || config.Type == GroupType.Guild || config.Type == GroupType.Generic))
                {
                    return false;
                }
            }

            if (pattern.Id == PatternId.ChaoticBand)
            {
                if (!(config.Type == GroupType.Military || config.Type == GroupType.Generic))
                {
                    return false;
                }
            }

            return true;
        }

        private struct PatternEvaluationResult
        {
            public GroupPatternModifiers Modifiers;
            public NativeList<PatternId> ActivePatternIds;
        }
    }
}
