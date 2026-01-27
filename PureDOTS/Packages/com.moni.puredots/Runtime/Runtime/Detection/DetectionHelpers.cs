using Unity.Mathematics;
using Unity.Entities;
using Unity.Collections;
using Unity.Burst;

namespace PureDOTS.Runtime.Detection
{
    /// <summary>
    /// Static helpers for stealth and perception calculations.
    /// </summary>
    [BurstCompile]
    public static class DetectionHelpers
    {
        /// <summary>
        /// Default detection configuration.
        /// </summary>
        public static DetectionConfig DefaultConfig => new DetectionConfig
        {
            BaseSuccessChance = 0.5f,
            DistanceFalloff = 0.02f,
            MovementDetectionBonus = 0.2f,
            AlertnessMultiplier = 1.5f,
            CheckIntervalTicks = 30
        };

        /// <summary>
        /// Calculates effective stealth rating.
        /// </summary>
        public static float CalculateEffectiveStealth(in StealthStats stats)
        {
            float base_rating = stats.BaseStealthRating + stats.EquipmentBonus;
            
            // Apply state bonus
            float stateBonus = GetStateBonus(stats.CurrentState);
            
            // Apply environment and movement
            float effective = base_rating + stateBonus + stats.EnvironmentBonus - stats.MovementPenalty;
            
            return math.clamp(effective, 0f, 100f);
        }

        /// <summary>
        /// Gets stealth bonus from visibility state.
        /// </summary>
        public static float GetStateBonus(VisibilityState state)
        {
            return state switch
            {
                VisibilityState.Exposed => 0f,
                VisibilityState.Concealed => 25f,
                VisibilityState.Hidden => 50f,
                VisibilityState.Invisible => 75f,
                _ => 0f
            };
        }

        /// <summary>
        /// Calculates effective perception rating.
        /// </summary>
        public static float CalculateEffectivePerception(in PerceptionStats stats)
        {
            float base_rating = stats.BasePerceptionRating + stats.EquipmentBonus;
            float alertnessBonus = stats.AlertnessLevel * 10f;
            
            return math.clamp(base_rating + alertnessBonus, 0f, 100f);
        }

        /// <summary>
        /// Performs a detection check.
        /// </summary>
        public static bool PerformDetectionCheck(
            float perceptionRating,
            float stealthRating,
            float distance,
            in DetectionConfig config,
            uint seed,
            out float confidence)
        {
            // Base chance modified by perception vs stealth
            float chance = config.BaseSuccessChance;
            chance += (perceptionRating - stealthRating) * 0.01f;
            
            // Distance penalty
            chance -= distance * config.DistanceFalloff;
            
            // Clamp chance
            chance = math.clamp(chance, 0.05f, 0.95f);
            
            // Roll
            float roll = DeterministicRandom(seed) / (float)uint.MaxValue;
            bool detected = roll < chance;
            
            // Confidence based on margin
            confidence = detected ? math.saturate(chance - roll + 0.5f) : 0f;
            
            return detected;
        }

        /// <summary>
        /// Checks if target is in detection range.
        /// </summary>
        public static bool IsInDetectionRange(float3 observerPos, float3 targetPos, float detectionRadius)
        {
            return math.distancesq(observerPos, targetPos) <= detectionRadius * detectionRadius;
        }

        /// <summary>
        /// Calculates visibility modifier from environment.
        /// </summary>
        public static float GetEnvironmentModifier(float3 position, in DynamicBuffer<Entity> zoneEntities, in VisibilityZone zone)
        {
            float distSq = math.distancesq(position, zone.Center);
            if (distSq > zone.Radius * zone.Radius)
                return 0f;
            
            // Falloff towards edge
            float dist = math.sqrt(distSq);
            float falloff = 1f - (dist / zone.Radius);
            
            return zone.StealthModifier * falloff;
        }

        /// <summary>
        /// Updates alert level based on detection.
        /// </summary>
        public static AlertState UpdateAlertState(
            in AlertState current,
            bool detected,
            float3 detectedPosition,
            uint currentTick,
            float alertDecayRate)
        {
            var result = current;
            
            if (detected)
            {
                result.AlertLevel = math.min(2f, current.AlertLevel + 0.5f);
                result.LastAlertPosition = detectedPosition;
                result.AlertStartTick = currentTick;
                result.AlertDecayTick = currentTick + 300; // 5 seconds before decay
            }
            else if (currentTick > current.AlertDecayTick)
            {
                result.AlertLevel = math.max(0f, current.AlertLevel - alertDecayRate);
            }
            
            result.IsInvestigating = (byte)(result.AlertLevel >= 1f ? 1 : 0);
            
            return result;
        }

        /// <summary>
        /// Adds or updates detection result.
        /// </summary>
        public static void RecordDetection(
            ref DynamicBuffer<DetectionResult> results,
            Entity detected,
            float confidence,
            float3 position,
            uint tick)
        {
            // Update existing
            for (int i = 0; i < results.Length; i++)
            {
                if (results[i].DetectedEntity == detected)
                {
                    var entry = results[i];
                    entry.Confidence = math.max(entry.Confidence, confidence);
                    entry.LastKnownPosition = position;
                    entry.DetectionTick = tick;
                    entry.IsCurrentlyVisible = 1;
                    results[i] = entry;
                    return;
                }
            }

            // Add new
            if (results.Length < results.Capacity)
            {
                results.Add(new DetectionResult
                {
                    DetectedEntity = detected,
                    Confidence = confidence,
                    LastKnownPosition = position,
                    DetectionTick = tick,
                    IsCurrentlyVisible = 1
                });
            }
        }

        /// <summary>
        /// Marks entities no longer visible.
        /// </summary>
        public static void DecayDetections(ref DynamicBuffer<DetectionResult> results, uint currentTick, uint maxAge)
        {
            for (int i = results.Length - 1; i >= 0; i--)
            {
                if (currentTick - results[i].DetectionTick > maxAge)
                {
                    results.RemoveAt(i);
                }
                else
                {
                    var entry = results[i];
                    entry.IsCurrentlyVisible = 0;
                    entry.Confidence *= 0.95f; // Decay confidence
                    results[i] = entry;
                }
            }
        }

        /// <summary>
        /// Finds best cover point.
        /// </summary>
        public static Entity FindBestCover(
            float3 position,
            float3 threatDirection,
            NativeArray<Entity> coverEntities,
            NativeArray<CoverPoint> coverPoints,
            float maxDistance)
        {
            Entity best = Entity.Null;
            float bestScore = float.MinValue;

            for (int i = 0; i < coverPoints.Length; i++)
            {
                if (coverPoints[i].IsOccupied != 0) continue;
                
                float dist = math.distance(position, coverPoints[i].Position);
                if (dist > maxDistance) continue;
                
                // Score: cover value vs distance, prefer cover facing threat
                float facingBonus = math.dot(coverPoints[i].CoverDirection, -threatDirection);
                float score = coverPoints[i].CoverValue + facingBonus * 0.5f - dist * 0.01f;
                
                if (score > bestScore)
                {
                    bestScore = score;
                    best = coverEntities[i];
                }
            }

            return best;
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
    }
}

