using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Villagers;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Ensures that every aggregate entity carries the shared Villager* personality components
    /// so downstream systems can operate on aggregates and individuals uniformly.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct AggregatePersonalityBootstrapSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AggregateEntity>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var added = false;

            foreach (var (_, entity) in SystemAPI.Query<RefRO<AggregateEntity>>().WithNone<VillagerAlignment>().WithEntityAccess())
            {
                ecb.AddComponent(entity, new VillagerAlignment());
                added = true;
            }

            foreach (var (_, entity) in SystemAPI.Query<RefRO<AggregateEntity>>().WithNone<VillagerBehavior>().WithEntityAccess())
            {
                ecb.AddComponent(entity, new VillagerBehavior());
                added = true;
            }

            foreach (var (_, entity) in SystemAPI.Query<RefRO<AggregateEntity>>().WithNone<VillagerInitiativeState>().WithEntityAccess())
            {
                ecb.AddComponent(entity, new VillagerInitiativeState
                {
                    CurrentInitiative = 0.5f,
                    NextActionTick = 0,
                    PendingAction = default
                });
                added = true;
            }

            if (added)
            {
                ecb.Playback(state.EntityManager);
            }

            ecb.Dispose();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }

    /// <summary>
    /// Computes weighted alignment averages for aggregates from their member villagers.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(AggregateAggregationSystem))]
    public partial struct AggregateAlignmentComputationSystem : ISystem
    {
        private ComponentLookup<VillagerAlignment> _memberAlignmentLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _memberAlignmentLookup = state.GetComponentLookup<VillagerAlignment>(true);
            state.RequireForUpdate<AggregateEntity>();
            state.RequireForUpdate<AggregateMember>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _memberAlignmentLookup.Update(ref state);
            var hasTime = SystemAPI.TryGetSingleton(out TimeState timeState);
            var currentTick = hasTime ? timeState.Tick : 0u;

            foreach (var (alignmentRef, members, entity) in SystemAPI
                         .Query<RefRW<VillagerAlignment>, DynamicBuffer<AggregateMember>>()
                         .WithAll<AggregateEntity>()
                         .WithEntityAccess())
            {
                ref var alignment = ref alignmentRef.ValueRW;
                if (members.Length == 0)
                {
                    ResetAlignment(ref alignment, currentTick, hasTime);
                    continue;
                }

                float weightSum = 0f;
                float moralSum = 0f;
                float orderSum = 0f;
                float puritySum = 0f;
                float strengthSum = 0f;

                for (int i = 0; i < members.Length; i++)
                {
                    var member = members[i];
                    if (!_memberAlignmentLookup.HasComponent(member.Member))
                    {
                        continue;
                    }

                    var memberAlignment = _memberAlignmentLookup[member.Member];
                    var weight = math.max(0.01f, member.Weight <= 0f ? 1f : member.Weight);
                    weightSum += weight;
                    moralSum += memberAlignment.MoralAxis * weight;
                    orderSum += memberAlignment.OrderAxis * weight;
                    puritySum += memberAlignment.PurityAxis * weight;
                    strengthSum += memberAlignment.AlignmentStrength * weight;
                }

                if (weightSum <= math.FLT_MIN_NORMAL)
                {
                    ResetAlignment(ref alignment, currentTick, hasTime);
                    continue;
                }

                var newAlignment = alignment;
                newAlignment.MoralAxis = (sbyte)math.clamp(math.round(moralSum / weightSum), -100f, 100f);
                newAlignment.OrderAxis = (sbyte)math.clamp(math.round(orderSum / weightSum), -100f, 100f);
                newAlignment.PurityAxis = (sbyte)math.clamp(math.round(puritySum / weightSum), -100f, 100f);
                newAlignment.AlignmentStrength = math.saturate(strengthSum / weightSum);

                if (hasTime && HasAlignmentChanged(in alignment, in newAlignment))
                {
                    newAlignment.LastShiftTick = currentTick;
                }

                alignment = newAlignment;
            }
        }

        private static void ResetAlignment(ref VillagerAlignment alignment, uint currentTick, bool hasTime)
        {
            if (alignment.MoralAxis == 0 && alignment.OrderAxis == 0 && alignment.PurityAxis == 0 && alignment.AlignmentStrength == 0f)
            {
                return;
            }

            alignment.MoralAxis = 0;
            alignment.OrderAxis = 0;
            alignment.PurityAxis = 0;
            alignment.AlignmentStrength = 0f;
            if (hasTime)
            {
                alignment.LastShiftTick = currentTick;
            }
        }

        private static bool HasAlignmentChanged(in VillagerAlignment previous, in VillagerAlignment updated)
        {
            return previous.MoralAxis != updated.MoralAxis ||
                   previous.OrderAxis != updated.OrderAxis ||
                   previous.PurityAxis != updated.PurityAxis ||
                   math.abs(previous.AlignmentStrength - updated.AlignmentStrength) > 0.0001f;
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }

    /// <summary>
    /// Produces aggregate VillagerBehavior values by averaging member behavior traits.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(AggregateAlignmentComputationSystem))]
    public partial struct AggregateBehaviorComputationSystem : ISystem
    {
        private ComponentLookup<VillagerBehavior> _memberBehaviorLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _memberBehaviorLookup = state.GetComponentLookup<VillagerBehavior>(true);
            state.RequireForUpdate<AggregateEntity>();
            state.RequireForUpdate<AggregateMember>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _memberBehaviorLookup.Update(ref state);

            foreach (var (behaviorRef, members) in SystemAPI
                         .Query<RefRW<VillagerBehavior>, DynamicBuffer<AggregateMember>>()
                         .WithAll<AggregateEntity>())
            {
                ref var behavior = ref behaviorRef.ValueRW;
                if (members.Length == 0)
                {
                    behavior = default;
                    continue;
                }

                float weightSum = 0f;
                float vengefulSum = 0f;
                float boldSum = 0f;
                float initiativeModifierSum = 0f;
                float grudgeSum = 0f;
                uint lastMajorTick = 0;

                for (int i = 0; i < members.Length; i++)
                {
                    var member = members[i];
                    if (!_memberBehaviorLookup.HasComponent(member.Member))
                    {
                        continue;
                    }

                    var memberBehavior = _memberBehaviorLookup[member.Member];
                    var weight = math.max(0.01f, member.Weight <= 0f ? 1f : member.Weight);
                    weightSum += weight;
                    vengefulSum += memberBehavior.VengefulScore * weight;
                    boldSum += memberBehavior.BoldScore * weight;
                    initiativeModifierSum += memberBehavior.InitiativeModifier * weight;
                    grudgeSum += memberBehavior.ActiveGrudgeCount * weight;
                    lastMajorTick = math.max(lastMajorTick, memberBehavior.LastMajorActionTick);
                }

                if (weightSum <= math.FLT_MIN_NORMAL)
                {
                    behavior = default;
                    continue;
                }

                behavior.VengefulScore = (sbyte)math.clamp(math.round(vengefulSum / weightSum), -100f, 100f);
                behavior.BoldScore = (sbyte)math.clamp(math.round(boldSum / weightSum), -100f, 100f);
                behavior.InitiativeModifier = initiativeModifierSum / weightSum;
                behavior.ActiveGrudgeCount = (byte)math.clamp(math.round(grudgeSum / weightSum), 0f, 255f);
                behavior.LastMajorActionTick = lastMajorTick;
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }

    /// <summary>
    /// Computes aggregate initiative cadence from averaged behavior/alignments and aggregate morale/cohesion.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(AggregateBehaviorComputationSystem))]
    public partial struct AggregateInitiativeComputationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AggregateEntity>();
            state.RequireForUpdate<VillagerInitiativeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var hasTime = SystemAPI.TryGetSingleton(out TimeState timeState);
            var tick = hasTime ? timeState.Tick : 0u;

            foreach (var (initiativeRef, behavior, alignment, aggregate) in SystemAPI
                         .Query<RefRW<VillagerInitiativeState>, RefRO<VillagerBehavior>, RefRO<VillagerAlignment>, RefRO<AggregateEntity>>())
            {
                var baseInitiative = ComputeBaseInitiative(in aggregate.ValueRO);
                var boldModifier = behavior.ValueRO.BoldScore * 0.0015f;
                var grudgeBoost = behavior.ValueRO.ActiveGrudgeCount * 0.02f;
                var alignmentModifier = ComputeAlignmentModifier(in alignment.ValueRO);

                var computed = math.clamp((baseInitiative + boldModifier + grudgeBoost) * alignmentModifier, 0f, 1f);

                var initiative = initiativeRef.ValueRO;
                initiative.CurrentInitiative = computed;

                if (hasTime)
                {
                    var needsReschedule = tick >= initiative.NextActionTick || initiative.NextActionTick == 0;
                    if (needsReschedule)
                    {
                        initiative.NextActionTick = tick + ComputeAggregateInterval(computed, aggregate.ValueRO.MemberCount);
                    }
                }

                initiativeRef.ValueRW = initiative;
            }
        }

        private static float ComputeBaseInitiative(in AggregateEntity aggregate)
        {
            var moraleFactor = math.saturate(aggregate.Morale);
            var cohesionFactor = math.saturate(aggregate.Cohesion);
            var stressPenalty = math.saturate(aggregate.Stress);
            var memberFactor = math.saturate(aggregate.MemberCount / 100f);

            var value = 0.4f
                        + moraleFactor * 0.2f
                        + cohesionFactor * 0.2f
                        + memberFactor * 0.1f
                        - stressPenalty * 0.15f;
            return math.clamp(value, 0.1f, 0.95f);
        }

        private static float ComputeAlignmentModifier(in VillagerAlignment alignment)
        {
            var modifier = 1f;
            if (alignment.IsLawful)
            {
                modifier -= 0.05f;
            }
            else if (alignment.IsChaotic)
            {
                modifier += 0.05f;
            }

            if (alignment.IsPure)
            {
                modifier += 0.02f;
            }
            else if (alignment.IsCorrupt)
            {
                modifier -= 0.02f;
            }

            return math.clamp(modifier, 0.8f, 1.2f);
        }

        private static uint ComputeAggregateInterval(float initiative, int memberCount)
        {
            var memberScale = math.clamp(math.sqrt(math.max(1, memberCount)), 1f, 12f);
            var baseDays = math.lerp(30f, 3f, initiative);
            baseDays = math.max(1f, baseDays / memberScale);
            return (uint)math.max(1f, baseDays * 86400f);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
