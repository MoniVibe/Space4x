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
    [AddComponentMenu("Space4X/Recipe Catalog")]
    public sealed class RecipeCatalogAuthoring : MonoBehaviour
    {
        [Serializable]
        public class RecipeSpecData
        {
            public string id;
            public string outputProductId;
            public List<string> inputResourceIds = new List<string>();
            [Min(0f)]
            public float productionTimeSeconds = 10f;
            [Header("Tech Gate")]
            [Range(0, 255)] public byte requiredTechTier = 0;
            [Min(0f)]
            public float energyCostMW = 0f;
        }

        public List<RecipeSpecData> recipes = new List<RecipeSpecData>();

        public sealed class Baker : Unity.Entities.Baker<RecipeCatalogAuthoring>
        {
            public override void Bake(RecipeCatalogAuthoring authoring)
            {
                if (authoring == null || authoring.recipes == null || authoring.recipes.Count == 0)
                {
                    Debug.LogWarning("RecipeCatalogAuthoring has no recipes defined.");
                    return;
                }

                using var builder = new BlobBuilder(Allocator.Temp);
                ref var catalogBlob = ref builder.ConstructRoot<RecipeCatalogBlob>();
                var recipeArray = builder.Allocate(ref catalogBlob.Recipes, authoring.recipes.Count);

                for (int i = 0; i < authoring.recipes.Count; i++)
                {
                    var recipeData = authoring.recipes[i];
                    var inputCount = recipeData.inputResourceIds != null ? recipeData.inputResourceIds.Count : 0;
                    var inputArray = builder.Allocate(ref recipeArray[i].InputResourceIds, inputCount);

                    for (int j = 0; j < inputCount; j++)
                    {
                        inputArray[j] = new FixedString64Bytes(recipeData.inputResourceIds[j] ?? string.Empty);
                    }

                    recipeArray[i] = new RecipeSpec
                    {
                        Id = new FixedString64Bytes(recipeData.id ?? string.Empty),
                        OutputProductId = new FixedString64Bytes(recipeData.outputProductId ?? string.Empty),
                        ProductionTimeSeconds = math.max(0f, recipeData.productionTimeSeconds),
                        RequiredTechTier = recipeData.requiredTechTier,
                        EnergyCostMW = math.max(0f, recipeData.energyCostMW)
                    };
                }

                var blobAsset = builder.CreateBlobAssetReference<RecipeCatalogBlob>(Allocator.Persistent);
                builder.Dispose();

                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new RecipeCatalogSingleton { Catalog = blobAsset });
            }
        }
    }
}

