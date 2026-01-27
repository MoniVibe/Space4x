using PureDOTS.Runtime.Telemetry;
using Unity.Entities;

namespace PureDOTS.Systems.Telemetry
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public sealed partial class SimPhaseProfilerBootstrapSystem : SystemBase
    {
        protected override void OnCreate()
        {
            base.OnCreate();

            var entityManager = EntityManager;
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<SimPhaseProfilerState>());
            if (query.IsEmptyIgnoreFilter)
            {
                var entity = entityManager.CreateEntity(typeof(SimPhaseProfilerState), typeof(SimPhaseProfilerPhaseStartTimes));
                entityManager.SetComponentData(entity, default(SimPhaseProfilerState));
                entityManager.SetComponentData(entity, SimPhaseProfilerPhaseStartTimesExtensions.CreateDefault());
            }

            Enabled = false;
        }

        protected override void OnUpdate()
        {
            // No runtime work required; entity only needs to exist.
        }
    }
}
