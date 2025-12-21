using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Telemetry;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

using SpatialSystemGroup = PureDOTS.Systems.SpatialSystemGroup;

namespace Space4X.Registry
{
    /// <summary>
    /// Updates per-entity morale based on modifiers and drift toward baseline.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(Space4XAffiliationComplianceSystem))]
    public partial struct Space4XMoraleSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<MoraleState>();
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

            var currentTick = timeState.Tick;
            var deltaTime = timeState.FixedDeltaTime;

            // Update morale for entities with modifiers
            foreach (var (moraleRef, modifiers, entity) in SystemAPI.Query<RefRW<MoraleState>, DynamicBuffer<MoraleModifier>>().WithEntityAccess())
            {
                var modifiersBuffer = modifiers;
                UpdateMoraleWithModifiers(ref moraleRef.ValueRW, ref modifiersBuffer, currentTick, deltaTime);
            }

            // Update morale for entities without modifiers (just drift)
            foreach (var (moraleRef, entity) in SystemAPI.Query<RefRW<MoraleState>>().WithNone<MoraleModifier>().WithEntityAccess())
            {
                UpdateMoraleDriftOnly(ref moraleRef.ValueRW, currentTick, deltaTime);
            }
        }

        [BurstCompile]
        private static void UpdateMoraleWithModifiers(ref MoraleState morale, ref DynamicBuffer<MoraleModifier> modifiers, uint currentTick, float deltaTime)
        {
            float current = (float)morale.Current;
            float baseline = (float)morale.Baseline;
            float driftRate = (float)morale.DriftRate;

            // Sum active modifiers
            float totalModifier = 0f;
            for (int i = modifiers.Length - 1; i >= 0; i--)
            {
                var modifier = modifiers[i];

                // Check if modifier has expired
                if (modifier.RemainingTicks > 0)
                {
                    modifier.RemainingTicks--;
                    if (modifier.RemainingTicks == 0)
                    {
                        modifiers.RemoveAt(i);
                        continue;
                    }
                    modifiers[i] = modifier;
                }

                totalModifier += (float)modifier.Strength;
            }

            // Apply drift toward baseline
            float driftAmount = driftRate * deltaTime;
            if (current < baseline)
            {
                current = math.min(current + driftAmount, baseline);
            }
            else if (current > baseline)
            {
                current = math.max(current - driftAmount, baseline);
            }

            // Apply modifiers (they shift the effective value, not the baseline)
            float effectiveMorale = current + totalModifier;

            // Clamp to valid range
            morale.Current = (half)math.clamp(effectiveMorale, -1f, 1f);
            morale.LastUpdateTick = currentTick;
        }

        [BurstCompile]
        private static void UpdateMoraleDriftOnly(ref MoraleState morale, uint currentTick, float deltaTime)
        {
            float current = (float)morale.Current;
            float baseline = (float)morale.Baseline;
            float driftRate = (float)morale.DriftRate;

            // Apply drift toward baseline
            float driftAmount = driftRate * deltaTime;
            if (current < baseline)
            {
                current = math.min(current + driftAmount, baseline);
            }
            else if (current > baseline)
            {
                current = math.max(current - driftAmount, baseline);
            }

            morale.Current = (half)math.clamp(current, -1f, 1f);
            morale.LastUpdateTick = currentTick;
        }
    }

    /// <summary>
    /// Aggregates morale from members to parent organizations.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(Space4XMoraleSystem))]
    public partial struct Space4XMoraleAggregationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<AggregateMoraleState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var currentTick = timeState.Tick;

            // Build lookup of entity morale values
            var moraleLookup = SystemAPI.GetComponentLookup<MoraleState>(true);
            moraleLookup.Update(ref state);

            // Aggregate morale for entities with AffiliationTag buffers
            foreach (var (aggregateRef, affiliations, entity) in SystemAPI.Query<RefRW<AggregateMoraleState>, DynamicBuffer<AffiliationTag>>().WithEntityAccess())
            {
                AggregateFromAffiliations(ref aggregateRef.ValueRW, in affiliations, ref moraleLookup, currentTick);
            }
        }

        [BurstCompile]
        private static void AggregateFromAffiliations(
            ref AggregateMoraleState aggregate,
            in DynamicBuffer<AffiliationTag> affiliations,
            ref ComponentLookup<MoraleState> moraleLookup,
            uint currentTick)
        {
            float sum = 0f;
            float sumSquared = 0f;
            float min = 1f;
            float max = -1f;
            int count = 0;

            for (int i = 0; i < affiliations.Length; i++)
            {
                var affiliation = affiliations[i];
                if (affiliation.Target == Entity.Null || !moraleLookup.HasComponent(affiliation.Target))
                {
                    continue;
                }

                var memberMorale = moraleLookup[affiliation.Target];
                float morale = (float)memberMorale.Current;
                float loyalty = (float)affiliation.Loyalty;

                // Weight by loyalty
                float weightedMorale = morale * loyalty;
                sum += weightedMorale;
                sumSquared += weightedMorale * weightedMorale;
                min = math.min(min, morale);
                max = math.max(max, morale);
                count++;
            }

            if (count > 0)
            {
                float mean = sum / count;
                float meanSquared = sumSquared / count;
                float variance = meanSquared - (mean * mean);

                aggregate.MeanMorale = (half)mean;
                aggregate.Variance = (half)math.max(0f, variance);
                aggregate.MinMorale = (half)min;
                aggregate.MaxMorale = (half)max;
                aggregate.MemberCount = count;
            }
            else
            {
                aggregate.MeanMorale = (half)0f;
                aggregate.Variance = (half)0f;
                aggregate.MinMorale = (half)0f;
                aggregate.MaxMorale = (half)0f;
                aggregate.MemberCount = 0;
            }

            aggregate.LastUpdateTick = currentTick;
        }
    }

    /// <summary>
    /// Emits morale telemetry metrics.
    /// </summary>
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(Space4XMoraleAggregationSystem))]
    public partial struct Space4XMoraleTelemetrySystem : ISystem
    {
        private EntityQuery _moraleQuery;
        private EntityQuery _aggregateQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TelemetryStream>();

            _moraleQuery = SystemAPI.QueryBuilder()
                .WithAll<MoraleState>()
                .Build();

            _aggregateQuery = SystemAPI.QueryBuilder()
                .WithAll<AggregateMoraleState>()
                .Build();
        }

        public void OnUpdate(ref SystemState state)
        {
            var telemetryEntity = SystemAPI.GetSingletonEntity<TelemetryStream>();
            var buffer = state.EntityManager.GetBuffer<TelemetryMetric>(telemetryEntity);

            // Individual morale stats
            int totalEntities = _moraleQuery.CalculateEntityCount();
            float sumMorale = 0f;
            int lowMoraleCount = 0;
            int criticalCount = 0;
            int inspiredCount = 0;

            foreach (var morale in SystemAPI.Query<RefRO<MoraleState>>())
            {
                float current = (float)morale.ValueRO.Current;
                sumMorale += current;

                if (current <= MoraleThresholds.CriticalLow)
                {
                    criticalCount++;
                }
                else if (current <= MoraleThresholds.Low)
                {
                    lowMoraleCount++;
                }
                else if (current >= MoraleThresholds.Inspired)
                {
                    inspiredCount++;
                }
            }

            float avgMorale = totalEntities > 0 ? sumMorale / totalEntities : 0f;

            buffer.AddMetric("space4x.morale.entities", totalEntities);
            buffer.AddMetric("space4x.morale.average", avgMorale, TelemetryMetricUnit.Ratio);
            buffer.AddMetric("loop.combat.morale.avg", avgMorale, TelemetryMetricUnit.Ratio);
            buffer.AddMetric("space4x.morale.low", lowMoraleCount);
            buffer.AddMetric("space4x.morale.critical", criticalCount);
            buffer.AddMetric("space4x.morale.inspired", inspiredCount);

            // Aggregate stats
            int aggregateCount = _aggregateQuery.CalculateEntityCount();
            int highVarianceCount = 0;

            foreach (var aggregate in SystemAPI.Query<RefRO<AggregateMoraleState>>())
            {
                if ((float)aggregate.ValueRO.Variance >= MoraleThresholds.DangerousVariance)
                {
                    highVarianceCount++;
                }
            }

            buffer.AddMetric("space4x.morale.aggregates", aggregateCount);
            buffer.AddMetric("space4x.morale.highVariance", highVarianceCount);
        }
    }
}
