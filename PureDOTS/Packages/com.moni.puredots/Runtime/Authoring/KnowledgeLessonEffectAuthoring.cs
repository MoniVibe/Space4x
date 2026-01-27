#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using PureDOTS.Runtime.Knowledge;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Authoring
{
    [DisallowMultipleComponent]
    public sealed class KnowledgeLessonEffectAuthoring : MonoBehaviour
    {
        [Serializable]
        public struct HarvestLessonDefinition
        {
            public string lessonId;
            public string resourceTypeId;
            [Tooltip("Bit mask of tiers (1<<tier).")]
            public int tierMask;
            public float qualityCapBonus;
            public float flatQualityBonus;
            public float yieldMultiplier;
            public float harvestTimeMultiplier;
            public float resourceValueMultiplier;
            public float aggregateValueMultiplier;
        }

        [Serializable]
        public struct ProcessingLessonDefinition
        {
            public string lessonId;
            public string inputResourceTypeId;
            public string outputResourceTypeId;
            public int tierMask;
            public float yieldMultiplier;
            public float qualityBonus;
            public float processTimeMultiplier;
            public float resourceValueMultiplier;
            public float aggregateValueMultiplier;
        }

        [Serializable]
        public struct LessonMetadataDefinition
        {
            public string lessonId;
            public string axisId;
            public string oppositeLessonId;
            [Range(0, 255)]
            public int difficulty;
            public bool allowParallelOpposites;
        }

        public List<HarvestLessonDefinition> harvestLessons = new();
        public List<ProcessingLessonDefinition> processingLessons = new();
        public List<LessonMetadataDefinition> lessonMetadata = new();

        class Baker : Baker<KnowledgeLessonEffectAuthoring>
        {
            public override void Bake(KnowledgeLessonEffectAuthoring authoring)
            {
                // When the reference authoring lives on the same GameObject, it owns the catalog singleton.
                if (authoring.TryGetComponent<KnowledgeLessonCatalogReferenceAuthoring>(out _))
                {
                    return;
                }

                var entity = GetEntity(TransformUsageFlags.None);
                var blobRef = KnowledgeLessonCatalogBuilder.BuildCatalog(authoring);
                var catalog = new KnowledgeLessonEffectCatalog { Blob = blobRef };

                try
                {
                    AddComponent(entity, catalog);
                }
                catch (InvalidOperationException ex) when (ex.Message.IndexOf("duplicate component", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    SetComponent(entity, catalog);
                }
            }
        }
    }

    internal static class KnowledgeLessonCatalogBuilder
    {
        public static BlobAssetReference<KnowledgeLessonEffectBlob> BuildCatalog(KnowledgeLessonEffectAuthoring authoring)
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<KnowledgeLessonEffectBlob>();

            FillHarvestEffects(authoring, builder, ref root);
            FillProcessingEffects(authoring, builder, ref root);
            FillMetadata(authoring, builder, ref root);

            return builder.CreateBlobAssetReference<KnowledgeLessonEffectBlob>(Allocator.Persistent);
        }

        private static void FillHarvestEffects(
            KnowledgeLessonEffectAuthoring authoring,
            BlobBuilder builder,
            ref KnowledgeLessonEffectBlob root)
        {
            var harvestArray = builder.Allocate(ref root.HarvestEffects, math.max(authoring.harvestLessons.Count, 0));
            for (int i = 0; i < harvestArray.Length; i++)
            {
                var src = authoring.harvestLessons[i];
                harvestArray[i] = new HarvestLessonEffect
                {
                    LessonId = ToFixed(src.lessonId),
                    ResourceTypeId = ToFixed(src.resourceTypeId),
                    TierMask = (byte)math.clamp(src.tierMask, 0, 0xFF),
                    QualityCapBonus = src.qualityCapBonus,
                    FlatQualityBonus = src.flatQualityBonus,
                    YieldMultiplier = src.yieldMultiplier == 0f ? 1f : src.yieldMultiplier,
                    HarvestTimeMultiplier = src.harvestTimeMultiplier == 0f ? 1f : src.harvestTimeMultiplier,
                    ResourceValueMultiplier = src.resourceValueMultiplier == 0f ? 1f : src.resourceValueMultiplier,
                    AggregateValueMultiplier = src.aggregateValueMultiplier == 0f ? 1f : src.aggregateValueMultiplier
                };
            }
        }

        private static void FillProcessingEffects(
            KnowledgeLessonEffectAuthoring authoring,
            BlobBuilder builder,
            ref KnowledgeLessonEffectBlob root)
        {
            var processingArray = builder.Allocate(ref root.ProcessingEffects, math.max(authoring.processingLessons.Count, 0));
            for (int i = 0; i < processingArray.Length; i++)
            {
                var src = authoring.processingLessons[i];
                processingArray[i] = new ProcessingLessonEffect
                {
                    LessonId = ToFixed(src.lessonId),
                    InputResourceTypeId = ToFixed(src.inputResourceTypeId),
                    OutputResourceTypeId = ToFixed(src.outputResourceTypeId),
                    TierMask = (byte)math.clamp(src.tierMask, 0, 0xFF),
                    YieldMultiplier = src.yieldMultiplier == 0f ? 1f : src.yieldMultiplier,
                    QualityBonus = src.qualityBonus,
                    ProcessTimeMultiplier = src.processTimeMultiplier == 0f ? 1f : src.processTimeMultiplier,
                    ResourceValueMultiplier = src.resourceValueMultiplier == 0f ? 1f : src.resourceValueMultiplier,
                    AggregateValueMultiplier = src.aggregateValueMultiplier == 0f ? 1f : src.aggregateValueMultiplier
                };
            }
        }

        private static void FillMetadata(
            KnowledgeLessonEffectAuthoring authoring,
            BlobBuilder builder,
            ref KnowledgeLessonEffectBlob root)
        {
            var metadataArray = builder.Allocate(ref root.LessonMetadata, math.max(authoring.lessonMetadata.Count, 0));
            for (int i = 0; i < metadataArray.Length; i++)
            {
                var src = authoring.lessonMetadata[i];
                metadataArray[i] = new KnowledgeLessonMetadata
                {
                    LessonId = ToFixed(src.lessonId),
                    AxisId = ToFixed(src.axisId),
                    OppositeLessonId = ToFixed(src.oppositeLessonId),
                    Difficulty = (byte)math.clamp(src.difficulty, 0, 255),
                    Flags = src.allowParallelOpposites ? KnowledgeLessonFlags.AllowParallelOpposites : KnowledgeLessonFlags.None
                };
            }
        }

        public static FixedString64Bytes ToFixed(string value)
        {
            FixedString64Bytes fs = default;
            if (!string.IsNullOrWhiteSpace(value))
            {
                fs = new FixedString64Bytes(value.Trim());
            }

            return fs;
        }
    }
}
#endif
