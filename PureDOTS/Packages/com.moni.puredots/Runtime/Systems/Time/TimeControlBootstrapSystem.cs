using PureDOTS.Runtime.Components;
using Unity.Entities;

namespace PureDOTS.Systems.Time
{
    /// <summary>
    /// Ensures time control singletons exist for simulation/game input systems.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
    public partial struct TimeControlBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            var entityManager = state.EntityManager;
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<TimeControlSingletonTag>());
            var controlEntity = query.IsEmptyIgnoreFilter
                ? entityManager.CreateEntity(typeof(TimeControlSingletonTag))
                : query.GetSingletonEntity();

            if (!entityManager.HasComponent<TimeControlConfig>(controlEntity))
            {
                entityManager.AddComponentData(controlEntity, TimeControlConfig.CreateDefault());
            }

            if (!entityManager.HasComponent<TimeControlInputState>(controlEntity))
            {
                entityManager.AddComponentData(controlEntity, default(TimeControlInputState));
            }

            if (!entityManager.HasBuffer<TimeControlCommand>(controlEntity))
            {
                entityManager.AddBuffer<TimeControlCommand>(controlEntity);
            }

            state.Enabled = false;
        }

        public void OnUpdate(ref SystemState state) { }
    }
}
