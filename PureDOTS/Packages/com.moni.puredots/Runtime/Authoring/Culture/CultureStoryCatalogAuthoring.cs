#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using PureDOTS.Runtime.Culture;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Authoring.Culture
{
    /// <summary>
    /// Authoring ScriptableObject for culture story catalog.
    /// </summary>
    public class CultureStoryCatalogAuthoring : MonoBehaviour
    {
        [Serializable]
        public class StoryDefinition
        {
            [Header("Identity")]
            public string storyId;
            public string displayName;
            public string originCultureId;

            [Header("Classification")]
            public StoryType type = StoryType.Legend;
            [Range(0, 255)] public int importanceRank = 50;
            [Range(0f, 1f)] public float transmissionDifficulty = 0.3f;
            [Range(0f, 0.1f)] public float decayRate = 0.01f;

            [Header("Prerequisites")]
            public List<string> prerequisiteStoryIds = new();

            [Header("Effects")]
            public List<StoryEffectDef> effects = new();

            [Header("Tags")]
            public List<string> tags = new();
        }

        [Serializable]
        public class StoryEffectDef
        {
            public StoryEffectType type = StoryEffectType.MoraleBonus;
            public float value = 0.1f;
            public string targetId;
        }

        public List<StoryDefinition> stories = new();
    }

    /// <summary>
    /// Baker for CultureStoryCatalogAuthoring.
    /// </summary>
    public sealed class CultureStoryCatalogBaker : Baker<CultureStoryCatalogAuthoring>
    {
        public override void Bake(CultureStoryCatalogAuthoring authoring)
        {
            using var bb = new BlobBuilder(Allocator.Temp);
            ref var root = ref bb.ConstructRoot<CultureStoryCatalogBlob>();

            var storyArray = bb.Allocate(ref root.Stories, authoring.stories.Count);
            for (int i = 0; i < authoring.stories.Count; i++)
            {
                var src = authoring.stories[i];
                ref var story = ref storyArray[i];
                story.StoryId = new FixedString64Bytes(src.storyId);
                story.DisplayName = new FixedString64Bytes(src.displayName);
                story.OriginCultureId = new FixedString64Bytes(src.originCultureId);
                story.Type = src.type;
                story.ImportanceRank = (byte)src.importanceRank;
                story.TransmissionDifficulty = src.transmissionDifficulty;
                story.DecayRate = src.decayRate;

                // Bake prerequisites
                var prereqs = bb.Allocate(ref story.PrerequisiteStories, src.prerequisiteStoryIds.Count);
                for (int p = 0; p < src.prerequisiteStoryIds.Count; p++)
                {
                    prereqs[p] = new FixedString64Bytes(src.prerequisiteStoryIds[p]);
                }

                // Bake effects
                var effects = bb.Allocate(ref story.Effects, src.effects.Count);
                for (int e = 0; e < src.effects.Count; e++)
                {
                    effects[e] = new StoryEffect
                    {
                        Type = src.effects[e].type,
                        Value = src.effects[e].value,
                        TargetId = new FixedString64Bytes(src.effects[e].targetId ?? "")
                    };
                }

                // Bake tags
                var tags = bb.Allocate(ref story.Tags, src.tags.Count);
                for (int t = 0; t < src.tags.Count; t++)
                {
                    tags[t] = new FixedString32Bytes(src.tags[t]);
                }

            }

            var blob = bb.CreateBlobAssetReference<CultureStoryCatalogBlob>(Allocator.Persistent);
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new CultureStoryCatalogRef { Blob = blob });
        }
    }
}
#endif

