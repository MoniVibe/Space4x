using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using SpatialSystemGroup = PureDOTS.Systems.SpatialSystemGroup;

namespace Space4X.Registry
{
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateBefore(typeof(Space4XTargetPrioritySystem))]
    public partial struct Space4XIntelDecaySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<IntelTargetFact>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var tuning = Space4XIntelTuning.Default;
            if (SystemAPI.TryGetSingleton(out Space4XIntelTuning tuningSingleton))
            {
                tuning = tuningSingleton;
            }

            var decayPerTick = math.max(0f, tuning.DecayPerTick);
            foreach (var intelBufferRO in SystemAPI.Query<DynamicBuffer<IntelTargetFact>>())
            {
                var intelBuffer = intelBufferRO;
                for (int i = intelBuffer.Length - 1; i >= 0; i--)
                {
                    var fact = intelBuffer[i];
                    if (fact.Target == Entity.Null)
                    {
                        intelBuffer.RemoveAt(i);
                        continue;
                    }

                    if (fact.ExpireTick != 0u && timeState.Tick >= fact.ExpireTick)
                    {
                        intelBuffer.RemoveAt(i);
                        continue;
                    }

                    if (decayPerTick <= 0f || fact.LastSeenTick == 0u || timeState.Tick <= fact.LastSeenTick)
                    {
                        continue;
                    }

                    var tickDelta = timeState.Tick - fact.LastSeenTick;
                    var confidence = math.max(0f, (float)fact.Confidence - decayPerTick * tickDelta);
                    if (confidence < tuning.MinConfidence)
                    {
                        intelBuffer.RemoveAt(i);
                        continue;
                    }

                    fact.Confidence = (half)confidence;
                    fact.LastSeenTick = timeState.Tick;
                    intelBuffer[i] = fact;
                }
            }
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(Space4XTargetPrioritySystem))]
    [UpdateBefore(typeof(Space4XHostileSpeciesTargetingSystem))]
    public partial struct Space4XIntelTargetingSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<TargetPriority>();
            state.RequireForUpdate<IntelTargetFact>();
            state.RequireForUpdate<TargetCandidate>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var tuning = Space4XIntelTuning.Default;
            if (SystemAPI.TryGetSingleton(out Space4XIntelTuning tuningSingleton))
            {
                tuning = tuningSingleton;
            }

            const float scoreEpsilon = 0.0001f;

            foreach (var (intelFacts, candidates, priority) in
                SystemAPI.Query<DynamicBuffer<IntelTargetFact>, DynamicBuffer<TargetCandidate>, RefRW<TargetPriority>>())
            {
                if (intelFacts.Length == 0 || candidates.Length == 0)
                {
                    continue;
                }
                var candidatesRW = candidates;

                float bestScore = float.MinValue;
                Entity bestTarget = Entity.Null;

                for (int i = 0; i < candidatesRW.Length; i++)
                {
                    var candidate = candidatesRW[i];
                    var bonus = 0f;

                    for (int j = 0; j < intelFacts.Length; j++)
                    {
                        var fact = intelFacts[j];
                        if (fact.Target != candidate.Entity)
                        {
                            continue;
                        }

                        var confidence = math.saturate((float)fact.Confidence);
                        if (confidence <= 0f)
                        {
                            continue;
                        }

                        var weight = math.saturate((float)fact.Weight);
                        var typeBonus = ResolveIntelBonus(fact.Type, tuning);
                        bonus += (tuning.BaseWeight + typeBonus) * confidence * weight;
                    }

                    if (bonus > 0f)
                    {
                        bonus = math.min(bonus, tuning.MaxBonus);
                        candidate.Score += bonus;
                        candidatesRW[i] = candidate;
                    }

                    if (candidate.Score > bestScore ||
                        (math.abs(candidate.Score - bestScore) <= scoreEpsilon && IsPreferredTarget(candidate.Entity, bestTarget)))
                    {
                        bestScore = candidate.Score;
                        bestTarget = candidate.Entity;
                    }
                }

                if (bestTarget != Entity.Null)
                {
                    if (bestTarget != priority.ValueRO.CurrentTarget)
                    {
                        priority.ValueRW.EngagementDuration = 0f;
                    }

                    priority.ValueRW.CurrentTarget = bestTarget;
                    priority.ValueRW.CurrentScore = bestScore;
                }
            }
        }

        private static float ResolveIntelBonus(IntelFactType type, in Space4XIntelTuning tuning)
        {
            switch (type)
            {
                case IntelFactType.CommandNode:
                    return tuning.CommandNodeBonus;
                case IntelFactType.PriorityTarget:
                    return tuning.PriorityTargetBonus;
                case IntelFactType.Threat:
                    return tuning.ThreatBonus;
                case IntelFactType.HighValue:
                    return tuning.HighValueBonus;
                case IntelFactType.Objective:
                    return tuning.ObjectiveBonus;
                default:
                    return 0f;
            }
        }

        private static bool IsPreferredTarget(Entity candidate, Entity current)
        {
            if (current == Entity.Null)
            {
                return true;
            }

            if (candidate.Index != current.Index)
            {
                return candidate.Index < current.Index;
            }

            return candidate.Version < current.Version;
        }
    }
}
