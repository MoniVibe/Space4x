using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Resource;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Executes resource processing recipes for facilities that hold inputs in
    /// their storehouse inventory and produce refined or composite outputs.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ResourceSystemGroup))]
    [UpdateAfter(typeof(ResourceRegistrySystem))]
    public partial struct ResourceProcessingSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TickTimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<ResourceRecipeSet>();
            state.RequireForUpdate<ResourceTypeIndex>();
            state.RequireForUpdate<ResourceProcessorConfig>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<TickTimeState>(out var tickTime)
                || tickTime.IsPaused
                || !tickTime.IsPlaying
                || !SystemAPI.TryGetSingleton<RewindState>(out var rewindState)
                || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var recipeSetComponent = SystemAPI.GetSingleton<ResourceRecipeSet>();
            if (!recipeSetComponent.Value.IsCreated)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<ResourceTypeIndex>(out var resourceTypeIndex)
                || !resourceTypeIndex.Catalog.IsCreated)
            {
                return;
            }

            ref var recipeSet = ref recipeSetComponent.Value.Value;
            var deltaTime = math.max(0f, tickTime.FixedDeltaTime);

            foreach (var (configRO, processorStateRW, inventoryRW, inventoryItems, capacities, reservations, queue)
                     in SystemAPI.Query<RefRO<ResourceProcessorConfig>, RefRW<ResourceProcessorState>, RefRW<StorehouseInventory>,
                         DynamicBuffer<StorehouseInventoryItem>, DynamicBuffer<StorehouseCapacityElement>,
                         DynamicBuffer<StorehouseReservationItem>, DynamicBuffer<ResourceProcessorQueue>>())
            {
                ref var processorState = ref processorStateRW.ValueRW;
                ref var inventory = ref inventoryRW.ValueRW;

                UpdateInProgressRecipe(ref processorState, ref inventory, inventoryItems, capacities, reservations, resourceTypeIndex.Catalog, deltaTime);

                if (processorState.RecipeId.Length != 0)
                {
                    continue;
                }

                if (TryStartQueuedRecipe(configRO.ValueRO.FacilityTag, ref processorState, ref inventory, inventoryItems, capacities, reservations, queue, resourceTypeIndex.Catalog, ref recipeSet))
                {
                    continue;
                }

                if (configRO.ValueRO.AutoRun != 0)
                {
                    TryStartAutoRecipe(configRO.ValueRO.FacilityTag, ref processorState, ref inventory, inventoryItems, capacities, reservations, resourceTypeIndex.Catalog, ref recipeSet);
                }
            }

            static void UpdateInProgressRecipe(ref ResourceProcessorState processorState, ref StorehouseInventory inventory,
                DynamicBuffer<StorehouseInventoryItem> items, DynamicBuffer<StorehouseCapacityElement> capacities,
                DynamicBuffer<StorehouseReservationItem> reservations, BlobAssetReference<ResourceTypeIndexBlob> catalog, float deltaTime)
            {
                if (processorState.RecipeId.Length == 0)
                {
                    return;
                }

                if (processorState.RemainingSeconds > 0f)
                {
                    processorState.RemainingSeconds = math.max(0f, processorState.RemainingSeconds - deltaTime);
                }

                if (processorState.RemainingSeconds <= 0f)
                {
                    if (TryProduceOutput(ref processorState, ref inventory, items, capacities, reservations, catalog))
                    {
                        processorState = default;
                    }
                }
            }

            static bool TryStartQueuedRecipe(FixedString32Bytes facilityTag, ref ResourceProcessorState processorState,
                ref StorehouseInventory inventory, DynamicBuffer<StorehouseInventoryItem> items,
                DynamicBuffer<StorehouseCapacityElement> capacities, DynamicBuffer<StorehouseReservationItem> reservations,
                DynamicBuffer<ResourceProcessorQueue> queue, BlobAssetReference<ResourceTypeIndexBlob> catalog,
                ref ResourceRecipeSetBlob recipeSet)
            {
                for (int i = 0; i < queue.Length; i++)
                {
                    var entry = queue[i];
                    if (!TryGetRecipeById(entry.RecipeId, ref recipeSet, out var recipe))
                    {
                        queue.RemoveAt(i);
                        i--;
                        continue;
                    }

                    if (!FacilityMatches(facilityTag, recipe.FacilityTag))
                    {
                        continue;
                    }

                    if (TryStartRecipe(in recipe, ref processorState, ref inventory, items, capacities, reservations, catalog))
                    {
                        if (entry.Repeat > 1)
                        {
                            entry.Repeat -= 1;
                            queue[i] = entry;
                        }
                        else
                        {
                            queue.RemoveAt(i);
                        }

                        return true;
                    }
                }

                return false;
            }

            static void TryStartAutoRecipe(FixedString32Bytes facilityTag, ref ResourceProcessorState processorState,
                ref StorehouseInventory inventory, DynamicBuffer<StorehouseInventoryItem> items,
                DynamicBuffer<StorehouseCapacityElement> capacities, DynamicBuffer<StorehouseReservationItem> reservations,
                BlobAssetReference<ResourceTypeIndexBlob> catalog, ref ResourceRecipeSetBlob recipeSet)
            {
                for (int i = 0; i < recipeSet.Recipes.Length; i++)
                {
                    ref var recipe = ref recipeSet.Recipes[i];
                    if (!FacilityMatches(facilityTag, recipe.FacilityTag))
                    {
                        continue;
                    }

                    if (TryStartRecipe(in recipe, ref processorState, ref inventory, items, capacities, reservations, catalog))
                    {
                        break;
                    }
                }
            }

            static bool TryGetRecipeById(FixedString64Bytes recipeId, ref ResourceRecipeSetBlob recipeSet, out ResourceRecipeBlob recipe)
            {
                for (int i = 0; i < recipeSet.Recipes.Length; i++)
                {
                    ref var candidate = ref recipeSet.Recipes[i];
                    if (candidate.Id.Equals(recipeId))
                    {
                        recipe = candidate;
                        return true;
                    }
                }

                recipe = default;
                return false;
            }

            static bool FacilityMatches(FixedString32Bytes processorTag, FixedString32Bytes recipeTag)
            {
                if (processorTag.Length == 0)
                {
                    return true;
                }

                if (recipeTag.Length == 0)
                {
                    return true;
                }

                return processorTag.Equals(recipeTag);
            }

            static bool TryStartRecipe(in ResourceRecipeBlob recipe, ref ResourceProcessorState processorState,
                ref StorehouseInventory inventory, DynamicBuffer<StorehouseInventoryItem> items,
                DynamicBuffer<StorehouseCapacityElement> capacities, DynamicBuffer<StorehouseReservationItem> reservations,
                BlobAssetReference<ResourceTypeIndexBlob> catalog)
            {
                var outputIndex = ResolveResourceTypeIndex(recipe.OutputResourceId, catalog);
                if (outputIndex == ushort.MaxValue)
                {
                    return false;
                }

                var outputAmount = math.max(1f, recipe.OutputAmount);
                if (!StorehouseMutationService.HasCapacityForDeposit(
                        outputIndex,
                        outputAmount,
                        catalog,
                        items,
                        capacities,
                        reservations))
                {
                    return false;
                }

                if (!CanFulfill(recipe, items))
                {
                    return false;
                }

                if (!ConsumeIngredients(in recipe, ref inventory, items, catalog))
                {
                    return false;
                }

                processorState.RecipeId = recipe.Id;
                processorState.OutputResourceId = recipe.OutputResourceId;
                processorState.Kind = recipe.Kind;
                processorState.OutputAmount = math.max(1, recipe.OutputAmount);
                processorState.RemainingSeconds = math.max(0f, recipe.ProcessSeconds);

                if (processorState.RemainingSeconds <= 0f)
                {
                    if (TryProduceOutput(ref processorState, ref inventory, items, capacities, reservations, catalog))
                    {
                        processorState = default;
                    }
                }

                return true;
            }

            static bool CanFulfill(in ResourceRecipeBlob recipe, DynamicBuffer<StorehouseInventoryItem> items)
            {
                for (int ingredientIndex = 0; ingredientIndex < recipe.Ingredients.Length; ingredientIndex++)
                {
                    var ingredient = recipe.Ingredients[ingredientIndex];
                    var available = 0f;

                    for (int itemIndex = 0; itemIndex < items.Length; itemIndex++)
                    {
                        if (items[itemIndex].ResourceTypeId.Equals(ingredient.ResourceId))
                        {
                            available = math.max(0f, items[itemIndex].Amount - items[itemIndex].Reserved);
                            break;
                        }
                    }

                    if (available + 1e-3f < ingredient.Amount)
                    {
                        return false;
                    }
                }

                return true;
            }

            static bool ConsumeIngredients(in ResourceRecipeBlob recipe, ref StorehouseInventory inventory,
                DynamicBuffer<StorehouseInventoryItem> items, BlobAssetReference<ResourceTypeIndexBlob> catalog)
            {
                for (int ingredientIndex = 0; ingredientIndex < recipe.Ingredients.Length; ingredientIndex++)
                {
                    var ingredient = recipe.Ingredients[ingredientIndex];
                    var resourceIndex = ResolveResourceTypeIndex(ingredient.ResourceId, catalog);
                    if (resourceIndex == ushort.MaxValue)
                    {
                        return false;
                    }

                    if (!StorehouseMutationService.TryConsumeUnreserved(
                            resourceIndex,
                            ingredient.Amount,
                            catalog,
                            ref inventory,
                            items))
                    {
                        return false;
                    }
                }
                return true;
            }

            static bool TryProduceOutput(ref ResourceProcessorState processorState, ref StorehouseInventory inventory,
                DynamicBuffer<StorehouseInventoryItem> items, DynamicBuffer<StorehouseCapacityElement> capacities,
                DynamicBuffer<StorehouseReservationItem> reservations, BlobAssetReference<ResourceTypeIndexBlob> catalog)
            {
                if (processorState.OutputResourceId.Length == 0 || processorState.OutputAmount <= 0)
                {
                    return true;
                }

                var outputIndex = ResolveResourceTypeIndex(processorState.OutputResourceId, catalog);
                if (outputIndex == ushort.MaxValue)
                {
                    return true;
                }

                var amount = (float)processorState.OutputAmount;
                if (!StorehouseMutationService.HasCapacityForDeposit(
                        outputIndex,
                        amount,
                        catalog,
                        items,
                        capacities,
                        reservations))
                {
                    return false;
                }

                if (!StorehouseMutationService.TryDepositWithPerTypeCapacity(
                        outputIndex,
                        amount,
                        catalog,
                        ref inventory,
                        items,
                        capacities,
                        reservations,
                        out var depositedAmount))
                {
                    return false;
                }

                if (depositedAmount + 1e-3f < amount)
                {
                    processorState.OutputAmount = (int)math.max(1f, math.ceil(amount - depositedAmount));
                    return false;
                }

                return true;
            }

            static ushort ResolveResourceTypeIndex(FixedString64Bytes resourceId, BlobAssetReference<ResourceTypeIndexBlob> catalog)
            {
                var index = catalog.Value.LookupIndex(resourceId);
                return index < 0 ? ushort.MaxValue : (ushort)index;
            }
        }
    }
}
