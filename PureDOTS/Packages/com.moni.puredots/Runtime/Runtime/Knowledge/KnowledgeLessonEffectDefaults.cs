using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Knowledge
{
    public static class KnowledgeLessonEffectDefaults
    {
        public static BlobAssetReference<KnowledgeLessonEffectBlob> CreateDefaultCatalog()
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<KnowledgeLessonEffectBlob>();

            var harvestArray = builder.Allocate(ref root.HarvestEffects, 3);
            harvestArray[0] = new HarvestLessonEffect
            {
                LessonId = new FixedString64Bytes("lesson.harvest.iron_ore"),
                ResourceTypeId = new FixedString64Bytes("space4x.minerals"),
                TierMask = 0,
                QualityCapBonus = 25f,
                FlatQualityBonus = 10f,
                YieldMultiplier = 1.1f,
                HarvestTimeMultiplier = 0.9f,
                ResourceValueMultiplier = 1.05f,
                AggregateValueMultiplier = 1.05f
            };

            harvestArray[1] = new HarvestLessonEffect
            {
                LessonId = new FixedString64Bytes("lesson.harvest.ironoak"),
                ResourceTypeId = new FixedString64Bytes("resource.tree.ironoak"),
                TierMask = 0,
                QualityCapBonus = 50f,
                FlatQualityBonus = 30f,
                YieldMultiplier = 1.25f,
                HarvestTimeMultiplier = 0.8f,
                ResourceValueMultiplier = 1.15f,
                AggregateValueMultiplier = 1.1f
            };

            harvestArray[2] = new HarvestLessonEffect
            {
                LessonId = new FixedString64Bytes("lesson.harvest.general"),
                ResourceTypeId = default,
                TierMask = 0,
                QualityCapBonus = 10f,
                FlatQualityBonus = 5f,
                YieldMultiplier = 1.05f,
                HarvestTimeMultiplier = 0.95f,
                ResourceValueMultiplier = 1.02f,
                AggregateValueMultiplier = 1.02f
            };

            var processingArray = builder.Allocate(ref root.ProcessingEffects, 1);
            processingArray[0] = new ProcessingLessonEffect
            {
                LessonId = new FixedString64Bytes("lesson.processing.ore_refinery"),
                InputResourceTypeId = new FixedString64Bytes("space4x.minerals"),
                OutputResourceTypeId = new FixedString64Bytes("space4x.rare_metals"),
                TierMask = 0,
                YieldMultiplier = 1.2f,
                QualityBonus = 40f,
                ProcessTimeMultiplier = 0.85f,
                ResourceValueMultiplier = 1.15f,
                AggregateValueMultiplier = 1.15f
            };

            var metadataArray = builder.Allocate(ref root.LessonMetadata, 6);
            metadataArray[0] = new KnowledgeLessonMetadata
            {
                LessonId = new FixedString64Bytes("lesson.harvest.general"),
                AxisId = new FixedString64Bytes("axis.harvest.core"),
                OppositeLessonId = default,
                Difficulty = 10,
                Flags = KnowledgeLessonFlags.AllowParallelOpposites
            };

            metadataArray[1] = new KnowledgeLessonMetadata
            {
                LessonId = new FixedString64Bytes("lesson.harvest.iron_ore"),
                AxisId = new FixedString64Bytes("axis.harvest.metals"),
                OppositeLessonId = default,
                Difficulty = 35,
                Flags = KnowledgeLessonFlags.None
            };

            metadataArray[2] = new KnowledgeLessonMetadata
            {
                LessonId = new FixedString64Bytes("lesson.harvest.ironoak"),
                AxisId = new FixedString64Bytes("axis.harvest.ancient_woods"),
                OppositeLessonId = default,
                Difficulty = 60,
                Flags = KnowledgeLessonFlags.None
            };

            metadataArray[3] = new KnowledgeLessonMetadata
            {
                LessonId = new FixedString64Bytes("lesson.processing.ore_refinery"),
                AxisId = new FixedString64Bytes("axis.processing.metals"),
                OppositeLessonId = default,
                Difficulty = 45,
                Flags = KnowledgeLessonFlags.None
            };

            metadataArray[4] = new KnowledgeLessonMetadata
            {
                LessonId = new FixedString64Bytes("lesson.myth.dragons_caution"),
                AxisId = new FixedString64Bytes("axis.attitude.dragons"),
                OppositeLessonId = new FixedString64Bytes("lesson.myth.dragons_bravery"),
                Difficulty = 65,
                Flags = KnowledgeLessonFlags.None
            };

            metadataArray[5] = new KnowledgeLessonMetadata
            {
                LessonId = new FixedString64Bytes("lesson.myth.dragons_bravery"),
                AxisId = new FixedString64Bytes("axis.attitude.dragons"),
                OppositeLessonId = new FixedString64Bytes("lesson.myth.dragons_caution"),
                Difficulty = 75,
                Flags = KnowledgeLessonFlags.None
            };

            return builder.CreateBlobAssetReference<KnowledgeLessonEffectBlob>(Allocator.Persistent);
        }
    }
}
