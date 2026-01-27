using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using PureDOTS.Runtime.Resource;
using PureDOTS.Runtime.Components;

namespace PureDOTS.Runtime.Knowledge
{
    public struct KnowledgeLessonEffectCatalog : IComponentData
    {
        public BlobAssetReference<KnowledgeLessonEffectBlob> Blob;
    }

    public struct KnowledgeLessonEffectBlob
    {
        public BlobArray<HarvestLessonEffect> HarvestEffects;
        public BlobArray<ProcessingLessonEffect> ProcessingEffects;
        public BlobArray<KnowledgeLessonMetadata> LessonMetadata;
    }

    public struct HarvestLessonEffect
    {
        public FixedString64Bytes LessonId;
        public FixedString64Bytes ResourceTypeId;
        public byte TierMask;
        public float QualityCapBonus;
        public float FlatQualityBonus;
        public float YieldMultiplier;
        public float HarvestTimeMultiplier;
        public float ResourceValueMultiplier;
        public float AggregateValueMultiplier;
    }

    public struct ProcessingLessonEffect
    {
        public FixedString64Bytes LessonId;
        public FixedString64Bytes InputResourceTypeId;
        public FixedString64Bytes OutputResourceTypeId;
        public byte TierMask;
        public float YieldMultiplier;
        public float QualityBonus;
        public float ProcessTimeMultiplier;
        public float ResourceValueMultiplier;
        public float AggregateValueMultiplier;
    }

    public struct HarvestLessonModifiers
    {
        public float QualityCapBonus;
        public float FlatQualityBonus;
        public float YieldMultiplier;
        public float HarvestTimeMultiplier;
        public float ResourceValueMultiplier;
        public float AggregateValueMultiplier;

        public static HarvestLessonModifiers Identity => new HarvestLessonModifiers
        {
            QualityCapBonus = 0f,
            FlatQualityBonus = 0f,
            YieldMultiplier = 1f,
            HarvestTimeMultiplier = 1f,
            ResourceValueMultiplier = 1f,
            AggregateValueMultiplier = 1f
        };

        public void Accumulate(in HarvestLessonEffect effect)
        {
            QualityCapBonus += effect.QualityCapBonus;
            FlatQualityBonus += effect.FlatQualityBonus;
            YieldMultiplier *= math.max(0.001f, effect.YieldMultiplier == 0f ? 1f : effect.YieldMultiplier);
            HarvestTimeMultiplier *= math.max(0.001f, effect.HarvestTimeMultiplier == 0f ? 1f : effect.HarvestTimeMultiplier);
            ResourceValueMultiplier *= effect.ResourceValueMultiplier == 0f ? 1f : effect.ResourceValueMultiplier;
            AggregateValueMultiplier *= effect.AggregateValueMultiplier == 0f ? 1f : effect.AggregateValueMultiplier;
        }
    }

    public struct KnowledgeLessonMetadata
    {
        public FixedString64Bytes LessonId;
        public FixedString64Bytes AxisId;
        public FixedString64Bytes OppositeLessonId;
        public byte Difficulty;
        public KnowledgeLessonFlags Flags;
    }

    [Flags]
    public enum KnowledgeLessonFlags : byte
    {
        None = 0,
        AllowParallelOpposites = 1 << 0
    }

    public static class KnowledgeLessonEffectUtility
    {
        public static HarvestLessonModifiers EvaluateHarvestModifiers(
            ref KnowledgeLessonEffectBlob blob,
            in FixedList32Bytes<VillagerLessonProgress> lessons,
            in FixedString64Bytes resourceTypeId,
            ResourceQualityTier tier)
        {
            if (blob.HarvestEffects.Length == 0 || lessons.Length == 0)
            {
                return HarvestLessonModifiers.Identity;
            }

            var modifiers = HarvestLessonModifiers.Identity;

            for (var lessonIndex = 0; lessonIndex < lessons.Length; lessonIndex++)
            {
                var lesson = lessons[lessonIndex];
                if (lesson.Progress <= 0f || lesson.LessonId.Length == 0)
                {
                    continue;
                }

                for (var effectIndex = 0; effectIndex < blob.HarvestEffects.Length; effectIndex++)
                {
                    ref var effect = ref blob.HarvestEffects[effectIndex];
                    if (!effect.LessonId.Equals(lesson.LessonId))
                    {
                        continue;
                    }

                    if (effect.ResourceTypeId.Length > 0 && !effect.ResourceTypeId.Equals(resourceTypeId))
                    {
                        continue;
                    }

                    if (!TierMatches(effect.TierMask, tier))
                    {
                        continue;
                    }

                    modifiers.Accumulate(effect);
                }
            }

            return modifiers;
        }

        public static ushort EvaluateHarvestQuality(
            ushort baseQuality,
            ushort qualityVariance,
            ResourceQualityTier tierOverride,
            float skillLevel,
            in HarvestLessonModifiers modifiers,
            uint knowledgeFlags)
        {
            var desiredQuality = baseQuality == 0
                ? math.clamp(100 + qualityVariance, 1, 600)
                : baseQuality;

            var adjustedQuality = desiredQuality + modifiers.FlatQualityBonus;
            if (qualityVariance > 0)
            {
                adjustedQuality += qualityVariance * (skillLevel / 100f);
            }

            adjustedQuality = math.clamp(adjustedQuality, 1f, 600f);

            var targetTier = tierOverride != ResourceQualityTier.Unknown
                ? tierOverride
                : ResourceQualityUtility.DetermineTier((ushort)math.round(adjustedQuality));

            var hasLesson = TierLessonSatisfied(targetTier, knowledgeFlags);
            var lessonBonus = modifiers.QualityCapBonus;

            if ((knowledgeFlags & VillagerKnowledgeFlags.HarvestLegendary) != 0 && targetTier >= ResourceQualityTier.Legendary)
            {
                lessonBonus += 25f;
            }

            if ((knowledgeFlags & VillagerKnowledgeFlags.HarvestRelic) != 0 && targetTier >= ResourceQualityTier.Relic)
            {
                lessonBonus += 50f;
            }

            var roundedQuality = (ushort)math.round(adjustedQuality);
            return ResourceQualityUtility.ClampQualityWithSkill(roundedQuality, skillLevel, lessonBonus, hasLesson);
        }

        private static bool TierMatches(byte tierMask, ResourceQualityTier tier)
        {
            if (tierMask == 0)
            {
                return true;
            }

            var bit = 1 << (int)tier;
            return (tierMask & bit) != 0;
        }

        private static bool TierLessonSatisfied(ResourceQualityTier tier, uint knowledgeFlags)
        {
            return tier switch
            {
                ResourceQualityTier.Legendary => (knowledgeFlags & VillagerKnowledgeFlags.HarvestLegendary) != 0,
                ResourceQualityTier.Relic => (knowledgeFlags & VillagerKnowledgeFlags.HarvestRelic) != 0,
                _ => true
            };
        }
        public static bool TryGetLessonMetadata(
            ref KnowledgeLessonEffectBlob blob,
            in FixedString64Bytes lessonId,
            out KnowledgeLessonMetadata metadata)
        {
            if (lessonId.Length == 0 || blob.LessonMetadata.Length == 0)
            {
                metadata = default;
                return false;
            }

            for (var i = 0; i < blob.LessonMetadata.Length; i++)
            {
                ref var entry = ref blob.LessonMetadata[i];
                if (entry.LessonId.Equals(lessonId))
                {
                    metadata = entry;
                    return true;
                }
            }

            metadata = default;
            return false;
        }

    }
}
