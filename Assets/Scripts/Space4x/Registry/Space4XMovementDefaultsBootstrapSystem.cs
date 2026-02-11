using Space4X.Runtime;
using Unity.Entities;

namespace Space4X.Registry
{
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
    public partial struct Space4XMovementDefaultsBootstrapSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var entityManager = state.EntityManager;
            var createdAny = false;

            if (!SystemAPI.TryGetSingletonEntity<Space4XMovementInertiaConfig>(out _))
            {
                var entity = entityManager.CreateEntity(typeof(Space4XMovementInertiaConfig));
                entityManager.SetComponentData(entity, Space4XMovementInertiaConfig.Default);
                createdAny = true;
            }

            if (!SystemAPI.TryGetSingletonEntity<Space4XMovementTuningConfig>(out _))
            {
                var entity = entityManager.CreateEntity(typeof(Space4XMovementTuningConfig));
                entityManager.SetComponentData(entity, Space4XMovementTuningConfig.Default);
                createdAny = true;
            }

            if (!SystemAPI.TryGetSingletonEntity<Space4XCombatTuningConfig>(out _))
            {
                var entity = entityManager.CreateEntity(typeof(Space4XCombatTuningConfig));
                entityManager.SetComponentData(entity, Space4XCombatTuningConfig.Default);
                createdAny = true;
            }

            state.Enabled = false;
        }
    }
}
