using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component that identifies a product by its catalog ID.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Product ID")]
    public sealed class ProductIdAuthoring : MonoBehaviour
    {
        [Tooltip("Product ID from the catalog")]
        public string productId = string.Empty;

        [Header("Tech Gate")]
        [Range(0, 255)]
        [Tooltip("Required tech tier for this product")]
        public byte requiredTechTier = 0;

        private void OnValidate()
        {
            productId = string.IsNullOrWhiteSpace(productId) ? string.Empty : productId.Trim();
        }

        public sealed class Baker : Unity.Entities.Baker<ProductIdAuthoring>
        {
            public override void Bake(ProductIdAuthoring authoring)
            {
                if (string.IsNullOrWhiteSpace(authoring.productId))
                {
                    Debug.LogWarning($"ProductIdAuthoring on '{authoring.name}' has no productId set.");
                    return;
                }

                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Registry.ProductId
                {
                    Id = new FixedString64Bytes(authoring.productId)
                });

                if (authoring.requiredTechTier > 0)
                {
                    AddComponent(entity, new Registry.RequiredTechTier
                    {
                        Tier = authoring.requiredTechTier
                    });
                }
            }
        }
    }
}

