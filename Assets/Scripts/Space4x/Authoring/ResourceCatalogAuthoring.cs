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
    [AddComponentMenu("Space4X/Resource Catalog")]
    public sealed class ResourceCatalogAuthoring : MonoBehaviour
    {
        [Serializable]
        public class ResourceSpecData
        {
            public string id;
            public ResourceType type;
            [Header("Prefab Metadata")]
            [Tooltip("Presentation archetype (e.g., 'raw-resource', 'mineral')")]
            public string presentationArchetype = string.Empty;
            [Header("Default Style Tokens")]
            [Range(0, 255)] public byte defaultPalette = 0;
            [Range(0, 255)] public byte defaultRoughness = 128;
            [Range(0, 255)] public byte defaultPattern = 0;
        }

        public List<ResourceSpecData> resources = new List<ResourceSpecData>();

        public sealed class Baker : Unity.Entities.Baker<ResourceCatalogAuthoring>
        {
            public override void Bake(ResourceCatalogAuthoring authoring)
            {
                if (authoring == null || authoring.resources == null || authoring.resources.Count == 0)
                {
                    Debug.LogWarning("ResourceCatalogAuthoring has no resources defined.");
                    return;
                }

                using var builder = new BlobBuilder(Allocator.Temp);
                ref var catalogBlob = ref builder.ConstructRoot<ResourceCatalogBlob>();
                var resourceArray = builder.Allocate(ref catalogBlob.Resources, authoring.resources.Count);

                for (int i = 0; i < authoring.resources.Count; i++)
                {
                    var resourceData = authoring.resources[i];
                    resourceArray[i] = new ResourceSpec
                    {
                        Id = new FixedString64Bytes(resourceData.id ?? string.Empty),
                        Type = resourceData.type,
                        PresentationArchetype = new FixedString64Bytes(resourceData.presentationArchetype ?? string.Empty),
                        DefaultStyleTokens = new StyleTokens
                        {
                            Palette = resourceData.defaultPalette,
                            Roughness = resourceData.defaultRoughness,
                            Pattern = resourceData.defaultPattern
                        }
                    };
                }

                var blobAsset = builder.CreateBlobAssetReference<ResourceCatalogBlob>(Allocator.Persistent);
                builder.Dispose();

                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new ResourceCatalogSingleton { Catalog = blobAsset });
            }
        }
    }
}

