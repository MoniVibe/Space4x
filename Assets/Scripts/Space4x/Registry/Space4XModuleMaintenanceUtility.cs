using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    public static class Space4XModuleMaintenanceUtility
    {
        public static void LogEvent(bool hasLog, DynamicBuffer<ModuleMaintenanceCommandLogEntry> log, uint tick, Entity carrier, int slotIndex, Entity module, ModuleMaintenanceEventType eventType, float amount)
        {
            if (!hasLog)
            {
                return;
            }

            log.Add(new ModuleMaintenanceCommandLogEntry
            {
                Tick = tick,
                Carrier = carrier,
                SlotIndex = slotIndex,
                Module = module,
                EventType = eventType,
                Amount = amount
            });
        }

        public static bool ApplyTelemetry(ModuleMaintenanceEventType eventType, float amount, uint tick, ref ModuleMaintenanceTelemetry telemetry)
        {
            telemetry.LastUpdateTick = math.max(telemetry.LastUpdateTick, tick);
            switch (eventType)
            {
                case ModuleMaintenanceEventType.RefitStarted:
                    telemetry.RefitStarted += 1;
                    break;
                case ModuleMaintenanceEventType.RefitCompleted:
                    telemetry.RefitCompleted += 1;
                    telemetry.RefitWorkApplied += math.max(0f, amount);
                    break;
                case ModuleMaintenanceEventType.RepairApplied:
                    telemetry.RepairApplied += amount;
                    break;
                case ModuleMaintenanceEventType.ModuleFailed:
                    telemetry.Failures += 1;
                    break;
            }

            return true;
        }
    }
}
