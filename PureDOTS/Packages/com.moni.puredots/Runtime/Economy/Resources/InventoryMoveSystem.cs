using PureDOTS.Runtime.Components;
using PureDOTS.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Economy.Resources
{
    /// <summary>
    /// Transfers items between inventories.
    /// Handles capacity checks and stack merging.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct InventoryMoveSystem : ISystem
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

            // Process move requests
            foreach (var (moveRequest, entity) in SystemAPI.Query<RefRO<InventoryMoveRequest>>().WithEntityAccess())
            {
                ProcessMove(ref state, moveRequest.ValueRO, ref catalogBlob);
                state.EntityManager.RemoveComponent<InventoryMoveRequest>(entity);
            }
        }

        [BurstCompile]
        private void ProcessMove(ref SystemState state, InventoryMoveRequest request, ref ItemSpecCatalogBlob catalog)
        {
            if (!_inventoryLookup.HasComponent(request.Source) || !_inventoryLookup.HasComponent(request.Destination))
            {
                return;
            }

            if (!_itemBufferLookup.HasBuffer(request.Source) || !_itemBufferLookup.HasBuffer(request.Destination))
            {
                return;
            }

            var sourceInventory = _inventoryLookup[request.Source];
            var destInventory = _inventoryLookup[request.Destination];
            var sourceItems = _itemBufferLookup[request.Source];
            var destItems = _itemBufferLookup[request.Destination];

            // Find item spec for mass/volume calculation
            if (!TryFindItemSpec(request.ItemId, ref catalog, out var spec))
            {
                return;
            }

            // Check if source has enough
            float available = GetItemQuantity(in sourceItems, request.ItemId);
            if (available < request.Quantity)
            {
                return;
            }

            // Check destination capacity
            float massToAdd = request.Quantity * spec.MassPerUnit;
            float volumeToAdd = request.Quantity * spec.VolumePerUnit;

            if (destInventory.CurrentMass + massToAdd > destInventory.MaxMass)
            {
                return;
            }

            if (destInventory.MaxVolume > 0f && destInventory.CurrentVolume + volumeToAdd > destInventory.MaxVolume)
            {
                return;
            }

            // Remove from source
            RemoveItem(ref sourceItems, request.ItemId, request.Quantity);

            // Add to destination
            AddItem(destItems, request.ItemId, request.Quantity, request.Quality, request.Durability);
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

        [BurstCompile]
        private static float GetItemQuantity(in DynamicBuffer<InventoryItem> items, in FixedString64Bytes itemId)
        {
            float total = 0f;
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i].ItemId.Equals(itemId))
                {
                    total += items[i].Quantity;
                }
            }

            return total;
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
        private void AddItem(DynamicBuffer<InventoryItem> items, FixedString64Bytes itemId, float quantity, float quality, float durability)
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
            var tickTimeState = SystemAPI.GetSingleton<TickTimeState>();
            items.Add(new InventoryItem
            {
                ItemId = itemId,
                Quantity = quantity,
                Quality = quality,
                Durability = durability,
                CreatedTick = tickTimeState.Tick
            });
        }
    }

    /// <summary>
    /// Request to move items between inventories.
    /// Added by other systems, processed by InventoryMoveSystem.
    /// </summary>
    public struct InventoryMoveRequest : IComponentData
    {
        public Entity Source;
        public Entity Destination;
        public FixedString64Bytes ItemId;
        public float Quantity;
        public float Quality;
        public float Durability;
    }
}

