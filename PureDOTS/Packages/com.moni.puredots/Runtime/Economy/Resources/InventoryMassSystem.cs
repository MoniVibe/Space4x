using PureDOTS.Runtime.Components;
using PureDOTS.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Economy.Resources
{
    /// <summary>
    /// Calculates total mass and volume for inventories from items using ItemSpec catalog.
    /// Updates Inventory.CurrentMass and CurrentVolume.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct InventoryMassSystem : ISystem
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
            var tick = tickTimeState.Tick;

            foreach (var (inventory, entity) in SystemAPI.Query<RefRW<Inventory>>().WithEntityAccess())
            {
                if (!_itemBufferLookup.HasBuffer(entity))
                {
                    continue;
                }

                var items = _itemBufferLookup[entity];
                float totalMass = 0f;
                float totalVolume = 0f;

                for (int i = 0; i < items.Length; i++)
                {
                    var item = items[i];
                    if (TryFindItemSpec(item.ItemId, ref catalogBlob, out var spec))
                    {
                        totalMass += item.Quantity * spec.MassPerUnit;
                        totalVolume += item.Quantity * spec.VolumePerUnit;
                    }
                }

                inventory.ValueRW.CurrentMass = totalMass;
                inventory.ValueRW.CurrentVolume = totalVolume;
                inventory.ValueRW.LastUpdateTick = tick;
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

