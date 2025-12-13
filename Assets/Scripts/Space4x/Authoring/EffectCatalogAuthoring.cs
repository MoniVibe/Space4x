using System;
using System.Collections.Generic;
using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Authoring
{
    using Debug = UnityEngine.Debug;

    
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Effect Catalog")]
    public sealed class EffectCatalogAuthoring : MonoBehaviour
    {
        [Serializable]
        public class EffectSpecData
        {
            public string id;
            [Header("Prefab Metadata")]
            [Tooltip("Presentation archetype (e.g., 'explosion', 'shield', 'beam')")]
            public string presentationArchetype = string.Empty;
            [Header("Default Style Tokens")]
            [Range(0, 255)] public byte defaultPalette = 0;
            [Range(0, 255)] public byte defaultRoughness = 128;
            [Range(0, 255)] public byte defaultPattern = 0;
        }

        public List<EffectSpecData> effects = new List<EffectSpecData>();

        public sealed class Baker : Unity.Entities.Baker<EffectCatalogAuthoring>
        {
            public override void Bake(EffectCatalogAuthoring authoring)
            {
                if (authoring == null || authoring.effects == null || authoring.effects.Count == 0)
                {
                    Debug.LogWarning("EffectCatalogAuthoring has no effects defined.");
                    return;
                }

                using var builder = new BlobBuilder(Allocator.Temp);
                ref var catalogBlob = ref builder.ConstructRoot<EffectCatalogBlob>();
                var effectArray = builder.Allocate(ref catalogBlob.Effects, authoring.effects.Count);

                for (int i = 0; i < authoring.effects.Count; i++)
                {
                    var effectData = authoring.effects[i];
                    effectArray[i] = new EffectSpec
                    {
                        Id = new FixedString64Bytes(effectData.id ?? string.Empty),
                        PresentationArchetype = new FixedString64Bytes(effectData.presentationArchetype ?? string.Empty),
                        DefaultStyleTokens = new StyleTokens
                        {
                            Palette = effectData.defaultPalette,
                            Roughness = effectData.defaultRoughness,
                            Pattern = effectData.defaultPattern
                        }
                    };
                }

                var blobAsset = builder.CreateBlobAssetReference<EffectCatalogBlob>(Allocator.Persistent);
                builder.Dispose();

                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new EffectCatalogSingleton { Catalog = blobAsset });
            }
        }
    }
}

