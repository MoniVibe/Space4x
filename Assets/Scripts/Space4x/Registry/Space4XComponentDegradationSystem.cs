using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Applies gradual degradation and hazard damage to module health.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(GameplayFixedStepSyncSystem))]
    public partial struct Space4XComponentDegradationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<GameplayFixedStep>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
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

            var deltaTime = SystemAPI.GetSingleton<GameplayFixedStep>().FixedDeltaTime;

            var hasMaintenanceLog = SystemAPI.TryGetSingletonBuffer<ModuleMaintenanceCommandLogEntry>(out var maintenanceLog);
            var hasMaintenanceTelemetry = SystemAPI.TryGetSingletonEntity<ModuleMaintenanceTelemetry>(out var maintenanceTelemetryEntity);
            var maintenanceTelemetry = hasMaintenanceTelemetry ? state.EntityManager.GetComponentData<ModuleMaintenanceTelemetry>(maintenanceTelemetryEntity) : default;
            var telemetryDirty = false;
            var tick = time.Tick;

            foreach (var (health, entity) in SystemAPI.Query<RefRW<ModuleHealth>>().WithNone<HazardDamageEvent>().WithEntityAccess())
            {
                var failed = ApplyDegradation(ref health.ValueRW, deltaTime, 0f);
                if (failed)
                {
                    Space4XModuleMaintenanceUtility.LogEvent(hasMaintenanceLog, maintenanceLog, tick, Entity.Null, -1, entity, ModuleMaintenanceEventType.ModuleFailed, 0f);
                    if (hasMaintenanceTelemetry)
                    {
                        telemetryDirty |= Space4XModuleMaintenanceUtility.ApplyTelemetry(ModuleMaintenanceEventType.ModuleFailed, 0f, tick, ref maintenanceTelemetry);
                    }
                }
            }

            foreach (var (health, hazardEvents, entity) in SystemAPI.Query<RefRW<ModuleHealth>, DynamicBuffer<HazardDamageEvent>>().WithEntityAccess())
            {
                var damage = 0f;
                for (var i = 0; i < hazardEvents.Length; i++)
                {
                    damage += math.max(0f, hazardEvents[i].Amount);
                }

                hazardEvents.Clear();
                var failed = ApplyDegradation(ref health.ValueRW, deltaTime, damage);
                if (failed)
                {
                    Space4XModuleMaintenanceUtility.LogEvent(hasMaintenanceLog, maintenanceLog, tick, Entity.Null, -1, entity, ModuleMaintenanceEventType.ModuleFailed, damage);
                    if (hasMaintenanceTelemetry)
                    {
                        telemetryDirty |= Space4XModuleMaintenanceUtility.ApplyTelemetry(ModuleMaintenanceEventType.ModuleFailed, damage, tick, ref maintenanceTelemetry);
                    }
                }
            }

            if (telemetryDirty && hasMaintenanceTelemetry)
            {
                state.EntityManager.SetComponentData(maintenanceTelemetryEntity, maintenanceTelemetry);
            }
        }

        private static bool ApplyDegradation(ref ModuleHealth health, float deltaTime, float incomingDamage)
        {
            var degradation = math.max(0f, health.DegradationPerSecond) * math.max(0f, deltaTime);
            var newHealth = health.CurrentHealth - degradation - math.max(0f, incomingDamage);
            newHealth = math.min(newHealth, health.MaxHealth);

            var wasFailed = health.Failed != 0;
            if (newHealth <= 0f)
            {
                health.CurrentHealth = 0f;
                health.Failed = 1;
                return !wasFailed;
            }

            health.CurrentHealth = newHealth;
            health.Failed = 0;
            return false;
        }
    }
}
