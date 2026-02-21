using PureDOTS.Runtime.Economy.Production;
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

            var recipeData = new NativeList<(ProductionRecipeBlob recipe, NativeList<RecipeInputBlob> inputs, NativeList<RecipeOutputBlob> outputs)>(36, Allocator.Temp);

            // Ore -> Ingot
            var oreInputs = new NativeList<RecipeInputBlob>(Allocator.Temp);
            oreInputs.Add(new RecipeInputBlob
            {
                ItemId = new FixedString64Bytes("space4x_ore"),
                Quantity = 100f,
                MinPurity = 0f,
                MinQuality = 0f
            });
            var ingotOutputs = new NativeList<RecipeOutputBlob>(Allocator.Temp);
            ingotOutputs.Add(new RecipeOutputBlob
            {
                ItemId = new FixedString64Bytes("space4x_ingot"),
                Quantity = 75f
            });
            recipeData.Add((new ProductionRecipeBlob
            {
                RecipeId = new FixedString64Bytes("space4x_ore_to_ingot"),
                Stage = ProductionStage.Refining,
                RequiredBusinessType = BusinessType.Alchemist,
                MinTechTier = 1,
                MinArtisanExpertise = 10,
                BaseTimeCost = 6.0f,
                LaborCost = 1.0f
            }, oreInputs, ingotOutputs));

            // Ore -> Ingot (Mk2)
            var oreMk2Inputs = new NativeList<RecipeInputBlob>(Allocator.Temp);
            oreMk2Inputs.Add(new RecipeInputBlob
            {
                ItemId = new FixedString64Bytes("space4x_ore"),
                Quantity = 120f,
                MinPurity = 0f,
                MinQuality = 0f
            });
            var ingotMk2Outputs = new NativeList<RecipeOutputBlob>(Allocator.Temp);
            ingotMk2Outputs.Add(new RecipeOutputBlob
            {
                ItemId = new FixedString64Bytes("space4x_ingot"),
                Quantity = 100f
            });
            recipeData.Add((new ProductionRecipeBlob
            {
                RecipeId = new FixedString64Bytes("space4x_ore_to_ingot_mk2"),
                Stage = ProductionStage.Refining,
                RequiredBusinessType = BusinessType.Alchemist,
                MinTechTier = 2,
                MinArtisanExpertise = 18,
                BaseTimeCost = 7.0f,
                LaborCost = 1.0f
            }, oreMk2Inputs, ingotMk2Outputs));

            // Ingot + Supplies -> Parts
            var partsInputs = new NativeList<RecipeInputBlob>(Allocator.Temp);
            partsInputs.Add(new RecipeInputBlob
            {
                ItemId = new FixedString64Bytes("space4x_ingot"),
                Quantity = 10f,
                MinPurity = 0f,
                MinQuality = 0f
            });
            partsInputs.Add(new RecipeInputBlob
            {
                ItemId = new FixedString64Bytes("space4x_supplies"),
                Quantity = 5f,
                MinPurity = 0f,
                MinQuality = 0f
            });
            var partsOutputs = new NativeList<RecipeOutputBlob>(Allocator.Temp);
            partsOutputs.Add(new RecipeOutputBlob
            {
                ItemId = new FixedString64Bytes("space4x_parts"),
                Quantity = 2f
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

            // Ingot + Supplies -> Parts (Mk2)
            var partsMk2Inputs = new NativeList<RecipeInputBlob>(Allocator.Temp);
            partsMk2Inputs.Add(new RecipeInputBlob
            {
                ItemId = new FixedString64Bytes("space4x_ingot"),
                Quantity = 14f,
                MinPurity = 0f,
                MinQuality = 0f
            });
            partsMk2Inputs.Add(new RecipeInputBlob
            {
                ItemId = new FixedString64Bytes("space4x_supplies"),
                Quantity = 6f,
                MinPurity = 0f,
                MinQuality = 0f
            });
            var partsMk2Outputs = new NativeList<RecipeOutputBlob>(Allocator.Temp);
            partsMk2Outputs.Add(new RecipeOutputBlob
            {
                ItemId = new FixedString64Bytes("space4x_parts"),
                Quantity = 3f
            });
            recipeData.Add((new ProductionRecipeBlob
            {
                RecipeId = new FixedString64Bytes("space4x_parts_assembly_mk2"),
                Stage = ProductionStage.Crafting,
                RequiredBusinessType = BusinessType.Builder,
                MinTechTier = 2,
                MinArtisanExpertise = 22,
                BaseTimeCost = 12.0f,
                LaborCost = 1.0f
            }, partsMk2Inputs, partsMk2Outputs));

            // Ore + Supplies -> Alloy
            var alloyInputs = new NativeList<RecipeInputBlob>(Allocator.Temp);
            alloyInputs.Add(new RecipeInputBlob
            {
                ItemId = new FixedString64Bytes("space4x_ore"),
                Quantity = 80f,
                MinPurity = 0f,
                MinQuality = 0f
            });
            alloyInputs.Add(new RecipeInputBlob
            {
                ItemId = new FixedString64Bytes("space4x_supplies"),
                Quantity = 10f,
                MinPurity = 0f,
                MinQuality = 0f
            });
            var alloyOutputs = new NativeList<RecipeOutputBlob>(Allocator.Temp);
            alloyOutputs.Add(new RecipeOutputBlob
            {
                ItemId = new FixedString64Bytes("space4x_alloy"),
                Quantity = 40f
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

            // Ore + Supplies -> Alloy (Mk2)
            var alloyMk2Inputs = new NativeList<RecipeInputBlob>(Allocator.Temp);
            alloyMk2Inputs.Add(new RecipeInputBlob
            {
                ItemId = new FixedString64Bytes("space4x_ore"),
                Quantity = 100f,
                MinPurity = 0f,
                MinQuality = 0f
            });
            alloyMk2Inputs.Add(new RecipeInputBlob
            {
                ItemId = new FixedString64Bytes("space4x_supplies"),
                Quantity = 12f,
                MinPurity = 0f,
                MinQuality = 0f
            });
            var alloyMk2Outputs = new NativeList<RecipeOutputBlob>(Allocator.Temp);
            alloyMk2Outputs.Add(new RecipeOutputBlob
            {
                ItemId = new FixedString64Bytes("space4x_alloy"),
                Quantity = 60f
            });
            recipeData.Add((new ProductionRecipeBlob
            {
                RecipeId = new FixedString64Bytes("space4x_ore_to_alloy_mk2"),
                Stage = ProductionStage.Refining,
                RequiredBusinessType = BusinessType.Alchemist,
                MinTechTier = 2,
                MinArtisanExpertise = 24,
                BaseTimeCost = 11.0f,
                LaborCost = 1.0f
            }, alloyMk2Inputs, alloyMk2Outputs));

            // Parts + Alloy -> Engine module
            var engineInputs = new NativeList<RecipeInputBlob>(Allocator.Temp);
            engineInputs.Add(new RecipeInputBlob
            {
                ItemId = new FixedString64Bytes("space4x_parts"),
                Quantity = 6f,
                MinPurity = 0f,
                MinQuality = 0f
            });
            engineInputs.Add(new RecipeInputBlob
            {
                ItemId = new FixedString64Bytes("space4x_alloy"),
                Quantity = 8f,
                MinPurity = 0f,
                MinQuality = 0f
            });
            var engineOutputs = new NativeList<RecipeOutputBlob>(Allocator.Temp);
            engineOutputs.Add(new RecipeOutputBlob
            {
                ItemId = new FixedString64Bytes("engine-mk1"),
                Quantity = 1f
            });
            recipeData.Add((new ProductionRecipeBlob
            {
                RecipeId = new FixedString64Bytes("space4x_module_engine_mk1"),
                Stage = ProductionStage.Crafting,
                RequiredBusinessType = BusinessType.Blacksmith,
                MinTechTier = 1,
                MinArtisanExpertise = 20,
                BaseTimeCost = 12.0f,
                LaborCost = 1.0f
            }, engineInputs, engineOutputs));

            // Parts + Alloy -> Engine module (Mk2)
            var engineMk2Inputs = new NativeList<RecipeInputBlob>(Allocator.Temp);
            engineMk2Inputs.Add(new RecipeInputBlob
            {
                ItemId = new FixedString64Bytes("space4x_parts"),
                Quantity = 10f,
                MinPurity = 0f,
                MinQuality = 0f
            });
            engineMk2Inputs.Add(new RecipeInputBlob
            {
                ItemId = new FixedString64Bytes("space4x_alloy"),
                Quantity = 14f,
                MinPurity = 0f,
                MinQuality = 0f
            });
            var engineMk2Outputs = new NativeList<RecipeOutputBlob>(Allocator.Temp);
            engineMk2Outputs.Add(new RecipeOutputBlob
            {
                ItemId = new FixedString64Bytes("engine-mk2"),
                Quantity = 1f
            });
            recipeData.Add((new ProductionRecipeBlob
            {
                RecipeId = new FixedString64Bytes("space4x_module_engine_mk2"),
                Stage = ProductionStage.Crafting,
                RequiredBusinessType = BusinessType.Blacksmith,
                MinTechTier = 2,
                MinArtisanExpertise = 26,
                BaseTimeCost = 16.0f,
                LaborCost = 1.0f
            }, engineMk2Inputs, engineMk2Outputs));

            // Parts + Alloy -> Shield module
            var shieldInputs = new NativeList<RecipeInputBlob>(Allocator.Temp);
            shieldInputs.Add(new RecipeInputBlob
            {
                ItemId = new FixedString64Bytes("space4x_parts"),
                Quantity = 5f,
                MinPurity = 0f,
                MinQuality = 0f
            });
            shieldInputs.Add(new RecipeInputBlob
            {
                ItemId = new FixedString64Bytes("space4x_alloy"),
                Quantity = 7f,
                MinPurity = 0f,
                MinQuality = 0f
            });
            var shieldOutputs = new NativeList<RecipeOutputBlob>(Allocator.Temp);
            shieldOutputs.Add(new RecipeOutputBlob
            {
                ItemId = new FixedString64Bytes("shield-s-1"),
                Quantity = 1f
            });
            recipeData.Add((new ProductionRecipeBlob
            {
                RecipeId = new FixedString64Bytes("space4x_module_shield_s_1"),
                Stage = ProductionStage.Crafting,
                RequiredBusinessType = BusinessType.Blacksmith,
                MinTechTier = 1,
                MinArtisanExpertise = 18,
                BaseTimeCost = 11.0f,
                LaborCost = 1.0f
            }, shieldInputs, shieldOutputs));

            // Parts + Alloy -> Shield module (M)
            var shieldMInputs = new NativeList<RecipeInputBlob>(Allocator.Temp);
            shieldMInputs.Add(new RecipeInputBlob
            {
                ItemId = new FixedString64Bytes("space4x_parts"),
                Quantity = 9f,
                MinPurity = 0f,
                MinQuality = 0f
            });
            shieldMInputs.Add(new RecipeInputBlob
            {
                ItemId = new FixedString64Bytes("space4x_alloy"),
                Quantity = 12f,
                MinPurity = 0f,
                MinQuality = 0f
            });
            var shieldMOutputs = new NativeList<RecipeOutputBlob>(Allocator.Temp);
            shieldMOutputs.Add(new RecipeOutputBlob
            {
                ItemId = new FixedString64Bytes("shield-m-1"),
                Quantity = 1f
            });
            recipeData.Add((new ProductionRecipeBlob
            {
                RecipeId = new FixedString64Bytes("space4x_module_shield_m_1"),
                Stage = ProductionStage.Crafting,
                RequiredBusinessType = BusinessType.Blacksmith,
                MinTechTier = 2,
                MinArtisanExpertise = 24,
                BaseTimeCost = 14.0f,
                LaborCost = 1.0f
            }, shieldMInputs, shieldMOutputs));

            // Parts + Alloy -> Laser module
            var laserInputs = new NativeList<RecipeInputBlob>(Allocator.Temp);
            laserInputs.Add(new RecipeInputBlob
            {
                ItemId = new FixedString64Bytes("space4x_parts"),
                Quantity = 4f,
                MinPurity = 0f,
                MinQuality = 0f
            });
            laserInputs.Add(new RecipeInputBlob
            {
                ItemId = new FixedString64Bytes("space4x_alloy"),
                Quantity = 6f,
                MinPurity = 0f,
                MinQuality = 0f
            });
            var laserOutputs = new NativeList<RecipeOutputBlob>(Allocator.Temp);
            laserOutputs.Add(new RecipeOutputBlob
            {
                ItemId = new FixedString64Bytes("laser-s-1"),
                Quantity = 1f
            });
            recipeData.Add((new ProductionRecipeBlob
            {
                RecipeId = new FixedString64Bytes("space4x_module_laser_s_1"),
                Stage = ProductionStage.Crafting,
                RequiredBusinessType = BusinessType.Blacksmith,
                MinTechTier = 1,
                MinArtisanExpertise = 16,
                BaseTimeCost = 9.0f,
                LaborCost = 1.0f
            }, laserInputs, laserOutputs));

            // Parts + Alloy -> Missile module (M)
            var missileMInputs = new NativeList<RecipeInputBlob>(Allocator.Temp);
            missileMInputs.Add(new RecipeInputBlob
            {
                ItemId = new FixedString64Bytes("space4x_parts"),
                Quantity = 9f,
                MinPurity = 0f,
                MinQuality = 0f
            });
            missileMInputs.Add(new RecipeInputBlob
            {
                ItemId = new FixedString64Bytes("space4x_alloy"),
                Quantity = 12f,
                MinPurity = 0f,
                MinQuality = 0f
            });
            var missileMOutputs = new NativeList<RecipeOutputBlob>(Allocator.Temp);
            missileMOutputs.Add(new RecipeOutputBlob
            {
                ItemId = new FixedString64Bytes("missile-m-1"),
                Quantity = 1f
            });
            recipeData.Add((new ProductionRecipeBlob
            {
                RecipeId = new FixedString64Bytes("space4x_module_missile_m_1"),
                Stage = ProductionStage.Crafting,
                RequiredBusinessType = BusinessType.Blacksmith,
                MinTechTier = 2,
                MinArtisanExpertise = 22,
                BaseTimeCost = 12.0f,
                LaborCost = 1.0f
            }, missileMInputs, missileMOutputs));

            // Parts + Alloy -> Bridge module
            var bridgeInputs = new NativeList<RecipeInputBlob>(Allocator.Temp);
            bridgeInputs.Add(new RecipeInputBlob
            {
                ItemId = new FixedString64Bytes("space4x_parts"),
                Quantity = 6f,
                MinPurity = 0f,
                MinQuality = 0f
            });
            bridgeInputs.Add(new RecipeInputBlob
            {
                ItemId = new FixedString64Bytes("space4x_alloy"),
                Quantity = 6f,
                MinPurity = 0f,
                MinQuality = 0f
            });
            var bridgeOutputs = new NativeList<RecipeOutputBlob>(Allocator.Temp);
            bridgeOutputs.Add(new RecipeOutputBlob
            {
                ItemId = new FixedString64Bytes("bridge-mk1"),
                Quantity = 1f
            });
            recipeData.Add((new ProductionRecipeBlob
            {
                RecipeId = new FixedString64Bytes("space4x_module_bridge_mk1"),
                Stage = ProductionStage.Crafting,
                RequiredBusinessType = BusinessType.Blacksmith,
                MinTechTier = 1,
                MinArtisanExpertise = 18,
                BaseTimeCost = 10.0f,
                LaborCost = 1.0f
            }, bridgeInputs, bridgeOutputs));

            // Parts + Alloy -> Cockpit module
            var cockpitInputs = new NativeList<RecipeInputBlob>(Allocator.Temp);
            cockpitInputs.Add(new RecipeInputBlob
            {
                ItemId = new FixedString64Bytes("space4x_parts"),
                Quantity = 5f,
                MinPurity = 0f,
                MinQuality = 0f
            });
            cockpitInputs.Add(new RecipeInputBlob
            {
                ItemId = new FixedString64Bytes("space4x_alloy"),
                Quantity = 5f,
                MinPurity = 0f,
                MinQuality = 0f
            });
            var cockpitOutputs = new NativeList<RecipeOutputBlob>(Allocator.Temp);
            cockpitOutputs.Add(new RecipeOutputBlob
            {
                ItemId = new FixedString64Bytes("cockpit-mk1"),
                Quantity = 1f
            });
            recipeData.Add((new ProductionRecipeBlob
            {
                RecipeId = new FixedString64Bytes("space4x_module_cockpit_mk1"),
                Stage = ProductionStage.Crafting,
                RequiredBusinessType = BusinessType.Blacksmith,
                MinTechTier = 1,
                MinArtisanExpertise = 16,
                BaseTimeCost = 9.0f,
                LaborCost = 1.0f
            }, cockpitInputs, cockpitOutputs));

            // Parts + Alloy -> Armor module
            var armorInputs = new NativeList<RecipeInputBlob>(Allocator.Temp);
            armorInputs.Add(new RecipeInputBlob
            {
                ItemId = new FixedString64Bytes("space4x_parts"),
                Quantity = 5f,
                MinPurity = 0f,
                MinQuality = 0f
            });
            armorInputs.Add(new RecipeInputBlob
            {
                ItemId = new FixedString64Bytes("space4x_alloy"),
                Quantity = 7f,
                MinPurity = 0f,
                MinQuality = 0f
            });
            var armorOutputs = new NativeList<RecipeOutputBlob>(Allocator.Temp);
            armorOutputs.Add(new RecipeOutputBlob
            {
                ItemId = new FixedString64Bytes("armor-s-1"),
                Quantity = 1f
            });
            recipeData.Add((new ProductionRecipeBlob
            {
                RecipeId = new FixedString64Bytes("space4x_module_armor_s_1"),
                Stage = ProductionStage.Crafting,
                RequiredBusinessType = BusinessType.Blacksmith,
                MinTechTier = 1,
                MinArtisanExpertise = 14,
                BaseTimeCost = 8.0f,
                LaborCost = 1.0f
            }, armorInputs, armorOutputs));

            // Parts + Alloy -> Ammo bay
            var ammoInputs = new NativeList<RecipeInputBlob>(Allocator.Temp);
            ammoInputs.Add(new RecipeInputBlob
            {
                ItemId = new FixedString64Bytes("space4x_parts"),
                Quantity = 4f,
                MinPurity = 0f,
                MinQuality = 0f
            });
            ammoInputs.Add(new RecipeInputBlob
            {
                ItemId = new FixedString64Bytes("space4x_alloy"),
                Quantity = 5f,
                MinPurity = 0f,
                MinQuality = 0f
            });
            var ammoOutputs = new NativeList<RecipeOutputBlob>(Allocator.Temp);
            ammoOutputs.Add(new RecipeOutputBlob
            {
                ItemId = new FixedString64Bytes("ammo-bay-s-1"),
                Quantity = 1f
            });
            recipeData.Add((new ProductionRecipeBlob
            {
                RecipeId = new FixedString64Bytes("space4x_module_ammo_bay_s_1"),
                Stage = ProductionStage.Crafting,
                RequiredBusinessType = BusinessType.Blacksmith,
                MinTechTier = 1,
                MinArtisanExpertise = 12,
                BaseTimeCost = 7.0f,
                LaborCost = 1.0f
            }, ammoInputs, ammoOutputs));

            // Parts + Alloy -> Reactor module (Mk2)
            var reactorInputs = new NativeList<RecipeInputBlob>(Allocator.Temp);
            reactorInputs.Add(new RecipeInputBlob
            {
                ItemId = new FixedString64Bytes("space4x_parts"),
                Quantity = 12f,
                MinPurity = 0f,
                MinQuality = 0f
            });
            reactorInputs.Add(new RecipeInputBlob
            {
                ItemId = new FixedString64Bytes("space4x_alloy"),
                Quantity = 16f,
                MinPurity = 0f,
                MinQuality = 0f
            });
            var reactorOutputs = new NativeList<RecipeOutputBlob>(Allocator.Temp);
            reactorOutputs.Add(new RecipeOutputBlob
            {
                ItemId = new FixedString64Bytes("reactor-mk2"),
                Quantity = 1f
            });
            recipeData.Add((new ProductionRecipeBlob
            {
                RecipeId = new FixedString64Bytes("space4x_module_reactor_mk2"),
                Stage = ProductionStage.Crafting,
                RequiredBusinessType = BusinessType.Blacksmith,
                MinTechTier = 2,
                MinArtisanExpertise = 28,
                BaseTimeCost = 18.0f,
                LaborCost = 1.0f
            }, reactorInputs, reactorOutputs));

            // Supplies -> Research packet
            var researchInputs = new NativeList<RecipeInputBlob>(Allocator.Temp);
            researchInputs.Add(new RecipeInputBlob
            {
                ItemId = new FixedString64Bytes("space4x_supplies"),
                Quantity = 12f,
                MinPurity = 0f,
                MinQuality = 0f
            });
            var researchOutputs = new NativeList<RecipeOutputBlob>(Allocator.Temp);
            researchOutputs.Add(new RecipeOutputBlob
            {
                ItemId = new FixedString64Bytes("space4x_research"),
                Quantity = 5f
            });
            recipeData.Add((new ProductionRecipeBlob
            {
                RecipeId = new FixedString64Bytes("space4x_research_packet"),
                Stage = ProductionStage.Crafting,
                RequiredBusinessType = BusinessType.Alchemist,
                MinTechTier = 1,
                MinArtisanExpertise = 10,
                BaseTimeCost = 8.0f,
                LaborCost = 1.0f
            }, researchInputs, researchOutputs));

            // Supplies -> Food
            var foodInputs = new NativeList<RecipeInputBlob>(Allocator.Temp);
            foodInputs.Add(new RecipeInputBlob
            {
                ItemId = new FixedString64Bytes("space4x_supplies"),
                Quantity = 8f,
                MinPurity = 0f,
                MinQuality = 0f
            });
            var foodOutputs = new NativeList<RecipeOutputBlob>(Allocator.Temp);
            foodOutputs.Add(new RecipeOutputBlob
            {
                ItemId = new FixedString64Bytes("space4x_food"),
                Quantity = 6f
            });
            recipeData.Add((new ProductionRecipeBlob
            {
                RecipeId = new FixedString64Bytes("space4x_supplies_to_food"),
                Stage = ProductionStage.Crafting,
                RequiredBusinessType = BusinessType.Builder,
                MinTechTier = 1,
                MinArtisanExpertise = 8,
                BaseTimeCost = 6.0f,
                LaborCost = 0.8f
            }, foodInputs, foodOutputs));

            // Supplies -> Water
            var waterInputs = new NativeList<RecipeInputBlob>(Allocator.Temp);
            waterInputs.Add(new RecipeInputBlob
            {
                ItemId = new FixedString64Bytes("space4x_supplies"),
                Quantity = 6f,
                MinPurity = 0f,
                MinQuality = 0f
            });
            var waterOutputs = new NativeList<RecipeOutputBlob>(Allocator.Temp);
            waterOutputs.Add(new RecipeOutputBlob
            {
                ItemId = new FixedString64Bytes("space4x_water"),
                Quantity = 6f
            });
            recipeData.Add((new ProductionRecipeBlob
            {
                RecipeId = new FixedString64Bytes("space4x_supplies_to_water"),
                Stage = ProductionStage.Crafting,
                RequiredBusinessType = BusinessType.Builder,
                MinTechTier = 1,
                MinArtisanExpertise = 8,
                BaseTimeCost = 5.0f,
                LaborCost = 0.8f
            }, waterInputs, waterOutputs));

            // Supplies -> Fuel
            var fuelInputs = new NativeList<RecipeInputBlob>(Allocator.Temp);
            fuelInputs.Add(new RecipeInputBlob
            {
                ItemId = new FixedString64Bytes("space4x_supplies"),
                Quantity = 12f,
                MinPurity = 0f,
                MinQuality = 0f
            });
            var fuelOutputs = new NativeList<RecipeOutputBlob>(Allocator.Temp);
            fuelOutputs.Add(new RecipeOutputBlob
            {
                ItemId = new FixedString64Bytes("space4x_fuel"),
                Quantity = 6f
            });
            recipeData.Add((new ProductionRecipeBlob
            {
                RecipeId = new FixedString64Bytes("space4x_supplies_to_fuel"),
                Stage = ProductionStage.Crafting,
                RequiredBusinessType = BusinessType.Builder,
                MinTechTier = 1,
                MinArtisanExpertise = 10,
                BaseTimeCost = 7.0f,
                LaborCost = 0.9f
            }, fuelInputs, fuelOutputs));

            // Supplies + Parts -> Trade goods
            var tradeInputs = new NativeList<RecipeInputBlob>(Allocator.Temp);
            tradeInputs.Add(new RecipeInputBlob
            {
                ItemId = new FixedString64Bytes("space4x_supplies"),
                Quantity = 10f,
                MinPurity = 0f,
                MinQuality = 0f
            });
            tradeInputs.Add(new RecipeInputBlob
            {
                ItemId = new FixedString64Bytes("space4x_parts"),
                Quantity = 2f,
                MinPurity = 0f,
                MinQuality = 0f
            });
            var tradeOutputs = new NativeList<RecipeOutputBlob>(Allocator.Temp);
            tradeOutputs.Add(new RecipeOutputBlob
            {
                ItemId = new FixedString64Bytes("space4x_trade_goods"),
                Quantity = 3f
            });
            recipeData.Add((new ProductionRecipeBlob
            {
                RecipeId = new FixedString64Bytes("space4x_trade_goods_assembly"),
                Stage = ProductionStage.Crafting,
                RequiredBusinessType = BusinessType.Builder,
                MinTechTier = 2,
                MinArtisanExpertise = 16,
                BaseTimeCost = 8.0f,
                LaborCost = 1.0f
            }, tradeInputs, tradeOutputs));

            // Ship hull build (shipyard placeholder: Builder)
            var shipInputs = new NativeList<RecipeInputBlob>(Allocator.Temp);
            shipInputs.Add(new RecipeInputBlob
            {
                ItemId = new FixedString64Bytes("space4x_parts"),
                Quantity = 30f,
                MinPurity = 0f,
                MinQuality = 0f
            });
            shipInputs.Add(new RecipeInputBlob
            {
                ItemId = new FixedString64Bytes("space4x_alloy"),
                Quantity = 40f,
                MinPurity = 0f,
                MinQuality = 0f
            });
            shipInputs.Add(new RecipeInputBlob
            {
                ItemId = new FixedString64Bytes("space4x_ingot"),
                Quantity = 20f,
                MinPurity = 0f,
                MinQuality = 0f
            });
            var shipOutputs = new NativeList<RecipeOutputBlob>(Allocator.Temp);
            shipOutputs.Add(new RecipeOutputBlob
            {
                ItemId = new FixedString64Bytes("lcv-sparrow"),
                Quantity = 1f
            });
            recipeData.Add((new ProductionRecipeBlob
            {
                RecipeId = new FixedString64Bytes("space4x_ship_lcv_sparrow"),
                Stage = ProductionStage.Crafting,
                RequiredBusinessType = BusinessType.Builder,
                MinTechTier = 1,
                MinArtisanExpertise = 25,
                BaseTimeCost = 28.0f,
                LaborCost = 1.0f
            }, shipInputs, shipOutputs));

            // Ship hull build (carrier)
            var muleInputs = new NativeList<RecipeInputBlob>(Allocator.Temp);
            muleInputs.Add(new RecipeInputBlob
            {
                ItemId = new FixedString64Bytes("space4x_parts"),
                Quantity = 60f,
                MinPurity = 0f,
                MinQuality = 0f
            });
            muleInputs.Add(new RecipeInputBlob
            {
                ItemId = new FixedString64Bytes("space4x_alloy"),
                Quantity = 80f,
                MinPurity = 0f,
                MinQuality = 0f
            });
            muleInputs.Add(new RecipeInputBlob
            {
                ItemId = new FixedString64Bytes("space4x_ingot"),
                Quantity = 40f,
                MinPurity = 0f,
                MinQuality = 0f
            });
            var muleOutputs = new NativeList<RecipeOutputBlob>(Allocator.Temp);
            muleOutputs.Add(new RecipeOutputBlob
            {
                ItemId = new FixedString64Bytes("cv-mule"),
                Quantity = 1f
            });
            recipeData.Add((new ProductionRecipeBlob
            {
                RecipeId = new FixedString64Bytes("space4x_ship_cv_mule"),
                Stage = ProductionStage.Crafting,
                RequiredBusinessType = BusinessType.Builder,
                MinTechTier = 2,
                MinArtisanExpertise = 35,
                BaseTimeCost = 45.0f,
                LaborCost = 1.0f
            }, muleInputs, muleOutputs));

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
