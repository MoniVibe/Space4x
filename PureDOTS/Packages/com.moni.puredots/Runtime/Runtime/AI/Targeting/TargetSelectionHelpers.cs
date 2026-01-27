using Unity.Mathematics;
using Unity.Entities;
using Unity.Collections;
using Unity.Burst;

namespace PureDOTS.Runtime.AI.Targeting
{
    /// <summary>
    /// Static helpers for target selection and prioritization.
    /// </summary>
    [BurstCompile]
    public static class TargetSelectionHelpers
    {
        /// <summary>
        /// Default target selection configuration.
        /// </summary>
        public static TargetSelectionConfig DefaultConfig => new TargetSelectionConfig
        {
            MaxRange = 50f,
            OptimalRange = 10f,
            ThreatWeight = 0.3f,
            DistanceWeight = 0.4f,
            HistoryWeight = 0.2f,
            ValueWeight = 0.1f,
            PreferWounded = 0,
            PreferEngaged = 0
        };

        /// <summary>
        /// Calculates overall target priority from weighted factors.
        /// </summary>
        public static float CalculatePriority(
            float threatScore,
            float distanceScore,
            float historyScore,
            float valueScore,
            in TargetSelectionConfig config)
        {
            return threatScore * config.ThreatWeight +
                   distanceScore * config.DistanceWeight +
                   historyScore * config.HistoryWeight +
                   valueScore * config.ValueWeight;
        }

        /// <summary>
        /// Calculates distance-based score (closer = higher).
        /// </summary>
        public static float CalculateDistanceScore(float distance, float maxRange, float optimalRange)
        {
            if (distance > maxRange) return 0;
            if (distance <= optimalRange) return 1f;
            
            // Linear falloff from optimal to max range
            return 1f - (distance - optimalRange) / (maxRange - optimalRange);
        }

        /// <summary>
        /// Calculates threat score from assessment.
        /// </summary>
        public static float CalculateThreatScore(in ThreatAssessment threat, bool preferEngaged)
        {
            float score = threat.CurrentThreat * math.max(0.1f, threat.AggressionLevel);
            if (threat.IsHostile != 0) score *= 1.5f;
            if (preferEngaged && threat.IsEngaged != 0) score *= 1.2f;
            return math.saturate(score);
        }

        /// <summary>
        /// Calculates revenge/history score from damage memory.
        /// </summary>
        public static float CalculateHistoryScore(in DynamicBuffer<DamageMemory> memory, Entity target)
        {
            for (int i = 0; i < memory.Length; i++)
            {
                if (memory[i].AttackerEntity == target)
                {
                    // Recent damage and hit count both matter
                    float recentFactor = math.min(1f, memory[i].RecentDamage * 0.01f);
                    float countFactor = math.min(1f, memory[i].HitCount * 0.1f);
                    return math.max(recentFactor, countFactor);
                }
            }
            return 0;
        }

        /// <summary>
        /// Filters candidates by range.
        /// </summary>
        public static void FilterByRange(ref DynamicBuffer<TargetCandidate> candidates, float maxRange)
        {
            for (int i = 0; i < candidates.Length; i++)
            {
                var candidate = candidates[i];
                candidate.IsValid = (byte)(candidate.Distance <= maxRange ? 1 : 0);
                candidates[i] = candidate;
            }
        }

        /// <summary>
        /// Sorts candidates by priority (highest first).
        /// </summary>
        public static void SortByPriority(ref DynamicBuffer<TargetCandidate> candidates)
        {
            // Simple bubble sort for small buffers
            for (int i = 0; i < candidates.Length - 1; i++)
            {
                for (int j = 0; j < candidates.Length - i - 1; j++)
                {
                    if (candidates[j].Priority < candidates[j + 1].Priority)
                    {
                        var temp = candidates[j];
                        candidates[j] = candidates[j + 1];
                        candidates[j + 1] = temp;
                    }
                }
            }
        }

        /// <summary>
        /// Adds damage to memory buffer, updating or creating entry.
        /// </summary>
        public static void RecordDamage(ref DynamicBuffer<DamageMemory> memory, Entity attacker, float damage, uint currentTick)
        {
            for (int i = 0; i < memory.Length; i++)
            {
                if (memory[i].AttackerEntity == attacker)
                {
                    var entry = memory[i];
                    entry.TotalDamageReceived += damage;
                    entry.RecentDamage += damage;
                    entry.HitCount++;
                    entry.LastHitTick = currentTick;
                    memory[i] = entry;
                    return;
                }
            }

            // New attacker
            if (memory.Length < memory.Capacity)
            {
                memory.Add(new DamageMemory
                {
                    AttackerEntity = attacker,
                    TotalDamageReceived = damage,
                    RecentDamage = damage,
                    HitCount = 1,
                    LastHitTick = currentTick
                });
            }
        }

        /// <summary>
        /// Decays recent damage over time.
        /// </summary>
        public static void DecayDamageMemory(ref DynamicBuffer<DamageMemory> memory, float decayRate)
        {
            for (int i = 0; i < memory.Length; i++)
            {
                var entry = memory[i];
                entry.RecentDamage *= (1f - decayRate);
                memory[i] = entry;
            }
        }

        /// <summary>
        /// Gets the highest priority valid target.
        /// </summary>
        public static Entity GetBestTarget(in DynamicBuffer<TargetCandidate> candidates)
        {
            Entity best = Entity.Null;
            float bestPriority = float.MinValue;

            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i].IsValid != 0 && candidates[i].Priority > bestPriority)
                {
                    bestPriority = candidates[i].Priority;
                    best = candidates[i].CandidateEntity;
                }
            }

            return best;
        }

        /// <summary>
        /// Evaluates all candidates and updates their priority scores.
        /// </summary>
        public static void EvaluateCandidates(
            ref DynamicBuffer<TargetCandidate> candidates,
            in DynamicBuffer<DamageMemory> memory,
            in TargetSelectionConfig config,
            float3 myPosition)
        {
            for (int i = 0; i < candidates.Length; i++)
            {
                var candidate = candidates[i];
                if (candidate.IsValid == 0) continue;

                float distanceScore = CalculateDistanceScore(candidate.Distance, config.MaxRange, config.OptimalRange);
                float historyScore = CalculateHistoryScore(memory, candidate.CandidateEntity);
                
                // Threat and value would come from other components
                float threatScore = 0.5f; // Default
                float valueScore = 0.5f;  // Default

                candidate.Priority = CalculatePriority(threatScore, distanceScore, historyScore, valueScore, config);
                candidates[i] = candidate;
            }
        }

        /// <summary>
        /// Clears old damage memories.
        /// </summary>
        public static void PruneDamageMemory(ref DynamicBuffer<DamageMemory> memory, uint currentTick, uint maxAge)
        {
            for (int i = memory.Length - 1; i >= 0; i--)
            {
                if (currentTick - memory[i].LastHitTick > maxAge)
                {
                    memory.RemoveAt(i);
                }
            }
        }
    }
}

