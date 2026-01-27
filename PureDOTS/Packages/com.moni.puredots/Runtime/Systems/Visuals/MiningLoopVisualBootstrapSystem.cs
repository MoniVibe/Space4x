using PureDOTS.Runtime.Visuals;
using Unity.Entities;

namespace PureDOTS.Systems.Visuals
{
    [UpdateInGroup(typeof(TimeSystemGroup))]
    [CreateAfter(typeof(CoreSingletonBootstrapSystem))]
    public partial struct MiningLoopVisualBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            var entityManager = state.EntityManager;

            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<MiningVisualManifest>());
            if (query.IsEmptyIgnoreFilter)
            {
                var manifestEntity = entityManager.CreateEntity(typeof(MiningVisualManifest));
                entityManager.AddBuffer<MiningVisualRequest>(manifestEntity);
            }

            state.Enabled = false;
        }

        public void OnUpdate(ref SystemState state)
        {
        }
    }
}

