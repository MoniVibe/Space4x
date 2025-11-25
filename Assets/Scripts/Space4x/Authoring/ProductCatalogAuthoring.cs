using System;
using System.Collections.Generic;
using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Product Catalog")]
    public sealed class ProductCatalogAuthoring : MonoBehaviour
    {
        [Serializable]
        public class ProductSpecData
        {
            public string id;
            public string displayName;
            [Header("Tech Gate")]
            [Range(0, 255)] public byte requiredTechTier = 0;
            [Header("Prefab Metadata")]
            [Tooltip("Presentation archetype (e.g., 'product', 'component')")]
            public string presentationArchetype = string.Empty;
            [Header("Default Style Tokens")]
            [Range(0, 255)] public byte defaultPalette = 0;
            [Range(0, 255)] public byte defaultRoughness = 128;
            [Range(0, 255)] public byte defaultPattern = 0;
        }

        public List<ProductSpecData> products = new List<ProductSpecData>();

        public sealed class Baker : Unity.Entities.Baker<ProductCatalogAuthoring>
        {
            public override void Bake(ProductCatalogAuthoring authoring)
            {
                if (authoring == null || authoring.products == null || authoring.products.Count == 0)
                {
                    Debug.LogWarning("ProductCatalogAuthoring has no products defined.");
                    return;
                }

                using var builder = new BlobBuilder(Allocator.Temp);
                ref var catalogBlob = ref builder.ConstructRoot<ProductCatalogBlob>();
                var productArray = builder.Allocate(ref catalogBlob.Products, authoring.products.Count);

                for (int i = 0; i < authoring.products.Count; i++)
                {
                    var productData = authoring.products[i];
                    productArray[i] = new ProductSpec
                    {
                        Id = new FixedString64Bytes(productData.id ?? string.Empty),
                        DisplayName = new FixedString64Bytes(productData.displayName ?? string.Empty),
                        RequiredTechTier = productData.requiredTechTier,
                        PresentationArchetype = new FixedString64Bytes(productData.presentationArchetype ?? string.Empty),
                        DefaultStyleTokens = new StyleTokens
                        {
                            Palette = productData.defaultPalette,
                            Roughness = productData.defaultRoughness,
                            Pattern = productData.defaultPattern
                        }
                    };
                }

                var blobAsset = builder.CreateBlobAssetReference<ProductCatalogBlob>(Allocator.Persistent);
                builder.Dispose();

                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new ProductCatalogSingleton { Catalog = blobAsset });
            }
        }
    }
}

