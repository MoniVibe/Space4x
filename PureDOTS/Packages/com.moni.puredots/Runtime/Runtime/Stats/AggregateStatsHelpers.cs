using Unity.Mathematics;
using Unity.Entities;
using Unity.Collections;
using Unity.Burst;

namespace PureDOTS.Runtime.Stats
{
    /// <summary>
    /// Static helpers for aggregate statistics calculations.
    /// </summary>
    [BurstCompile]
    public static class AggregateStatsHelpers
    {
        /// <summary>
        /// Default aggregate configuration.
        /// </summary>
        public static AggregateConfig DefaultConfig => new AggregateConfig
        {
            HealthyThreshold = 0.6f,
            CriticalThreshold = 0.2f,
            BottleneckThreshold = 0.3f,
            WeightByImportance = 0,
            IncludeInactive = 0
        };

        /// <summary>
        /// Calculates simple mean of values.
        /// </summary>
        public static float CalculateMean(NativeArray<float> values)
        {
            if (values.Length == 0) return 0;
            
            float sum = 0;
            for (int i = 0; i < values.Length; i++)
            {
                sum += values[i];
            }
            return sum / values.Length;
        }

        /// <summary>
        /// Calculates weighted mean of values.
        /// </summary>
        public static float CalculateWeightedMean(NativeArray<StatContribution> contributions)
        {
            if (contributions.Length == 0) return 0;
            
            float weightedSum = 0;
            float totalWeight = 0;
            
            for (int i = 0; i < contributions.Length; i++)
            {
                weightedSum += contributions[i].Value * contributions[i].Weight;
                totalWeight += contributions[i].Weight;
            }
            
            return totalWeight > 0 ? weightedSum / totalWeight : 0;
        }

        /// <summary>
        /// Calculates variance of values around mean.
        /// </summary>
        public static float CalculateVariance(NativeArray<float> values, float mean)
        {
            if (values.Length <= 1) return 0;
            
            float sumSquaredDiff = 0;
            for (int i = 0; i < values.Length; i++)
            {
                float diff = values[i] - mean;
                sumSquaredDiff += diff * diff;
            }
            
            return sumSquaredDiff / (values.Length - 1);
        }

        /// <summary>
        /// Calculates full aggregate statistics.
        /// </summary>
        public static AggregateStats CalculateFullStats(NativeArray<StatContribution> contributions, uint currentTick)
        {
            if (contributions.Length == 0)
            {
                return new AggregateStats { LastCalculatedTick = currentTick };
            }

            float min = float.MaxValue;
            float max = float.MinValue;
            float sum = 0;
            float weightedSum = 0;
            float totalWeight = 0;
            int count = 0;

            for (int i = 0; i < contributions.Length; i++)
            {
                var c = contributions[i];
                if (c.IsActive == 0) continue;
                
                sum += c.Value;
                weightedSum += c.Value * c.Weight;
                totalWeight += c.Weight;
                min = math.min(min, c.Value);
                max = math.max(max, c.Value);
                count++;
            }

            float mean = count > 0 ? sum / count : 0;
            float weightedMean = totalWeight > 0 ? weightedSum / totalWeight : 0;

            // Calculate variance
            float variance = 0;
            if (count > 1)
            {
                float sumSquaredDiff = 0;
                for (int i = 0; i < contributions.Length; i++)
                {
                    if (contributions[i].IsActive == 0) continue;
                    float diff = contributions[i].Value - mean;
                    sumSquaredDiff += diff * diff;
                }
                variance = sumSquaredDiff / (count - 1);
            }

            return new AggregateStats
            {
                Mean = mean,
                WeightedMean = weightedMean,
                Min = count > 0 ? min : 0,
                Max = count > 0 ? max : 0,
                Variance = variance,
                StandardDeviation = math.sqrt(variance),
                Count = count,
                TotalWeight = totalWeight,
                LastCalculatedTick = currentTick
            };
        }

        /// <summary>
        /// Finds bottlenecks - values below threshold.
        /// </summary>
        public static void FindBottlenecks(
            NativeArray<StatContribution> contributions,
            NativeArray<ushort> resourceTypeIds,
            NativeArray<Entity> entities,
            float threshold,
            ref DynamicBuffer<BottleneckBuffer> bottlenecks,
            uint currentTick)
        {
            bottlenecks.Clear();
            
            for (int i = 0; i < contributions.Length; i++)
            {
                var c = contributions[i];
                if (c.IsActive == 0) continue;
                
                float ratio = c.Value;
                if (ratio < threshold)
                {
                    float deficitRatio = 1f - (ratio / threshold);
                    byte severity = (byte)(deficitRatio > 0.66f ? 2 : deficitRatio > 0.33f ? 1 : 0);
                    
                    if (bottlenecks.Length < bottlenecks.Capacity)
                    {
                        bottlenecks.Add(new BottleneckBuffer
                        {
                            Entry = new BottleneckEntry
                            {
                                ResourceTypeId = resourceTypeIds[i],
                                BottleneckEntity = entities[i],
                                BottleneckValue = c.Value,
                                ThresholdValue = threshold,
                                DeficitRatio = deficitRatio,
                                Severity = severity,
                                DetectedTick = currentTick
                            }
                        });
                    }
                }
            }
            
            SortBottlenecksBySeverity(ref bottlenecks);
        }

        private static void SortBottlenecksBySeverity(ref DynamicBuffer<BottleneckBuffer> bottlenecks)
        {
            for (int i = 0; i < bottlenecks.Length - 1; i++)
            {
                for (int j = 0; j < bottlenecks.Length - i - 1; j++)
                {
                    if (bottlenecks[j].Entry.DeficitRatio < bottlenecks[j + 1].Entry.DeficitRatio)
                    {
                        var temp = bottlenecks[j];
                        bottlenecks[j] = bottlenecks[j + 1];
                        bottlenecks[j + 1] = temp;
                    }
                }
            }
        }

        /// <summary>
        /// Calculates group composition from contributions.
        /// </summary>
        public static GroupComposition CalculateComposition(
            NativeArray<StatContribution> contributions,
            in AggregateConfig config)
        {
            int total = 0;
            int active = 0;
            int healthy = 0;
            int damaged = 0;
            int critical = 0;
            float readinessSum = 0;
            float readinessWeight = 0;

            for (int i = 0; i < contributions.Length; i++)
            {
                var c = contributions[i];
                total++;
                
                if (c.IsActive == 0 && config.IncludeInactive == 0) continue;
                if (c.IsActive != 0) active++;
                
                if (c.Value >= config.HealthyThreshold)
                {
                    healthy++;
                }
                else if (c.Value >= config.CriticalThreshold)
                {
                    damaged++;
                }
                else
                {
                    critical++;
                }

                readinessSum += c.Value * c.Weight;
                readinessWeight += c.Weight;
            }

            return new GroupComposition
            {
                TotalCount = total,
                ActiveCount = active,
                HealthyCount = healthy,
                DamagedCount = damaged,
                CriticalCount = critical,
                HealthyRatio = total > 0 ? (float)healthy / total : 0,
                ReadinessScore = readinessWeight > 0 ? readinessSum / readinessWeight : 0
            };
        }

        /// <summary>
        /// Calculates efficiency with bottleneck penalty.
        /// </summary>
        public static float CalculateEfficiency(
            in AggregateStats stats,
            in DynamicBuffer<BottleneckBuffer> bottlenecks,
            float bottleneckPenaltyMultiplier)
        {
            float baseEfficiency = stats.WeightedMean;
            
            float penalty = 0;
            for (int i = 0; i < bottlenecks.Length; i++)
            {
                penalty += bottlenecks[i].Entry.DeficitRatio * bottleneckPenaltyMultiplier;
            }
            
            penalty = math.min(penalty, 0.5f);
            
            return baseEfficiency * (1f - penalty);
        }

        /// <summary>
        /// Calculates cohesion (how similar values are).
        /// </summary>
        public static float CalculateCohesion(in AggregateStats stats)
        {
            if (stats.Count <= 1) return 1f;
            
            float cv = stats.Mean > 0 ? stats.StandardDeviation / stats.Mean : 0;
            
            return math.saturate(1f - cv);
        }

        /// <summary>
        /// Gets the worst bottleneck (highest deficit).
        /// </summary>
        public static bool TryGetWorstBottleneck(
            in DynamicBuffer<BottleneckBuffer> bottlenecks,
            out BottleneckEntry worst)
        {
            worst = default;
            if (bottlenecks.Length == 0) return false;
            
            worst = bottlenecks[0].Entry;
            return true;
        }

        /// <summary>
        /// Counts bottlenecks by severity level.
        /// </summary>
        public static void CountBottlenecksBySeverity(
            in DynamicBuffer<BottleneckBuffer> bottlenecks,
            out int minor,
            out int moderate,
            out int critical)
        {
            minor = 0;
            moderate = 0;
            critical = 0;
            
            for (int i = 0; i < bottlenecks.Length; i++)
            {
                switch (bottlenecks[i].Entry.Severity)
                {
                    case 0: minor++; break;
                    case 1: moderate++; break;
                    case 2: critical++; break;
                }
            }
        }
    }
}

