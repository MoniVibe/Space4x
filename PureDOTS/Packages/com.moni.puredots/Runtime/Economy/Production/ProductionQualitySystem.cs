using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Economy.Resources;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Economy.Production
{
    /// <summary>
    /// Calculates quality/rarity/tech tier for production outputs.
    /// Uses formula configs to compute quality from inputs + artisan skill.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ProductionQualitySystem : ISystem
    {
        private ComponentLookup<BusinessProduction> _productionLookup;
        private ComponentLookup<BusinessInventory> _businessInventoryLookup;
        private BufferLookup<ProductionJob> _jobBufferLookup;
        private ComponentLookup<Inventory> _inventoryLookup;
        private BufferLookup<InventoryItem> _itemBufferLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TickTimeState>();
            state.RequireForUpdate<ProductionRecipeCatalog>();
            _productionLookup = state.GetComponentLookup<BusinessProduction>(false);
            _businessInventoryLookup = state.GetComponentLookup<BusinessInventory>(false);
            _jobBufferLookup = state.GetBufferLookup<ProductionJob>(false);
            _inventoryLookup = state.GetComponentLookup<Inventory>(false);
            _itemBufferLookup = state.GetBufferLookup<InventoryItem>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<ScenarioState>(out var scenario) ||
                !scenario.IsInitialized ||
                !scenario.EnableEconomy)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) ||
                rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            // Quality is calculated when jobs complete, not in a separate system
            // This system is a placeholder for future quality recalculation if needed
        }

        /// <summary>
        /// Calculates output quality for a refining recipe.
        /// </summary>
        [BurstCompile]
        public static float CalculateRefiningQuality(float inputPurity, float artisanExpertise, float businessQuality, int techTier, in RefiningFormulaConfigBlob config)
        {
            return (inputPurity * config.PurityCoefficient) +
                   (artisanExpertise * config.ExpertiseCoefficient) +
                   (businessQuality * config.BusinessQualityCoefficient) +
                   (techTier * config.TechTierCoefficient);
        }

        /// <summary>
        /// Calculates output quality for a crafting recipe.
        /// </summary>
        [BurstCompile]
        public static float CalculateCraftingQuality(float averageComponentQuality, float artisanBonus, float businessBonus, int techTier, in CraftingFormulaConfigBlob config)
        {
            return (averageComponentQuality * config.AverageComponentCoefficient) +
                   (artisanBonus * config.ArtisanBonusCoefficient) +
                   (businessBonus * config.BusinessBonusCoefficient) +
                   (techTier * config.TechTierBonusCoefficient);
        }
    }
}

