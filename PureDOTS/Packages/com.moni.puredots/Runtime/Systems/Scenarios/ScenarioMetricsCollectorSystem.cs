using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Resources;
using PureDOTS.Runtime.Scenarios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Scenarios
{
    /// <summary>
    /// Collects common scenario metrics so assertions/telemetry can reference them.
    /// Scans gameplay state each LateSimulation tick and writes values into ScenarioMetricsUtility.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public partial struct ScenarioMetricsCollectorSystem : ISystem
    {
        private EntityQuery _villagerQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScenarioInfo>();
            _villagerQuery = state.GetEntityQuery(ComponentType.ReadOnly<VillagerId>());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Resolve scenario entity
            Entity scenarioEntity = Entity.Null;
            if (SystemAPI.TryGetSingleton<ScenarioEntitySingleton>(out var scenarioSingleton))
            {
                scenarioEntity = scenarioSingleton.Value;
            }
            else if (SystemAPI.HasSingleton<ScenarioInfo>())
            {
                scenarioEntity = SystemAPI.GetSingletonEntity<ScenarioInfo>();
            }

            if (scenarioEntity == Entity.Null)
            {
                return;
            }

            // Get buffer lookup
            var metricLookup = SystemAPI.GetBufferLookup<ScenarioMetricSample>(isReadOnly: false);
            metricLookup.Update(ref state);

            if (!metricLookup.HasBuffer(scenarioEntity))
            {
                return;
            }

            // Villager count (generic VillagerId component).
            ScenarioMetricsUtility.SetMetric(ref metricLookup, scenarioEntity, "villager.count", _villagerQuery.CalculateEntityCount());

            // Completed deliveries (DeliveryReceipt buffers).
            double totalDeliveries = 0;
            foreach (var receipts in SystemAPI.Query<DynamicBuffer<DeliveryReceipt>>())
            {
                totalDeliveries += receipts.Length;
            }
            ScenarioMetricsUtility.SetMetric(ref metricLookup, scenarioEntity, "deliveries.count", totalDeliveries);

            // Total storehouse inventory across all storehouses.
            double totalInventory = 0;
            foreach (var inventory in SystemAPI.Query<DynamicBuffer<StorehouseInventoryItem>>())
            {
                for (int i = 0; i < inventory.Length; i++)
                {
                    totalInventory += inventory[i].Amount;
                }
            }
            ScenarioMetricsUtility.SetMetric(ref metricLookup, scenarioEntity, "storehouse.inventory", totalInventory);

            // Defaults for boolean metrics – systems can override when violations occur.
            ScenarioMetricsUtility.SetMetricIfUnset(ref metricLookup, scenarioEntity, "constraints.respected", 1.0);
            ScenarioMetricsUtility.SetMetricIfUnset(ref metricLookup, scenarioEntity, "deterministic.replay", 1.0);
        }
    }
}



