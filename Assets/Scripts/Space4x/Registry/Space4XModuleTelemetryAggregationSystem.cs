using PureDOTS.Systems;
using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Aggregates module ratings and maintenance telemetry into the registry snapshot.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XModuleRatingAggregationSystem))]
    [UpdateAfter(typeof(Space4XModuleMaintenanceTelemetrySystem))]
    public partial struct Space4XModuleTelemetryAggregationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<Space4XRegistrySnapshot>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<Space4XRegistrySnapshot>(out var snapshotEntity))
            {
                return;
            }

            ref var snapshot = ref SystemAPI.GetComponentRW<Space4XRegistrySnapshot>(snapshotEntity).ValueRW;

            int offenseTotal = 0;
            int defenseTotal = 0;
            int utilityTotal = 0;
            float powerBalanceTotal = 0f;
            int degradedTotal = 0;
            int repairingTotal = 0;
            int refittingTotal = 0;
            int carrierCount = 0;

            foreach (var (ratings, _) in SystemAPI.Query<RefRO<ModuleRatingAggregate>>().WithAll<Carrier>().WithEntityAccess())
            {
                var r = ratings.ValueRO;
                offenseTotal += r.OffenseRating;
                defenseTotal += r.DefenseRating;
                utilityTotal += r.UtilityRating;
                powerBalanceTotal += r.PowerBalanceMW;
                degradedTotal += r.DegradedModuleCount;
                repairingTotal += r.RepairingModuleCount;
                refittingTotal += r.RefittingModuleCount;
                carrierCount++;
            }

            snapshot.ModuleOffenseRatingTotal = offenseTotal;
            snapshot.ModuleDefenseRatingTotal = defenseTotal;
            snapshot.ModuleUtilityRatingTotal = utilityTotal;
            snapshot.ModulePowerBalanceMW = powerBalanceTotal;
            snapshot.ModuleDegradedCount = degradedTotal;
            snapshot.ModuleRepairingCount = repairingTotal;
            snapshot.ModuleRefittingCount = refittingTotal;

            if (SystemAPI.TryGetSingletonEntity<ModuleMaintenanceTelemetry>(out var maintenanceEntity))
            {
                var maintenance = SystemAPI.GetComponentRO<ModuleMaintenanceTelemetry>(maintenanceEntity).ValueRO;
                snapshot.ModuleRefitCount = (int)maintenance.RefitCompleted;
                snapshot.ModuleRepairCount = maintenance.RepairApplied > 0f ? 1 : 0;
                
                if (maintenance.RefitCompleted > 0)
                {
                    snapshot.ModuleRefitDurationAvgSeconds = maintenance.RefitWorkApplied / math.max(1f, maintenance.RefitCompleted);
                }
            }
        }
    }
}

