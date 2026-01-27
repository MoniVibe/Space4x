#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using PureDOTS.Runtime.Knowledge;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Authoring.Knowledge
{
    /// <summary>
    /// Authoring ScriptableObject for lesson catalog.
    /// </summary>
    public class LessonCatalogAuthoring : MonoBehaviour
    {
        [Serializable]
        public class LessonDefinition
        {
            [Header("Identity")]
            public string lessonId;
            public string displayName;
            public string description;

            [Header("Configuration")]
            public LessonCategory category = LessonCategory.Crafting;
            [Min(0)]
            public float xpPerTier = 100f;
            [Range(0f, 1f)]
            public float teachingDifficulty = 0.5f;
            public bool canBeDiscovered = true;
            [Range(0, 10)]
            public int requiredEnlightenment = 0;

            [Header("Prerequisites")]
            public List<PrerequisiteDefinition> prerequisites = new();

            [Header("Effects")]
            public List<EffectDefinition> effects = new();
        }

        [Serializable]
        public class PrerequisiteDefinition
        {
            public LessonPrerequisiteType type = LessonPrerequisiteType.Lesson;
            public string targetId;
            [Range(0, 255)]
            public int requiredLevel = 0;
            public MasteryTier requiredTier = MasteryTier.Novice;
        }

        [Serializable]
        public class EffectDefinition
        {
            public MasteryTier requiredTier = MasteryTier.Novice;
            public LessonEffectType type = LessonEffectType.YieldMultiplier;
            public float value = 0.1f;
            public string targetId;
            public string context;
        }

        public List<LessonDefinition> lessons = new();
    }

    /// <summary>
    /// Baker for LessonCatalogAuthoring.
    /// </summary>
    public sealed class LessonCatalogBaker : Baker<LessonCatalogAuthoring>
    {
        public override void Bake(LessonCatalogAuthoring authoring)
        {
            using var bb = new BlobBuilder(Allocator.Temp);
            ref var root = ref bb.ConstructRoot<LessonDefinitionBlob>();

            var lessonArray = bb.Allocate(ref root.Lessons, authoring.lessons.Count);
            for (int i = 0; i < authoring.lessons.Count; i++)
            {
                var src = authoring.lessons[i];
                ref var entry = ref lessonArray[i];
                entry.LessonId = new FixedString64Bytes(src.lessonId);
                entry.DisplayName = new FixedString64Bytes(src.displayName);
                entry.Description = new FixedString128Bytes(src.description);
                entry.Category = src.category;
                entry.XpPerTier = math.max(1f, src.xpPerTier);
                entry.TeachingDifficulty = math.clamp(src.teachingDifficulty, 0f, 1f);
                entry.CanBeDiscovered = src.canBeDiscovered;
                entry.RequiredEnlightenment = (byte)math.clamp(src.requiredEnlightenment, 0, 10);

                // Bake prerequisites
                var prereqs = bb.Allocate(ref entry.Prerequisites, src.prerequisites.Count);
                for (int p = 0; p < src.prerequisites.Count; p++)
                {
                    prereqs[p] = new LessonPrerequisite
                    {
                        Type = src.prerequisites[p].type,
                        TargetId = new FixedString64Bytes(src.prerequisites[p].targetId),
                        RequiredLevel = (byte)math.clamp(src.prerequisites[p].requiredLevel, 0, 255),
                        RequiredTier = src.prerequisites[p].requiredTier
                    };
                }

                // Bake effects
                var effects = bb.Allocate(ref entry.Effects, src.effects.Count);
                for (int e = 0; e < src.effects.Count; e++)
                {
                    effects[e] = new LessonEffect
                    {
                        RequiredTier = src.effects[e].requiredTier,
                        Type = src.effects[e].type,
                        Value = src.effects[e].value,
                        TargetId = new FixedString64Bytes(src.effects[e].targetId),
                        Context = new FixedString64Bytes(src.effects[e].context ?? "")
                    };
                }

            }

            var blobAsset = bb.CreateBlobAssetReference<LessonDefinitionBlob>(Allocator.Persistent);
            var entity = GetEntity(TransformUsageFlags.None);
            AddBlobAsset(ref blobAsset, out _);
            AddComponent(entity, new LessonCatalogRef { Blob = blobAsset });
        }
    }
}
#endif

