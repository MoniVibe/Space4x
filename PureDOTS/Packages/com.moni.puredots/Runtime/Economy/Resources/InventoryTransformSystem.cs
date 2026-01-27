using PureDOTS.Runtime.Components;
using PureDOTS.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Economy.Resources
{
    /// <summary>
    /// Handles atomic transform operations (consume X, produce Y).
    /// Stub for production system - all-or-nothing atomic operation.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct InventoryTransformSystem : ISystem
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

            // Process transform requests
            foreach (var (transformRequest, entity) in SystemAPI.Query<RefRO<InventoryTransformRequest>>().WithEntityAccess())
            {
                if (!state.EntityManager.HasBuffer<TransformInputBuffer>(entity) || !state.EntityManager.HasBuffer<TransformOutputBuffer>(entity))
                {
                    continue;
                }

                var inputs = state.EntityManager.GetBuffer<TransformInputBuffer>(entity);
                var outputs = state.EntityManager.GetBuffer<TransformOutputBuffer>(entity);
                ProcessTransform(ref state, transformRequest.ValueRO, inputs, outputs, ref catalogBlob, tick);
                state.EntityManager.RemoveComponent<InventoryTransformRequest>(entity);
            }
        }

        [BurstCompile]
        private void ProcessTransform(ref SystemState state, InventoryTransformRequest request, DynamicBuffer<TransformInputBuffer> inputs, DynamicBuffer<TransformOutputBuffer> outputs, ref ItemSpecCatalogBlob catalog, uint tick)
        {
            if (!_inventoryLookup.HasComponent(request.Target))
            {
                return;
            }

            if (!_itemBufferLookup.HasBuffer(request.Target))
            {
                return;
            }

            var inventory = _inventoryLookup[request.Target];
            var items = _itemBufferLookup[request.Target];

            // Check inputs are available
            for (int i = 0; i < inputs.Length; i++)
            {
                var input = inputs[i];
                float available = GetItemQuantity(in items, input.ItemId);
                if (available < input.Quantity)
                {
                    return; // Cannot transform - missing inputs
                }
            }

            // Check output capacity
            for (int i = 0; i < outputs.Length; i++)
            {
                var output = outputs[i];
                if (!TryFindItemSpec(output.ItemId, ref catalog, out var spec))
                {
                    return;
                }

                float massToAdd = output.Quantity * spec.MassPerUnit;
                float volumeToAdd = output.Quantity * spec.VolumePerUnit;

                if (inventory.CurrentMass + massToAdd > inventory.MaxMass)
                {
                    return; // Cannot transform - no capacity
                }

                if (inventory.MaxVolume > 0f && inventory.CurrentVolume + volumeToAdd > inventory.MaxVolume)
                {
                    return; // Cannot transform - no capacity
                }
            }

            // All checks passed - perform atomic transform
            // Remove inputs
            for (int i = 0; i < inputs.Length; i++)
            {
                var input = inputs[i];
                RemoveItem(ref items, input.ItemId, input.Quantity);
            }

            // Add outputs
            for (int i = 0; i < outputs.Length; i++)
            {
                var output = outputs[i];
                AddItem(ref items, output.ItemId, output.Quantity, output.Quality, output.Durability, tick);
            }
        }

        [BurstCompile]
        private static bool TryFindItemSpec(in FixedString64Bytes itemId, ref ItemSpecCatalogBlob catalog, out ItemSpecBlob spec)
        {
            for (int i = 0; i < catalog.Items.Length; i++)
            {
                ref var candidateSpec = ref catalog.Items[i];
                if (candidateSpec.ItemId.Equals(itemId))
                {
                    spec = candidateSpec;
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
    /// Input item for transform operation.
    /// </summary>
    public struct TransformInput
    {
        public FixedString64Bytes ItemId;
        public float Quantity;
    }

    /// <summary>
    /// Output item for transform operation.
    /// </summary>
    public struct TransformOutput
    {
        public FixedString64Bytes ItemId;
        public float Quantity;
        public float Quality;
        public float Durability;
    }

    /// <summary>
    /// Request to transform items (consume X, produce Y).
    /// Atomic all-or-nothing operation.
    /// </summary>
    public struct InventoryTransformRequest : IComponentData
    {
        public Entity Target;
    }

    /// <summary>
    /// Buffer of input items for transform operation.
    /// </summary>
    public struct TransformInputBuffer : IBufferElementData
    {
        public FixedString64Bytes ItemId;
        public float Quantity;
    }

    /// <summary>
    /// Buffer of output items for transform operation.
    /// </summary>
    public struct TransformOutputBuffer : IBufferElementData
    {
        public FixedString64Bytes ItemId;
        public float Quantity;
        public float Quality;
        public float Durability;
    }
}

