using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component for resource chain catalog.
    /// Creates a blob asset containing all resource definitions and recipes.
    /// </summary>
    public class ResourceChainCatalogAuthoring : MonoBehaviour
    {
        [Header("Catalog Settings")]
        [Tooltip("Whether to include all standard resources and recipes")]
        public bool includeStandardResources = true;

        [Tooltip("Whether to include advanced tier recipes")]
        public bool includeAdvancedRecipes = true;

        public class Baker : Baker<ResourceChainCatalogAuthoring>
        {
            public override void Bake(ResourceChainCatalogAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);

                // Build the blob asset
                var blobBuilder = new BlobBuilder(Allocator.Temp);
                ref var root = ref blobBuilder.ConstructRoot<ResourceChainCatalogBlob>();

                // Count resources and recipes
                int resourceCount = 0;
                int recipeCount = 0;

                if (authoring.includeStandardResources)
                {
                    resourceCount = 16; // All standard resources
                    recipeCount = authoring.includeAdvancedRecipes ? 11 : 8; // With or without advanced
                }

                // Allocate arrays
                var resources = blobBuilder.Allocate(ref root.Resources, resourceCount);
                var recipes = blobBuilder.Allocate(ref root.Recipes, recipeCount);

                if (authoring.includeStandardResources)
                {
                    // Raw resources
                    resources[0] = StandardResources.IronOre;
                    resources[1] = StandardResources.TitaniumOre;
                    resources[2] = StandardResources.Biomass;
                    resources[3] = StandardResources.HydrocarbonIce;
                    resources[4] = StandardResources.RareEarths;
                    resources[5] = StandardResources.Carbon;

                    // Refined resources
                    resources[6] = StandardResources.IronIngots;
                    resources[7] = StandardResources.TitaniumIngots;
                    resources[8] = StandardResources.Nutrients;
                    resources[9] = StandardResources.RefinedFuels;
                    resources[10] = StandardResources.Conductors;

                    // Composite resources
                    resources[11] = StandardResources.Steel;
                    resources[12] = StandardResources.Polymers;
                    resources[13] = StandardResources.Biopolymers;

                    // Advanced resources
                    resources[14] = StandardResources.Plasteel;
                    resources[15] = StandardResources.QuantumCores;

                    // Basic refining recipes
                    recipes[0] = StandardRecipes.RefineIron;
                    recipes[1] = StandardRecipes.RefineTitanium;
                    recipes[2] = StandardRecipes.ProcessBiomass;
                    recipes[3] = StandardRecipes.RefineHydrocarbons;
                    recipes[4] = StandardRecipes.ExtractConductors;

                    // Combination recipes
                    recipes[5] = StandardRecipes.SmeltSteel;
                    recipes[6] = StandardRecipes.SynthesizePolymers;
                    recipes[7] = StandardRecipes.CreateBiopolymers;

                    if (authoring.includeAdvancedRecipes)
                    {
                        // Advanced recipes
                        recipes[8] = StandardRecipes.ForgePlasteel;
                        recipes[9] = StandardRecipes.AssembleQuantumCores;
                        recipes[10] = StandardRecipes.CreateCompositeAlloys;
                    }
                }

                var blobReference = blobBuilder.CreateBlobAssetReference<ResourceChainCatalogBlob>(Allocator.Persistent);
                blobBuilder.Dispose();

                AddComponent(entity, new ResourceChainCatalog
                {
                    BlobReference = blobReference
                });

                // Add managed blob asset reference so Unity handles cleanup
                AddBlobAsset(ref blobReference, out _);
            }
        }
    }
}

