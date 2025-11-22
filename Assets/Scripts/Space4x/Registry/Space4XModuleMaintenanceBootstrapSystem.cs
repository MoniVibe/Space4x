using PureDOTS.Runtime.Components;
using Unity.Entities;

namespace Space4X.Registry
{
    /// <summary>
    /// Ensures a module maintenance log + telemetry entity exists for refit/repair systems.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4XModuleMaintenanceBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.TryGetSingletonEntity<ModuleMaintenanceLog>(out _))
            {
                state.Enabled = false;
                return;
            }

            var entity = state.EntityManager.CreateEntity(
                typeof(ModuleMaintenanceLog),
                typeof(ModuleMaintenanceTelemetry));

            state.EntityManager.AddBuffer<ModuleMaintenanceCommandLogEntry>(entity);
            state.EntityManager.SetComponentData(entity, new ModuleMaintenanceTelemetry());
            state.EntityManager.SetComponentData(entity, new ModuleMaintenanceLog
            {
                SnapshotHorizon = 512,
                LastPlaybackTick = 0
            });

            state.Enabled = false;
        }
    }
}
