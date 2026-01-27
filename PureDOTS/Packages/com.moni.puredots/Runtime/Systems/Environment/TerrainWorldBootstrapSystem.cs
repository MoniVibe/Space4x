using PureDOTS.Environment;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Environment
{
    /// <summary>
    /// Ensures the TerrainWorldConfig singleton exists.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
    public partial struct TerrainWorldBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            using var query = state.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<TerrainWorldConfig>());
            if (!query.IsEmptyIgnoreFilter)
            {
                state.Enabled = false;
                return;
            }

            var entity = state.EntityManager.CreateEntity(typeof(TerrainWorldConfig));
            state.EntityManager.SetComponentData(entity, TerrainWorldConfig.Default);
            state.Enabled = false;
        }

        public void OnUpdate(ref SystemState state)
        {
        }
    }
}
