using PureDOTS.Runtime.Navigation;
using PureDOTS.Runtime.Spatial;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Runtime.AI
{
    /// <summary>
    /// Burst-safe utilities for AI sensor queries and scoring.
    /// All vector and array parameters use ref for Burst compatibility.
    /// </summary>
    [BurstCompile]
    public static class AISensorUtilities
    {
        /// <summary>
        /// Samples nearby entities using spatial grid and categorizes by sensor category.
        /// </summary>
        [BurstCompile]
        public static void SampleSpatialEntities(
            ref float3 position,
            float radius,
            ref NativeArray<SpatialGridEntry> spatialEntries,
            AISensorCategory primaryCategory,
            AISensorCategory secondaryCategory,
            ref NativeList<AISensorReading> results)
        {
            results.Clear();
            var radiusSq = radius * radius;

            for (int i = 0; i < spatialEntries.Length; i++)
            {
                var entry = spatialEntries[i];
                var distSq = math.distancesq(position, entry.Position);
                if (distSq > radiusSq)
                {
                    continue;
                }

                // Determine category (simplified - would need component lookups for real categorization)
                var category = DetermineCategory(entry, primaryCategory, secondaryCategory);
                if (category == AISensorCategory.None)
                {
                    continue;
                }

                var normalizedScore = 1f - (math.sqrt(distSq) / radius); // Closer = higher score
                results.Add(new AISensorReading
                {
                    Target = entry.Entity,
                    DistanceSq = distSq,
                    NormalizedScore = normalizedScore,
                    CellId = entry.CellId,
                    SpatialVersion = 0, // Would need to pass grid version
                    Category = category
                });
            }

            // Sort by score (highest first)
            results.Sort(new SensorReadingComparer());
        }

        /// <summary>
        /// Filters sensor readings by category.
        /// </summary>
        [BurstCompile]
        public static void FilterByCategory(
            ref NativeArray<AISensorReading> readings,
            AISensorCategory category,
            ref NativeList<AISensorReading> results)
        {
            results.Clear();
            for (int i = 0; i < readings.Length; i++)
            {
                if (readings[i].Category == category)
                {
                    results.Add(readings[i]);
                }
            }
        }

        /// <summary>
        /// Finds the best scoring target from sensor readings.
        /// </summary>
        [BurstCompile]
        public static bool TryFindBestTarget(
            ref NativeArray<AISensorReading> readings,
            AISensorCategory category,
            out Entity bestTarget,
            out float bestScore)
        {
            bestTarget = Entity.Null;
            bestScore = 0f;
            bool found = false;

            for (int i = 0; i < readings.Length; i++)
            {
                var reading = readings[i];
                if (reading.Category == category && reading.NormalizedScore > bestScore)
                {
                    bestScore = reading.NormalizedScore;
                    bestTarget = reading.Target;
                    found = true;
                }
            }

            return found;
        }

        /// <summary>
        /// Determines sensor category for an entity (simplified placeholder).
        /// </summary>
        [BurstCompile]
        private static AISensorCategory DetermineCategory(
            in SpatialGridEntry entry,
            AISensorCategory primaryCategory,
            AISensorCategory secondaryCategory)
        {
            // In real implementation, would query entity components
            // For now, return primary category as placeholder
            return primaryCategory;
        }

        /// <summary>
        /// Comparer for sorting sensor readings by score.
        /// </summary>
        private struct SensorReadingComparer : System.Collections.Generic.IComparer<AISensorReading>
        {
            public int Compare(AISensorReading x, AISensorReading y)
            {
                return y.NormalizedScore.CompareTo(x.NormalizedScore); // Descending
            }
        }
    }

    /// <summary>
    /// Utility functions for scoring AI actions using utility curves.
    /// </summary>
    [BurstCompile]
    public static class AIUtilityScoring
    {
        /// <summary>
        /// Evaluates an action's utility score using sensor readings and utility curves.
        /// </summary>
        [BurstCompile]
        public static float EvaluateAction(
            ref AIUtilityActionBlob actionBlob,
            ref NativeArray<AISensorReading> sensorReadings)
        {
            float totalScore = 0f;

            for (int i = 0; i < actionBlob.Factors.Length; i++)
            {
                var factor = actionBlob.Factors[i];
                if (factor.SensorIndex >= sensorReadings.Length)
                {
                    continue;
                }

                var reading = sensorReadings[factor.SensorIndex];
                var rawValue = reading.NormalizedScore;

                // Apply threshold
                if (rawValue < factor.Threshold)
                {
                    continue;
                }

                // Apply response curve (power function)
                var normalized = math.pow(rawValue / factor.MaxValue, factor.ResponsePower);
                var contribution = normalized * factor.Weight;
                totalScore += contribution;
            }

            return totalScore;
        }

        /// <summary>
        /// Finds the best action from a behavior archetype.
        /// </summary>
        [BurstCompile]
        public static bool TryFindBestAction(
            ref AIUtilityArchetypeBlob archetypeBlob,
            ref NativeArray<AISensorReading> sensorReadings,
            out byte bestActionIndex,
            out float bestScore)
        {
            bestActionIndex = 0;
            bestScore = float.MinValue;
            bool found = false;

            for (byte i = 0; i < archetypeBlob.Actions.Length; i++)
            {
                ref var actionBlob = ref archetypeBlob.Actions[i];
                var score = EvaluateAction(ref actionBlob, ref sensorReadings);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestActionIndex = i;
                    found = true;
                }
            }

            return found;
        }
    }

    /// <summary>
    /// Utilities for integrating AI steering with flow field navigation.
    /// </summary>
    [BurstCompile]
    public static class AISteeringUtilities
    {
        /// <summary>
        /// Blends flow field direction with steering forces.
        /// </summary>
        [BurstCompile]
        public static void BlendWithFlowField(
            ref float2 flowDirection,
            ref float2 steeringDirection,
            float flowWeight,
            float steeringWeight,
            out float2 result)
        {
            var blended = flowDirection * flowWeight + steeringDirection * steeringWeight;
            if (math.lengthsq(blended) > 0.01f)
            {
                result = math.normalize(blended);
            }
            else
            {
                result = float2.zero;
            }
        }

        /// <summary>
        /// Computes separation force from nearby agents.
        /// </summary>
        [BurstCompile]
        public static void ComputeSeparation(
            ref float3 position,
            ref NativeArray<AISensorReading> sensorReadings,
            float separationRadius,
            float separationStrength,
            out float2 result)
        {
            var separation = float2.zero;
            int count = 0;

            for (int i = 0; i < sensorReadings.Length; i++)
            {
                var reading = sensorReadings[i];
                if (reading.Category != AISensorCategory.Villager && 
                    reading.Category != AISensorCategory.TransportUnit)
                {
                    continue;
                }

                var dist = math.sqrt(reading.DistanceSq);
                if (dist < separationRadius && dist > 0.01f)
                {
                    // Would need entity position lookup - simplified for now
                    // var direction = math.normalize(position - otherPosition);
                    // separation += direction * (separationRadius / dist);
                    count++;
                }
            }

            if (count > 0 && math.lengthsq(separation) > 0.01f)
            {
                separation = math.normalize(separation) * separationStrength;
            }

            result = separation;
        }
    }
}


