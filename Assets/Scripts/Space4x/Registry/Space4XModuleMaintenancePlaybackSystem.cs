using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Entities;

namespace Space4X.Registry
{
    /// <summary>
    /// Prunes maintenance command logs and rebuilds telemetry during rewind playback.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct Space4XModuleMaintenancePlaybackSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ModuleMaintenanceLog>();
            state.RequireForUpdate<ModuleMaintenanceTelemetry>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            var rewind = SystemAPI.GetSingleton<RewindState>();
            var targetTick = rewind.Mode == RewindMode.Playback ? rewind.PlaybackTick : time.Tick;

            var logEntity = SystemAPI.GetSingletonEntity<ModuleMaintenanceLog>();
            var logBuffer = state.EntityManager.GetBuffer<ModuleMaintenanceCommandLogEntry>(logEntity);
            var logState = SystemAPI.GetComponentRW<ModuleMaintenanceLog>(logEntity);
            var telemetry = SystemAPI.GetComponentRW<ModuleMaintenanceTelemetry>(logEntity);

            var horizon = logState.ValueRO.SnapshotHorizon;
            var cutoff = horizon == 0 ? 0 : (targetTick > horizon ? targetTick - horizon : 0);
            PruneOldEntries(logBuffer, cutoff);

            if (rewind.Mode == RewindMode.Record)
            {
                return;
            }

            var rebuiltTelemetry = default(ModuleMaintenanceTelemetry);
            var lastTick = 0u;
            for (var i = 0; i < logBuffer.Length; i++)
            {
                var entry = logBuffer[i];
                if (entry.Tick > targetTick)
                {
                    continue;
                }

                Space4XModuleMaintenanceUtility.ApplyTelemetry(entry.EventType, entry.Amount, entry.Tick, ref rebuiltTelemetry);
                if (entry.Tick > lastTick)
                {
                    lastTick = entry.Tick;
                }
            }

            rebuiltTelemetry.LastUpdateTick = lastTick;
            telemetry.ValueRW = rebuiltTelemetry;

            logState.ValueRW = new ModuleMaintenanceLog
            {
                SnapshotHorizon = logState.ValueRO.SnapshotHorizon,
                LastPlaybackTick = targetTick
            };
        }

        private static void PruneOldEntries(DynamicBuffer<ModuleMaintenanceCommandLogEntry> buffer, uint cutoffTick)
        {
            if (cutoffTick == 0 || buffer.Length == 0)
            {
                return;
            }

            var write = 0;
            for (var i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].Tick < cutoffTick)
                {
                    continue;
                }

                buffer[write++] = buffer[i];
            }

            buffer.Length = write;
        }
    }
}
