using Unity.Mathematics;
using Unity.Entities;
using Unity.Collections;
using Unity.Burst;

namespace PureDOTS.Runtime.Transformation
{
    /// <summary>
    /// Static helpers for entity transformation.
    /// </summary>
    [BurstCompile]
    public static class TransformationHelpers
    {
        /// <summary>
        /// Default transformation configuration.
        /// </summary>
        public static TransformationConfig DefaultConfig => new TransformationConfig
        {
            BaseTransformChance = 0.1f,
            PhysiqueWeighting = 0.3f,
            WillWeighting = 0.4f,
            MinDelayTicks = 3600,      // 1 minute
            MaxDelayTicks = 216000,    // 1 hour
            AllowIdentityRetention = 1,
            MemoryDecayRate = 0.001f
        };

        /// <summary>
        /// Checks if entity meets transformation requirements.
        /// </summary>
        public static bool MeetsRequirements(
            byte physique,
            byte will,
            byte alignment,
            in TransformationTypeRequirement requirement)
        {
            if (physique < requirement.MinPhysique) return false;
            if (will < requirement.MinWill) return false;
            if (requirement.RequiredAlignment != 0 && alignment != requirement.RequiredAlignment) return false;
            return true;
        }

        /// <summary>
        /// Calculates transformation chance.
        /// </summary>
        public static float CalculateTransformChance(
            float baseChance,
            byte physique,
            byte will,
            float triggerMagnitude,
            in TransformationConfig config)
        {
            float chance = baseChance;
            
            // Stat contributions
            chance += (physique / 100f) * config.PhysiqueWeighting;
            chance += (will / 100f) * config.WillWeighting;
            
            // Trigger magnitude bonus
            chance += triggerMagnitude * 0.01f;
            
            return math.clamp(chance, 0f, 1f);
        }

        /// <summary>
        /// Rolls for transformation.
        /// </summary>
        public static bool RollTransformation(float chance, uint seed)
        {
            float roll = DeterministicRandom(seed) / (float)uint.MaxValue;
            return roll < chance;
        }

        /// <summary>
        /// Calculates delayed transformation completion tick.
        /// </summary>
        public static uint CalculateCompletionTick(
            uint currentTick,
            in TransformationConfig config,
            uint seed)
        {
            uint range = config.MaxDelayTicks - config.MinDelayTicks;
            uint delay = config.MinDelayTicks + (DeterministicRandom(seed) % range);
            return currentTick + delay;
        }

        /// <summary>
        /// Updates transformation progress.
        /// </summary>
        public static float UpdateProgress(
            float currentProgress,
            uint currentTick,
            uint startTick,
            uint completionTick)
        {
            if (completionTick <= startTick) return 1f;
            
            uint elapsed = currentTick - startTick;
            uint total = completionTick - startTick;
            
            return math.saturate((float)elapsed / total);
        }

        /// <summary>
        /// Creates retained identity from original entity.
        /// </summary>
        public static RetainedIdentity CreateRetainedIdentity(
            FixedString64Bytes originalName,
            uint originalId,
            Entity village,
            Entity family,
            byte alignment,
            Entity transformer,
            bool wasPositive)
        {
            return new RetainedIdentity
            {
                OriginalName = originalName,
                OriginalEntityId = originalId,
                OriginalVillage = village,
                OriginalFamily = family,
                OriginalAlignment = alignment,
                RelationToTransformer = wasPositive ? 50f : -50f
            };
        }

        /// <summary>
        /// Adds a retained memory.
        /// </summary>
        public static void AddMemory(
            ref DynamicBuffer<RetainedMemory> memories,
            FixedString64Bytes memoryType,
            Entity related,
            float intensity,
            bool isPositive)
        {
            if (memories.Length >= memories.Capacity)
            {
                // Remove weakest memory
                int weakest = 0;
                float weakestIntensity = float.MaxValue;
                for (int i = 0; i < memories.Length; i++)
                {
                    if (memories[i].Intensity < weakestIntensity)
                    {
                        weakestIntensity = memories[i].Intensity;
                        weakest = i;
                    }
                }
                memories.RemoveAt(weakest);
            }

            memories.Add(new RetainedMemory
            {
                MemoryType = memoryType,
                RelatedEntity = related,
                Intensity = intensity,
                IsPositive = (byte)(isPositive ? 1 : 0)
            });
        }

        /// <summary>
        /// Decays memories over time.
        /// </summary>
        public static void DecayMemories(
            ref DynamicBuffer<RetainedMemory> memories,
            float decayRate)
        {
            for (int i = memories.Length - 1; i >= 0; i--)
            {
                var memory = memories[i];
                memory.Intensity *= (1f - decayRate);
                
                if (memory.Intensity < 0.01f)
                {
                    memories.RemoveAt(i);
                }
                else
                {
                    memories[i] = memory;
                }
            }
        }

        /// <summary>
        /// Gets strongest memory for an entity.
        /// </summary>
        public static bool TryGetMemory(
            in DynamicBuffer<RetainedMemory> memories,
            Entity related,
            out RetainedMemory memory)
        {
            memory = default;
            float strongest = 0;
            bool found = false;

            for (int i = 0; i < memories.Length; i++)
            {
                if (memories[i].RelatedEntity == related && memories[i].Intensity > strongest)
                {
                    strongest = memories[i].Intensity;
                    memory = memories[i];
                    found = true;
                }
            }

            return found;
        }

        /// <summary>
        /// Checks if transformation is complete.
        /// </summary>
        public static bool IsTransformationComplete(in TransformationInProgress progress, uint currentTick)
        {
            return progress.Progress >= 1f || currentTick >= progress.CompletionTick;
        }

        /// <summary>
        /// Gets relation modifier based on transformation cause.
        /// </summary>
        public static float GetRelationModifier(in RetainedIdentity identity, Entity other)
        {
            // Strong negative relation to transformer if vengeful
            if (identity.RelationToTransformer < 0)
                return -0.5f;
            
            return 0f;
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

