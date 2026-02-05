using System;
using System.Collections.Generic;
using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityDebug = UnityEngine.Debug;

namespace Space4X.Authoring
{
    using Debug = UnityEngine.Debug;

    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Aggregate Catalog")]
    public sealed class AggregateCatalogAuthoring : MonoBehaviour
    {
        [Serializable]
        public class AggregateSpecData
        {
            public string id;
            
            [Header("Composition Mode")]
            [Tooltip("If true, use composed aggregate with profile references. If false, use simple byte tokens.")]
            public bool useComposedProfiles = false;
            
            [Header("Simple Tokens (used if useComposedProfiles = false)")]
            [Range(0, 255)] public byte alignment = 0;
            [Range(0, 255)] public byte outlook = 0;
            [Range(0, 255)] public byte policy = 0;
            
            [Header("Composed Profiles (used if useComposedProfiles = true)")]
            [Tooltip("Template ID (references AggregateTemplateCatalog)")]
            public string templateId = string.Empty;
            [Tooltip("Outlook ID (references OutlookProfileCatalog)")]
            public string outlookId = string.Empty;
            [Tooltip("Alignment ID (references AlignmentProfileCatalog)")]
            public string alignmentId = string.Empty;
            [Tooltip("Personality ID (references PersonalityArchetypeCatalog)")]
            public string personalityId = string.Empty;
            [Tooltip("Theme ID (references ThemeProfileCatalog)")]
            public string themeId = string.Empty;
            
            [Header("Aggregate Type")]
            [Tooltip("Type of aggregate (Dynasty, Guild, Corporation, Army, Band)")]
            public AffiliationType aggregateType = AffiliationType.Faction;
            
            [Header("Reputation/Prestige")]
            [Tooltip("Initial reputation score")]
            [Range(0f, 1f)]
            public float initialReputation = 0.5f;
            [Tooltip("Initial prestige score")]
            [Range(0f, 1f)]
            public float initialPrestige = 0f;
            
            [Header("Prefab Metadata")]
            [Tooltip("Presentation archetype (e.g., 'faction', 'guild', 'dynasty')")]
            public string presentationArchetype = string.Empty;
            [Header("Default Style Tokens")]
            [Range(0, 255)] public byte defaultPalette = 0;
            [Range(0, 255)] public byte defaultRoughness = 128;
            [Range(0, 255)] public byte defaultPattern = 0;
        }

        public List<AggregateSpecData> aggregates = new List<AggregateSpecData>();

        public sealed class Baker : Unity.Entities.Baker<AggregateCatalogAuthoring>
        {
            public override void Bake(AggregateCatalogAuthoring authoring)
            {
                if (authoring == null || authoring.aggregates == null || authoring.aggregates.Count == 0)
                {
                    UnityDebug.LogWarning("AggregateCatalogAuthoring has no aggregates defined.");
                    return;
                }

                using var builder = new BlobBuilder(Allocator.Temp);
                ref var catalogBlob = ref builder.ConstructRoot<AggregateCatalogBlob>();
                var aggregateArray = builder.Allocate(ref catalogBlob.Aggregates, authoring.aggregates.Count);

                for (int i = 0; i < authoring.aggregates.Count; i++)
                {
                    var aggregateData = authoring.aggregates[i];
                    aggregateArray[i] = new AggregateSpec
                    {
                        Id = new FixedString64Bytes(aggregateData.id ?? string.Empty),
                        Alignment = aggregateData.alignment,
                        Outlook = aggregateData.outlook,
                        Policy = aggregateData.policy,
                        PresentationArchetype = new FixedString64Bytes(aggregateData.presentationArchetype ?? string.Empty),
                        DefaultStyleTokens = new StyleTokens
                        {
                            Palette = aggregateData.defaultPalette,
                            Roughness = aggregateData.defaultRoughness,
                            Pattern = aggregateData.defaultPattern
                        }
                    };
                }

                var blobAsset = builder.CreateBlobAssetReference<AggregateCatalogBlob>(Allocator.Persistent);
                builder.Dispose();

                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new AggregateCatalogSingleton { Catalog = blobAsset });
            }
        }
    }
}


