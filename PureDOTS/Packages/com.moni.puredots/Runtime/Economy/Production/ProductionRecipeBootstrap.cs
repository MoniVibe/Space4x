using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Economy.Production
{
    /// <summary>
    /// Bootstraps the ProductionRecipe catalog singleton with default recipes.
    /// Creates a default catalog if none exists.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct ProductionRecipeBootstrapSystem : ISystem
    {
        private static BlobAssetReference<ProductionRecipeCatalogBlob> s_RecipeCatalogBlob;

        public void OnCreate(ref SystemState state)
        {
            EnsureCatalog(ref state);
            state.Enabled = false; // Only run once
        }

        public void OnUpdate(ref SystemState state)
        {
            // No-op after initial bootstrap
        }

        public void OnDestroy(ref SystemState state)
        {
            DisposeCatalog(ref state);
        }

        private static void EnsureCatalog(ref SystemState state)
        {
            Entity existingEntity = Entity.Null;
            var hasExistingEntity = false;
            using (var query = state.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<ProductionRecipeCatalog>()))
            {
                if (query.TryGetSingleton(out ProductionRecipeCatalog existing))
                {
                    existingEntity = query.GetSingletonEntity();
                    hasExistingEntity = true;
                    if (!s_RecipeCatalogBlob.IsCreated && existing.Catalog.IsCreated)
                    {
                        s_RecipeCatalogBlob = existing.Catalog;
                    }
                    if (s_RecipeCatalogBlob.IsCreated)
                    {
                        state.EntityManager.SetComponentData(existingEntity, new ProductionRecipeCatalog { Catalog = s_RecipeCatalogBlob });
                        return;
                    }
                }
            }

            if (s_RecipeCatalogBlob.IsCreated)
            {
                AssignCatalogToEntity(existingEntity, hasExistingEntity, ref state);
                return;
            }

            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<ProductionRecipeCatalogBlob>();

            // Create recipe data structures
            var recipeData = new NativeList<(ProductionRecipeBlob recipe, NativeList<RecipeInputBlob> inputs, NativeList<RecipeOutputBlob> outputs)>(16, Allocator.Temp);

            // Refining recipe: iron ore → iron ingots
            var ironOreInputs = new NativeList<RecipeInputBlob>(Allocator.Temp);
            ironOreInputs.Add(new RecipeInputBlob
            {
                ItemId = new FixedString64Bytes("iron_ore"),
                Quantity = 100f,
                MinPurity = 0f,
                MinQuality = 0f
            });

            var ironIngotOutputs = new NativeList<RecipeOutputBlob>(Allocator.Temp);
            ironIngotOutputs.Add(new RecipeOutputBlob
            {
                ItemId = new FixedString64Bytes("iron_ingot"),
                Quantity = 75f // Yield based on purity
            });

            recipeData.Add((new ProductionRecipeBlob
            {
                RecipeId = new FixedString64Bytes("iron_ore_to_ingot"),
                Stage = ProductionStage.Refining,
                RequiredBusinessType = BusinessType.Blacksmith,
                MinTechTier = 1,
                MinArtisanExpertise = 10,
                BaseTimeCost = 8.0f,
                LaborCost = 1.0f
            }, ironOreInputs, ironIngotOutputs));

            // Crafting recipe: iron ingots + wood → sword
            var swordInputs = new NativeList<RecipeInputBlob>(Allocator.Temp);
            swordInputs.Add(new RecipeInputBlob
            {
                ItemId = new FixedString64Bytes("iron_ingot"),
                Quantity = 5f,
                MinPurity = 0f,
                MinQuality = 0f
            });
            swordInputs.Add(new RecipeInputBlob
            {
                ItemId = new FixedString64Bytes("wood"),
                Quantity = 2f,
                MinPurity = 0f,
                MinQuality = 0f
            });

            var swordOutputs = new NativeList<RecipeOutputBlob>(Allocator.Temp);
            swordOutputs.Add(new RecipeOutputBlob
            {
                ItemId = new FixedString64Bytes("sword"),
                Quantity = 1f
            });

            recipeData.Add((new ProductionRecipeBlob
            {
                RecipeId = new FixedString64Bytes("sword_craft"),
                Stage = ProductionStage.Crafting,
                RequiredBusinessType = BusinessType.Blacksmith,
                MinTechTier = 1,
                MinArtisanExpertise = 20,
                BaseTimeCost = 12.0f,
                LaborCost = 1.0f
            }, swordInputs, swordOutputs));

            // Build blob arrays
            var recipesArray = builder.Allocate(ref root.Recipes, recipeData.Length);

            for (int i = 0; i < recipeData.Length; i++)
            {
                var (recipeTemplate, inputs, outputs) = recipeData[i];
                
                // Create recipe with allocated arrays
                ref var recipe = ref recipesArray[i];
                recipe.RecipeId = recipeTemplate.RecipeId;
                recipe.Stage = recipeTemplate.Stage;
                recipe.RequiredBusinessType = recipeTemplate.RequiredBusinessType;
                recipe.MinTechTier = recipeTemplate.MinTechTier;
                recipe.MinArtisanExpertise = recipeTemplate.MinArtisanExpertise;
                recipe.BaseTimeCost = recipeTemplate.BaseTimeCost;
                recipe.LaborCost = recipeTemplate.LaborCost;

                var inputsArray = builder.Allocate(ref recipe.Inputs, inputs.Length);
                for (int j = 0; j < inputs.Length; j++)
                {
                    inputsArray[j] = inputs[j];
                }

                var outputsArray = builder.Allocate(ref recipe.Outputs, outputs.Length);
                for (int j = 0; j < outputs.Length; j++)
                {
                    outputsArray[j] = outputs[j];
                }
            }

            // Dispose temp data
            for (int i = 0; i < recipeData.Length; i++)
            {
                recipeData[i].inputs.Dispose();
                recipeData[i].outputs.Dispose();
            }
            recipeData.Dispose();

            s_RecipeCatalogBlob = builder.CreateBlobAssetReference<ProductionRecipeCatalogBlob>(Allocator.Persistent);
            var catalogComponent = new ProductionRecipeCatalog { Catalog = s_RecipeCatalogBlob };

            if (hasExistingEntity && state.EntityManager.Exists(existingEntity))
            {
                state.EntityManager.SetComponentData(existingEntity, catalogComponent);
            }
            else
            {
                var entity = state.EntityManager.CreateEntity(typeof(ProductionRecipeCatalog));
                state.EntityManager.SetComponentData(entity, catalogComponent);
            }
        }

        private static void DisposeCatalog(ref SystemState state)
        {
            using var query = state.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<ProductionRecipeCatalog>());
            if (query.TryGetSingleton(out ProductionRecipeCatalog catalog))
            {
                var entity = query.GetSingletonEntity();
                catalog.Catalog = default;
                if (state.EntityManager.Exists(entity))
                {
                    state.EntityManager.SetComponentData(entity, catalog);
                }
            }

            if (s_RecipeCatalogBlob.IsCreated)
            {
                s_RecipeCatalogBlob.Dispose();
                s_RecipeCatalogBlob = default;
            }
        }

        private static void AssignCatalogToEntity(Entity existingEntity, bool hasExistingEntity, ref SystemState state)
        {
            if (!s_RecipeCatalogBlob.IsCreated)
            {
                return;
            }

            var catalogComponent = new ProductionRecipeCatalog { Catalog = s_RecipeCatalogBlob };
            if (hasExistingEntity && state.EntityManager.Exists(existingEntity))
            {
                state.EntityManager.SetComponentData(existingEntity, catalogComponent);
            }
            else
            {
                var entity = state.EntityManager.CreateEntity(typeof(ProductionRecipeCatalog));
                state.EntityManager.SetComponentData(entity, catalogComponent);
            }
        }
    }
}
