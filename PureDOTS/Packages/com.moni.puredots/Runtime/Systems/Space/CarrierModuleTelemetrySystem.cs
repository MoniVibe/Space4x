using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Space;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Space
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CarrierModuleStatAggregationSystem))]
    public partial class CarrierModuleTelemetrySystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<TimeState>();
            RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        protected override void OnUpdate()
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            if (time.IsPaused)
            {
                return;
            }

            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (rewind.Mode != RewindMode.Record)
            {
                return;
            }

            var telemetry = new CarrierModuleTelemetry();

            foreach (var (totals, repairTickets, power) in SystemAPI.Query<CarrierModuleStatTotals, DynamicBuffer<ModuleRepairTicket>, CarrierPowerBudget>())
            {
                telemetry.CarrierCount++;
                telemetry.ActiveModules += totals.DamagedModuleCount + totals.DestroyedModuleCount == 0 ? 1 : 0;
                telemetry.DamagedModules += totals.DamagedModuleCount;
                telemetry.DestroyedModules += totals.DestroyedModuleCount;
                telemetry.RepairTicketCount += repairTickets.Length;
                telemetry.TotalPowerDraw += power.CurrentDraw;
                telemetry.TotalPowerGeneration += power.CurrentGeneration;
                telemetry.NetPower += power.CurrentGeneration - power.CurrentDraw;
                telemetry.AnyOverBudget |= power.OverBudget;
            }

            if (!SystemAPI.HasSingleton<CarrierModuleTelemetry>())
            {
                var entity = EntityManager.CreateEntity(typeof(CarrierModuleTelemetry));
                EntityManager.SetComponentData(entity, telemetry);
            }
            else
            {
                SystemAPI.SetSingleton(telemetry);
            }
        }
    }
}
