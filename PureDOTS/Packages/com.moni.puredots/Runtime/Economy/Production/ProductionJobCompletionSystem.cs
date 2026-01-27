using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Economy.Resources;
using PureDOTS.Runtime.Economy.Wealth;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Economy.Production
{
    /// <summary>
    /// Completes production jobs: consumes inputs, produces outputs, pays wages.
    /// Connects to Chunk 1 (wealth transactions) and Chunk 2 (inventory operations).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ProductionJobCompletionSystem : ISystem
    {
        private FixedString64Bytes _wageReason;
        private FixedString128Bytes _productionJobChannel;

        private ComponentLookup<BusinessProduction> _productionLookup;
        private ComponentLookup<BusinessInventory> _businessInventoryLookup;
        private ComponentLookup<BusinessBalance> _businessBalanceLookup;
        private ComponentLookup<VillagerWealth> _villagerWealthLookup;
        private BufferLookup<ProductionJob> _jobBufferLookup;
        private ComponentLookup<Inventory> _inventoryLookup;
        private BufferLookup<InventoryItem> _itemBufferLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TickTimeState>();
            state.RequireForUpdate<ProductionRecipeCatalog>();
            _productionLookup = state.GetComponentLookup<BusinessProduction>(false);
            _businessInventoryLookup = state.GetComponentLookup<BusinessInventory>(false);
            _businessBalanceLookup = state.GetComponentLookup<BusinessBalance>(false);
            _villagerWealthLookup = state.GetComponentLookup<VillagerWealth>(false);
            _jobBufferLookup = state.GetBufferLookup<ProductionJob>(false);
            _inventoryLookup = state.GetComponentLookup<Inventory>(false);
            _itemBufferLookup = state.GetBufferLookup<InventoryItem>(false);

            _wageReason = "wage";
            _productionJobChannel = "production_job";
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

            if (!SystemAPI.TryGetSingleton<ProductionRecipeCatalog>(out var catalog))
            {
                return;
            }

            ref var catalogBlob = ref catalog.Catalog.Value;

            _productionLookup.Update(ref state);
            _businessInventoryLookup.Update(ref state);
            _businessBalanceLookup.Update(ref state);
            _villagerWealthLookup.Update(ref state);
            _jobBufferLookup.Update(ref state);
            _inventoryLookup.Update(ref state);
            _itemBufferLookup.Update(ref state);

            var tickTimeState = SystemAPI.GetSingleton<TickTimeState>();
            var tick = tickTimeState.Tick;

            foreach (var (production, entity) in SystemAPI.Query<RefRW<BusinessProduction>>().WithEntityAccess())
            {
                if (!_jobBufferLookup.HasBuffer(entity))
                {
                    continue;
                }

                if (!_businessInventoryLookup.HasComponent(entity))
                {
                    continue;
                }

                var jobs = _jobBufferLookup[entity];
                var businessInventory = _businessInventoryLookup[entity];
                var inventoryEntity = businessInventory.InventoryEntity;

                if (!_inventoryLookup.HasComponent(inventoryEntity) || !_itemBufferLookup.HasBuffer(inventoryEntity))
                {
                    continue;
                }

                var items = _itemBufferLookup[inventoryEntity];

                // Process completed jobs
                for (int i = jobs.Length - 1; i >= 0; i--)
                {
                    var job = jobs[i];
                    if (job.Progress >= 1f || job.RemainingTime <= 0f)
                    {
                        // Complete job
                        if (TryFindRecipe(job.RecipeId, ref catalogBlob, out int recipeIndex))
                        {
                            CompleteJob(ref state, entity, job, ref catalogBlob, recipeIndex, items, inventoryEntity, tick);
                        }

                        jobs.RemoveAt(i);
                    }
                }
            }
        }

        [BurstCompile]
        private void CompleteJob(ref SystemState state, Entity businessEntity, ProductionJob job, ref ProductionRecipeCatalogBlob catalog, int recipeIndex, DynamicBuffer<InventoryItem> items, Entity inventoryEntity, uint tick)
        {
            ref var recipe = ref catalog.Recipes[recipeIndex];
            
            // Consume inputs
            for (int i = 0; i < recipe.Inputs.Length; i++)
            {
                ref var input = ref recipe.Inputs[i];
                RemoveItem(ref items, input.ItemId, input.Quantity);
            }

            // Produce outputs (quality calculation will be done by ProductionQualitySystem)
            for (int i = 0; i < recipe.Outputs.Length; i++)
            {
                ref var output = ref recipe.Outputs[i];
                AddItem(ref items, output.ItemId, output.Quantity, 50f, 1.0f, tick); // Default quality, will be updated by quality system
            }

            // Pay wages (Chunk 1)
            if (job.Worker != Entity.Null && _villagerWealthLookup.HasComponent(job.Worker))
            {
                if (_businessBalanceLookup.HasComponent(businessEntity))
                {
                    var businessBalance = _businessBalanceLookup[businessEntity];
                    float wage = recipe.BaseTimeCost * 10f; // Simple wage calculation

                    if (businessBalance.Cash >= wage)
                    {
                        WealthTransactionSystem.RecordTransaction(
                            ref state,
                            businessEntity,
                            job.Worker,
                            wage,
                            TransactionType.Income,
                            _wageReason,
                            _productionJobChannel
                        );
                    }
                }
            }
        }

        [BurstCompile]
        private static bool TryFindRecipe(in FixedString64Bytes recipeId, ref ProductionRecipeCatalogBlob catalog, out int recipeIndex)
        {
            for (int i = 0; i < catalog.Recipes.Length; i++)
            {
                ref var candidateRecipe = ref catalog.Recipes[i];
                if (candidateRecipe.RecipeId.Equals(recipeId))
                {
                    recipeIndex = i;
                    return true;
                }
            }

            recipeIndex = -1;
            return false;
        }

        [BurstCompile]
        private static void RemoveItem(ref DynamicBuffer<InventoryItem> items, in FixedString64Bytes itemId, float quantity)
        {
            float remaining = quantity;

            for (int i = items.Length - 1; i >= 0 && remaining > 0f; i--)
            {
                if (items[i].ItemId.Equals(itemId))
                {
                    var item = items[i];
                    float take = math.min(item.Quantity, remaining);
                    item.Quantity -= take;
                    remaining -= take;

                    if (item.Quantity <= 0f)
                    {
                        items.RemoveAt(i);
                    }
                    else
                    {
                        items[i] = item;
                    }
                }
            }
        }

        [BurstCompile]
        private static void AddItem(ref DynamicBuffer<InventoryItem> items, in FixedString64Bytes itemId, float quantity, float quality, float durability, uint tick)
        {
            // Try to merge with existing stack
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i].ItemId.Equals(itemId) && math.abs(items[i].Quality - quality) < 0.01f)
                {
                    var item = items[i];
                    item.Quantity += quantity;
                    items[i] = item;
                    return;
                }
            }

            // Create new stack
            items.Add(new InventoryItem
            {
                ItemId = itemId,
                Quantity = quantity,
                Quality = quality,
                Durability = durability,
                CreatedTick = tick
            });
        }
    }
}

