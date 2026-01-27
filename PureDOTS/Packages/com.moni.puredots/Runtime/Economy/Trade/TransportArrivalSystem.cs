using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Economy.Resources;
using PureDOTS.Runtime.Economy.Wealth;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Economy.Trade
{
    /// <summary>
    /// Unloads cargo on arrival, records arrival events.
    /// Connects to Chunk 2 (inventory operations).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct TransportArrivalSystem : ISystem
    {
        private ComponentLookup<TransportEntity> _transportLookup;
        private ComponentLookup<TransportProgress> _progressLookup;
        private ComponentLookup<Inventory> _inventoryLookup;
        private BufferLookup<InventoryItem> _itemBufferLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TickTimeState>();
            _transportLookup = state.GetComponentLookup<TransportEntity>(false);
            _progressLookup = state.GetComponentLookup<TransportProgress>(false);
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

            _transportLookup.Update(ref state);
            _progressLookup.Update(ref state);
            _inventoryLookup.Update(ref state);
            _itemBufferLookup.Update(ref state);

            foreach (var (transport, progress, entity) in SystemAPI.Query<RefRO<TransportEntity>, RefRW<TransportProgress>>().WithEntityAccess())
            {
                if (progress.ValueRO.LegProgress >= 1f)
                {
                    // Unload cargo to destination inventory
                    if (_itemBufferLookup.HasBuffer(entity))
                    {
                        var cargo = _itemBufferLookup[entity];
                        var destination = transport.ValueRO.DestinationNode;

                        if (_inventoryLookup.HasComponent(destination) && _itemBufferLookup.HasBuffer(destination))
                        {
                            var destItems = _itemBufferLookup[destination];
                            
                            // Transfer all cargo
                            for (int i = 0; i < cargo.Length; i++)
                            {
                                var item = cargo[i];
                                // Add to destination (simplified - should use InventoryMoveSystem)
                                destItems.Add(item);
                            }

                            cargo.Clear();
                        }
                    }

                    // Mark transport as arrived
                    state.EntityManager.RemoveComponent<TransportProgress>(entity);
                }
            }
        }
    }
}

