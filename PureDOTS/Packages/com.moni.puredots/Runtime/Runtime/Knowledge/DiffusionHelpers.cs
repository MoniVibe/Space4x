using Unity.Mathematics;
using Unity.Entities;
using Unity.Collections;
using Unity.Burst;

namespace PureDOTS.Runtime.Knowledge
{
    /// <summary>
    /// Static helpers for knowledge diffusion calculations.
    /// </summary>
    [BurstCompile]
    public static class DiffusionHelpers
    {
        /// <summary>
        /// Default diffusion infrastructure.
        /// </summary>
        public static DiffusionInfrastructure DefaultInfrastructure => new DiffusionInfrastructure
        {
            InfrastructureTier = 1,
            RelayQuality = 0.5f,
            ReceptionBonus = 0,
            TransmissionBonus = 0,
            HasLibrary = 0,
            HasAcademy = 0
        };

        /// <summary>
        /// Calculates travel time for knowledge diffusion.
        /// </summary>
        public static float CalculateTravelTime(
            float distance,
            byte knowledgeTier,
            float linkQuality,
            float infrastructureTier)
        {
            // Base travel time from distance
            float baseTime = distance * 0.1f;
            
            // Higher tier = slower spread
            float tierPenalty = 1f + knowledgeTier * 0.3f;
            
            // Better infrastructure = faster
            float infraBonus = 1f / (1f + infrastructureTier * 0.1f);
            
            // Link quality affects speed
            float linkFactor = 2f - linkQuality;
            
            return math.max(1f, baseTime * tierPenalty * infraBonus * linkFactor);
        }

        /// <summary>
        /// Calculates diffusion rate at destination.
        /// </summary>
        public static float CalculateDiffusionRate(
            float baseDiffusionRate,
            in DiffusionInfrastructure infra,
            float sourceRelation,
            bool hasDiplomaticTies)
        {
            // Base rate from knowledge
            float rate = baseDiffusionRate;
            
            // Infrastructure bonuses
            rate *= 1f + infra.ReceptionBonus;
            if (infra.HasLibrary != 0) rate *= 1.2f;
            if (infra.HasAcademy != 0) rate *= 1.15f;
            
            // Relations affect sharing
            if (hasDiplomaticTies)
            {
                rate *= 0.5f + sourceRelation * 0.5f;
            }
            
            return rate;
        }

        /// <summary>
        /// Calculates decay rate when cut off from source.
        /// </summary>
        public static float CalculateDecayRate(
            byte knowledgeTier,
            float masteryLevel,
            in DiffusionInfrastructure infra)
        {
            // Higher tier decays faster without reinforcement
            float tierDecay = 0.01f * knowledgeTier;
            
            // Better mastery resists decay
            float masteryResist = masteryLevel * 0.5f;
            
            // Libraries preserve knowledge
            float archiveResist = infra.HasLibrary != 0 ? 0.3f : 0;
            
            return math.max(0, tierDecay * (1f - masteryResist - archiveResist));
        }

        /// <summary>
        /// Checks if location meets requirements for knowledge.
        /// </summary>
        public static bool MeetsRequirements(
            in KnowledgeDefinition knowledge,
            in DiffusionInfrastructure infra,
            in DynamicBuffer<AdoptedKnowledge> adopted)
        {
            // Check infrastructure tier
            if (infra.InfrastructureTier < knowledge.RequiresInfrastructure)
                return false;
            
            // Check prerequisites (simplified - just tier check)
            if (knowledge.Tier > 0)
            {
                bool hasPrereq = false;
                for (int i = 0; i < adopted.Length; i++)
                {
                    if (adopted[i].Category == knowledge.Category &&
                        adopted[i].Tier >= knowledge.Tier - 1)
                    {
                        hasPrereq = true;
                        break;
                    }
                }
                if (!hasPrereq) return false;
            }
            
            return true;
        }

        /// <summary>
        /// Updates diffusion queue progress.
        /// </summary>
        public static void UpdateDiffusionQueue(
            ref DynamicBuffer<DiffusionQueue> queue,
            float deltaTime,
            float linkQuality)
        {
            for (int i = 0; i < queue.Length; i++)
            {
                var entry = queue[i];
                entry.TravelProgress += entry.TravelSpeed * deltaTime * linkQuality;
                entry.TravelProgress = math.saturate(entry.TravelProgress);
                queue[i] = entry;
            }
        }

        /// <summary>
        /// Checks for completed diffusions in queue and removes them.
        /// </summary>
        public static int ProcessCompletedDiffusions(
            ref DynamicBuffer<DiffusionQueue> queue,
            ref DynamicBuffer<AdoptedKnowledge> adopted,
            uint currentTick)
        {
            int completed = 0;
            
            for (int i = queue.Length - 1; i >= 0; i--)
            {
                if (queue[i].TravelProgress >= 1f)
                {
                    // Add to adopted
                    adopted.Add(new AdoptedKnowledge
                    {
                        KnowledgeId = queue[i].KnowledgeId,
                        Category = KnowledgeCategory.Technology, // Would need lookup
                        Tier = 0,
                        AdoptedTick = currentTick,
                        MasteryLevel = 0.1f,
                        CanTransmit = 0
                    });
                    
                    // Remove from queue
                    queue.RemoveAt(i);
                    completed++;
                }
            }
            
            return completed;
        }

        /// <summary>
        /// Calculates research pact sync progress.
        /// </summary>
        public static float CalculatePactSyncProgress(
            in ResearchPact pact,
            float partnerProgress,
            float myProgress)
        {
            if (pact.IsActive == 0) return 0;
            
            float gap = partnerProgress - myProgress;
            if (gap <= 0) return 0;
            
            return gap * pact.SyncRate * pact.ShareRatio;
        }

        /// <summary>
        /// Estimates arrival tick for queued knowledge.
        /// </summary>
        public static uint EstimateArrivalTick(
            uint currentTick,
            float remainingProgress,
            float travelSpeed)
        {
            if (travelSpeed <= 0) return uint.MaxValue;
            
            float remainingTime = (1f - remainingProgress) / travelSpeed;
            return currentTick + (uint)remainingTime;
        }

        /// <summary>
        /// Calculates mastery gain rate.
        /// </summary>
        public static float CalculateMasteryGain(
            float currentMastery,
            in DiffusionInfrastructure infra,
            float usageRate)
        {
            // Mastery grows with use
            float baseGain = 0.01f * usageRate;
            
            // Academy accelerates mastery
            float academyBonus = infra.HasAcademy != 0 ? 1.5f : 1f;
            
            // Diminishing returns at high mastery
            float diminishing = 1f - currentMastery * 0.5f;
            
            return baseGain * academyBonus * math.max(0.1f, diminishing);
        }

        /// <summary>
        /// Checks if entity can transmit knowledge.
        /// </summary>
        public static bool CanTransmitKnowledge(
            in AdoptedKnowledge knowledge,
            in DiffusionInfrastructure infra)
        {
            // Need sufficient mastery
            if (knowledge.MasteryLevel < 0.5f) return false;
            
            // Need transmission capability
            if (infra.TransmissionBonus <= 0 && infra.InfrastructureTier < 2) return false;
            
            return true;
        }

        /// <summary>
        /// Gets effective link throughput.
        /// </summary>
        public static float GetEffectiveThroughput(
            in DiffusionLink link,
            in DiffusionInfrastructure sourceInfra,
            in DiffusionInfrastructure targetInfra)
        {
            float baseThroughput = link.ThroughputLimit;
            float sourceBonus = 1f + sourceInfra.TransmissionBonus;
            float targetBonus = 1f + targetInfra.ReceptionBonus;
            float qualityFactor = link.LinkQuality;
            
            return baseThroughput * sourceBonus * targetBonus * qualityFactor;
        }

        /// <summary>
        /// Queues knowledge for diffusion.
        /// </summary>
        public static bool QueueKnowledgeDiffusion(
            ref DynamicBuffer<DiffusionQueue> queue,
            in KnowledgeDefinition knowledge,
            Entity sourceEntity,
            float travelTime,
            uint currentTick)
        {
            // Check if already in queue
            for (int i = 0; i < queue.Length; i++)
            {
                if (queue[i].KnowledgeId.Equals(knowledge.KnowledgeId))
                    return false;
            }
            
            // Check capacity
            if (queue.Length >= queue.Capacity)
                return false;
            
            queue.Add(new DiffusionQueue
            {
                KnowledgeId = knowledge.KnowledgeId,
                SourceEntity = sourceEntity,
                TravelProgress = 0,
                TravelSpeed = 1f / math.max(1f, travelTime),
                QueuedTick = currentTick
            });
            
            return true;
        }

        /// <summary>
        /// Updates mastery for all adopted knowledge.
        /// </summary>
        public static void UpdateMastery(
            ref DynamicBuffer<AdoptedKnowledge> adopted,
            in DiffusionInfrastructure infra,
            float deltaTime,
            float baseUsageRate)
        {
            for (int i = 0; i < adopted.Length; i++)
            {
                var knowledge = adopted[i];
                
                float masteryGain = CalculateMasteryGain(
                    knowledge.MasteryLevel,
                    infra,
                    baseUsageRate);
                
                knowledge.MasteryLevel = math.saturate(knowledge.MasteryLevel + masteryGain * deltaTime);
                knowledge.CanTransmit = (byte)(knowledge.MasteryLevel >= 0.5f ? 1 : 0);
                
                adopted[i] = knowledge;
            }
        }

        /// <summary>
        /// Applies decay to adopted knowledge when cut off.
        /// </summary>
        public static void ApplyKnowledgeDecay(
            ref DynamicBuffer<AdoptedKnowledge> adopted,
            in DiffusionInfrastructure infra,
            float deltaTime)
        {
            for (int i = adopted.Length - 1; i >= 0; i--)
            {
                var knowledge = adopted[i];
                
                float decayRate = CalculateDecayRate(
                    knowledge.Tier,
                    knowledge.MasteryLevel,
                    infra);
                
                knowledge.MasteryLevel -= decayRate * deltaTime;
                
                if (knowledge.MasteryLevel <= 0)
                {
                    // Knowledge lost
                    adopted.RemoveAt(i);
                }
                else
                {
                    knowledge.CanTransmit = (byte)(knowledge.MasteryLevel >= 0.5f ? 1 : 0);
                    adopted[i] = knowledge;
                }
            }
        }

        /// <summary>
        /// Checks if knowledge is already adopted.
        /// </summary>
        public static bool HasKnowledge(
            in DynamicBuffer<AdoptedKnowledge> adopted,
            FixedString64Bytes knowledgeId)
        {
            for (int i = 0; i < adopted.Length; i++)
            {
                if (adopted[i].KnowledgeId.Equals(knowledgeId))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Gets mastery level for specific knowledge.
        /// </summary>
        public static float GetMasteryLevel(
            in DynamicBuffer<AdoptedKnowledge> adopted,
            FixedString64Bytes knowledgeId)
        {
            for (int i = 0; i < adopted.Length; i++)
            {
                if (adopted[i].KnowledgeId.Equals(knowledgeId))
                    return adopted[i].MasteryLevel;
            }
            return 0;
        }
    }
}

