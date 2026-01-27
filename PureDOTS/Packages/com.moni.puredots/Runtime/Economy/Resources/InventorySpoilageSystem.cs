using PureDOTS.Runtime.Components;
using PureDOTS.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Economy.Resources
{
    /// <summary>
    /// Handles perishable item decay over time.
    /// Removes items from inventory when they spoil completely.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct InventorySpoilageSystem : ISystem
    {
        private ComponentLookup<Inventory> _inventoryLookup;
        private BufferLookup<InventoryItem> _itemBufferLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TickTimeState>();
            state.RequireForUpdate<ItemSpecCatalog>();
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

            if (!SystemAPI.TryGetSingleton<ItemSpecCatalog>(out var catalog))
            {
                return;
            }

            ref var catalogBlob = ref catalog.Catalog.Value;

            _inventoryLookup.Update(ref state);
            _itemBufferLookup.Update(ref state);

            var tickTimeState = SystemAPI.GetSingleton<TickTimeState>();
            var timeScale = math.max(0f, tickTimeState.CurrentSpeedMultiplier);

            foreach (var (inventory, entity) in SystemAPI.Query<RefRW<Inventory>>().WithEntityAccess())
            {
                if (!_itemBufferLookup.HasBuffer(entity))
                {
                    continue;
                }

                var items = _itemBufferLookup[entity];
                float spoiledThisTick = 0f;

                // Process spoilage FIFO
                for (int i = items.Length - 1; i >= 0; i--)
                {
                    var item = items[i];
                    if (!TryFindItemSpec(item.ItemId, ref catalogBlob, out var spec))
                    {
                        continue;
                    }

                    if (!spec.IsPerishable || spec.PerishRate <= 0f)
                    {
                        continue;
                    }

                    var decay = spec.PerishRate * timeScale;
                    var newQuantity = item.Quantity - decay;

                    if (newQuantity <= 0f)
                    {
                        spoiledThisTick += item.Quantity;
                        items.RemoveAt(i);
                    }
                    else
                    {
                        spoiledThisTick += decay;
                        item.Quantity = newQuantity;
                        items[i] = item;
                    }
                }
            }
        }

        [BurstCompile]
        private static bool TryFindItemSpec(in FixedString64Bytes itemId, ref ItemSpecCatalogBlob catalog, out ItemSpecBlob spec)
        {
            for (int i = 0; i < catalog.Items.Length; i++)
            {
                if (catalog.Items[i].ItemId.Equals(itemId))
                {
                    spec = catalog.Items[i];
                    return true;
                }
            }

            spec = default;
            return false;
        }
    }
}

