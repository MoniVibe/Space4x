using PureDOTS.Runtime.Components;
using PureDOTS.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Economy.Resources
{
    /// <summary>
    /// Spawns items into inventories (scenario setup, resource generation, miracles).
    /// All spawn operations must be explicit and logged.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct InventorySpawnSystem : ISystem
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

            // Process spawn requests
            foreach (var (spawnRequest, entity) in SystemAPI.Query<RefRO<InventorySpawnRequest>>().WithEntityAccess())
            {
                ProcessSpawn(ref state, spawnRequest.ValueRO, ref catalogBlob, tick);
                state.EntityManager.RemoveComponent<InventorySpawnRequest>(entity);
            }
        }

        [BurstCompile]
        private void ProcessSpawn(ref SystemState state, InventorySpawnRequest request, ref ItemSpecCatalogBlob catalog, uint tick)
        {
            if (!_inventoryLookup.HasComponent(request.Target))
            {
                return;
            }

            if (!_itemBufferLookup.HasBuffer(request.Target))
            {
                state.EntityManager.AddBuffer<InventoryItem>(request.Target);
            }

            if (!TryFindItemSpec(request.ItemId, ref catalog, out var spec))
            {
                return;
            }

            var inventory = _inventoryLookup[request.Target];
            var items = _itemBufferLookup[request.Target];

            // Check capacity
            float massToAdd = request.Quantity * spec.MassPerUnit;
            float volumeToAdd = request.Quantity * spec.VolumePerUnit;

            if (inventory.CurrentMass + massToAdd > inventory.MaxMass)
            {
                return;
            }

            if (inventory.MaxVolume > 0f && inventory.CurrentVolume + volumeToAdd > inventory.MaxVolume)
            {
                return;
            }

            // Add item
            AddItem(ref items, request.ItemId, request.Quantity, request.Quality, request.Durability, tick);
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

    /// <summary>
    /// Request to spawn items into an inventory.
    /// Added by scenario setup, resource generation, miracles, etc.
    /// </summary>
    public struct InventorySpawnRequest : IComponentData
    {
        public Entity Target;
        public FixedString64Bytes ItemId;
        public float Quantity;
        public float Quality;
        public float Durability;
        public FixedString128Bytes Source; // "scenario_setup", "miracle", "resource_generation", etc.
    }
}

