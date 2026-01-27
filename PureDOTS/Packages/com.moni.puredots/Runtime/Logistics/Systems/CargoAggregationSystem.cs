using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Economy.Resources;
using PureDOTS.Runtime.Logistics.Blobs;
using PureDOTS.Runtime.Logistics.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Logistics.Systems
{
    /// <summary>
    /// Aggregates cargo items into load state, load effect, and value state.
    /// Updates CargoLoadState, LoadEffect, and CargoValueState from CargoItem buffer.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct CargoAggregationSystem : ISystem
    {
        private ComponentLookup<HaulerCapacity> _capacityLookup;
        private BufferLookup<CargoItem> _cargoBufferLookup;
        private BufferLookup<CargoContainerSlot> _containerBufferLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TickTimeState>();
            state.RequireForUpdate<ItemSpecCatalog>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<ScenarioState>();
            _capacityLookup = state.GetComponentLookup<HaulerCapacity>(false);
            _cargoBufferLookup = state.GetBufferLookup<CargoItem>(false);
            _containerBufferLookup = state.GetBufferLookup<CargoContainerSlot>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<ScenarioState>(out var scenario)
                || !scenario.IsInitialized
                || !scenario.EnableEconomy)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState)
                || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<ItemSpecCatalog>(out var itemCatalog))
            {
                return;
            }

            ref var itemCatalogBlob = ref itemCatalog.Catalog.Value;

            _capacityLookup.Update(ref state);
            _cargoBufferLookup.Update(ref state);
            _containerBufferLookup.Update(ref state);

            var tickTimeState = SystemAPI.GetSingleton<TickTimeState>();
            var tick = tickTimeState.Tick;

            foreach (var (loadState, loadEffect, valueState, entity) in SystemAPI.Query<
                RefRW<CargoLoadState>,
                RefRW<LoadEffect>,
                RefRW<CargoValueState>>()
                .WithAll<HaulerTag>()
                .WithEntityAccess())
            {
                if (!_cargoBufferLookup.HasBuffer(entity))
                {
                    // Reset to zero if no cargo buffer
                    loadState.ValueRW = default;
                    loadEffect.ValueRW = default;
                    valueState.ValueRW = default;
                    continue;
                }

                var cargoItems = _cargoBufferLookup[entity];
                float totalMass = 0f;
                float totalVolume = 0f;
                float totalValue = 0f;
                float maxHazard = 0f;

                // Aggregate cargo items
                for (int i = 0; i < cargoItems.Length; i++)
                {
                    var cargo = cargoItems[i];
                    if (TryFindItemSpec(cargo.ResourceId, ref itemCatalogBlob, out var itemSpec))
                    {
                        float itemMass = cargo.Amount * itemSpec.MassPerUnit;
                        float itemVolume = cargo.Amount * itemSpec.VolumePerUnit;
                        float itemValue = cargo.Amount * itemSpec.BaseValue;

                        totalMass += itemMass;
                        totalVolume += itemVolume;
                        totalValue += itemValue;

                        // Calculate effective hazard (would use ResourceDef.HazardLevel if available)
                        // For now, use item tags as proxy
                        float hazard = 0f;
                        if ((itemSpec.Tags & ItemTags.Flammable) != 0)
                        {
                            hazard = 0.3f;
                        }
                        if (maxHazard < hazard)
                        {
                            maxHazard = hazard;
                        }
                    }
                }

                // Update load state
                loadState.ValueRW.TotalMass = totalMass;
                loadState.ValueRW.TotalVolume = totalVolume;
                loadState.ValueRW.TotalValue = totalValue;
                loadState.ValueRW.HazardAggregate = maxHazard;
                loadState.ValueRW.LastUpdateTick = tick;

                // Update load effect
                float loadRatio = 0f;
                if (_capacityLookup.TryGetComponent(entity, out var capacity))
                {
                    loadRatio = capacity.MaxMass > 0 ? totalMass / capacity.MaxMass : 0f;
                }
                loadEffect.ValueRW.LoadMass = totalMass;
                loadEffect.ValueRW.LoadRatio = loadRatio;

                // Update value state (basic calculation, can be enhanced with route risk, etc.)
                valueState.ValueRW.TotalValue = totalValue;
                valueState.ValueRW.RaidAttractiveness = totalValue * 0.001f; // Simple scaling, can be enhanced
                valueState.ValueRW.EscortPriority = totalValue * 0.001f; // Simple scaling, can be enhanced
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

