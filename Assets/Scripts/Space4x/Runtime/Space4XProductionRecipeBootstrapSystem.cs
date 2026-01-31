using PureDOTS.Runtime.Economy.Production;
using PureDOTS.Runtime.Economy.Resources;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using System;

namespace Space4X.Runtime
{
    /// <summary>
    /// Space4x-specific production recipe catalog (game-specific fork).
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(ProductionRecipeBootstrapSystem))]
    public partial struct Space4XProductionRecipeBootstrapSystem : ISystem
    {
        private static BlobAssetReference<ProductionRecipeCatalogBlob> s_CatalogBlob;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            EnsureCatalog(ref state);
            state.Enabled = false;
        }

        public void OnUpdate(ref SystemState state) { }

        public void OnDestroy(ref SystemState state)
        {
            DisposeCatalog(ref state);
        }

        private static void EnsureCatalog(ref SystemState state)
        {
            if (s_CatalogBlob.IsCreated)
            {
                AssignCatalog(ref state);
                return;
            }

            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<ProductionRecipeCatalogBlob>();

            var recipeData = new NativeList<(ProductionRecipeBlob recipe, NativeList<RecipeInputBlob> inputs, NativeList<RecipeOutputBlob> outputs)>(8, Allocator.Temp);

            // Ore -> Ingot
            var oreInputs = new NativeList<RecipeInputBlob>(Allocator.Temp);
            oreInputs.Add(new RecipeInputBlob
            {
                ItemId = new FixedString64Bytes("space4x_ore"),
                Quantity = 100f,
                MinPurity = 0f,
                MinQuality = 0f,
                RequiredTags = ItemTags.None,
                UseTagMatch = 0
            });
            var ingotOutputs = new NativeList<RecipeOutputBlob>(Allocator.Temp);
            ingotOutputs.Add(new RecipeOutputBlob
            {
                ItemId = new FixedString64Bytes("space4x_ingot"),
                Quantity = 75f,
                OutputTags = ItemTags.None,
                UseTagOutput = 0
            });
            recipeData.Add((new ProductionRecipeBlob
            {
                RecipeId = new FixedString64Bytes("space4x_ore_to_ingot"),
                Stage = ProductionStage.Refining,
                RequiredBusinessType = BusinessType.Blacksmith,
                MinTechTier = 1,
                MinArtisanExpertise = 10,
                BaseTimeCost = 6.0f,
                LaborCost = 1.0f
            }, oreInputs, ingotOutputs));

            // Ingot + Supplies -> Parts
            var partsInputs = new NativeList<RecipeInputBlob>(Allocator.Temp);
            partsInputs.Add(new RecipeInputBlob
            {
                ItemId = new FixedString64Bytes("space4x_ingot"),
                Quantity = 10f,
                MinPurity = 0f,
                MinQuality = 0f,
                RequiredTags = ItemTags.None,
                UseTagMatch = 0
            });
            partsInputs.Add(new RecipeInputBlob
            {
                ItemId = new FixedString64Bytes("space4x_supplies"),
                Quantity = 5f,
                MinPurity = 0f,
                MinQuality = 0f,
                RequiredTags = ItemTags.None,
                UseTagMatch = 0
            });
            var partsOutputs = new NativeList<RecipeOutputBlob>(Allocator.Temp);
            partsOutputs.Add(new RecipeOutputBlob
            {
                ItemId = new FixedString64Bytes("space4x_parts"),
                Quantity = 2f,
                OutputTags = ItemTags.None,
                UseTagOutput = 0
            });
            recipeData.Add((new ProductionRecipeBlob
            {
                RecipeId = new FixedString64Bytes("space4x_parts_assembly"),
                Stage = ProductionStage.Crafting,
                RequiredBusinessType = BusinessType.Builder,
                MinTechTier = 1,
                MinArtisanExpertise = 15,
                BaseTimeCost = 10.0f,
                LaborCost = 1.0f
            }, partsInputs, partsOutputs));

            // Ore + Fuel -> Alloy
            var alloyInputs = new NativeList<RecipeInputBlob>(Allocator.Temp);
            alloyInputs.Add(new RecipeInputBlob
            {
                ItemId = new FixedString64Bytes("space4x_ore"),
                Quantity = 80f,
                MinPurity = 0f,
                MinQuality = 0f,
                RequiredTags = ItemTags.None,
                UseTagMatch = 0
            });
            alloyInputs.Add(new RecipeInputBlob
            {
                ItemId = default,
                Quantity = 10f,
                MinPurity = 0f,
                MinQuality = 0f,
                RequiredTags = ItemTags.Fuel,
                UseTagMatch = 1
            });
            var alloyOutputs = new NativeList<RecipeOutputBlob>(Allocator.Temp);
            alloyOutputs.Add(new RecipeOutputBlob
            {
                ItemId = new FixedString64Bytes("space4x_alloy"),
                Quantity = 40f,
                OutputTags = ItemTags.None,
                UseTagOutput = 0
            });
            recipeData.Add((new ProductionRecipeBlob
            {
                RecipeId = new FixedString64Bytes("space4x_ore_to_alloy"),
                Stage = ProductionStage.Refining,
                RequiredBusinessType = BusinessType.Alchemist,
                MinTechTier = 1,
                MinArtisanExpertise = 20,
                BaseTimeCost = 9.0f,
                LaborCost = 1.0f
            }, alloyInputs, alloyOutputs));

            var recipesArray = builder.Allocate(ref root.Recipes, recipeData.Length);
            for (int i = 0; i < recipeData.Length; i++)
            {
                var (recipeTemplate, inputs, outputs) = recipeData[i];
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

            for (int i = 0; i < recipeData.Length; i++)
            {
                recipeData[i].inputs.Dispose();
                recipeData[i].outputs.Dispose();
            }
            recipeData.Dispose();

            s_CatalogBlob = builder.CreateBlobAssetReference<ProductionRecipeCatalogBlob>(Allocator.Persistent);
            AssignCatalog(ref state);
        }

        private static void AssignCatalog(ref SystemState state)
        {
            if (!s_CatalogBlob.IsCreated)
            {
                return;
            }

            var catalogComponent = new ProductionRecipeCatalog { Catalog = s_CatalogBlob };
            using var query = state.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<ProductionRecipeCatalog>());
            if (query.TryGetSingletonEntity<ProductionRecipeCatalog>(out var entity))
            {
                state.EntityManager.SetComponentData(entity, catalogComponent);
            }
            else
            {
                var newEntity = state.EntityManager.CreateEntity(typeof(ProductionRecipeCatalog));
                state.EntityManager.SetComponentData(newEntity, catalogComponent);
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

            if (s_CatalogBlob.IsCreated)
            {
                try
                {
                    s_CatalogBlob.Dispose();
                }
                catch (InvalidOperationException)
                {
                    // Domain unload can invalidate blob references; safe to swallow on shutdown.
                }
                s_CatalogBlob = default;
            }
        }
    }
}
