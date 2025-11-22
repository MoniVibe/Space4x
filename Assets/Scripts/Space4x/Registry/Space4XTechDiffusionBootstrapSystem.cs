using PureDOTS.Runtime.Components;
using Unity.Entities;

namespace Space4X.Registry
{
    /// <summary>
    /// Ensures a tech diffusion telemetry/log singleton exists.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4XTechDiffusionBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.TryGetSingletonEntity<TechDiffusionTelemetry>(out _))
            {
                state.Enabled = false;
                return;
            }

            var entity = state.EntityManager.CreateEntity(typeof(TechDiffusionTelemetry));
            state.EntityManager.AddBuffer<TechDiffusionCommandLogEntry>(entity);
            state.EntityManager.SetComponentData(entity, new TechDiffusionTelemetry
            {
                LastUpdateTick = 0,
                LastUpgradeTick = 0,
                ActiveDiffusions = 0,
                CompletedUpgrades = 0
            });

            state.Enabled = false;
        }
    }
}
