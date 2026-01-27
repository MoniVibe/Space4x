using PureDOTS.Environment;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Environment
{
    /// <summary>
    /// Ensures the terrain modification queue singleton exists.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
    public partial struct TerrainModificationBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            using var query = state.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<TerrainModificationQueue>());
            if (!query.IsEmptyIgnoreFilter)
            {
                state.Enabled = false;
                return;
            }

            var entity = state.EntityManager.CreateEntity(typeof(TerrainModificationQueue), typeof(TerrainModificationBudget));
            state.EntityManager.AddBuffer<TerrainModificationRequest>(entity);
            state.EntityManager.AddBuffer<TerrainDirtyRegion>(entity);
            state.EntityManager.AddBuffer<TerrainModificationEvent>(entity);
            state.EntityManager.AddBuffer<TerrainSurfaceTileVersion>(entity);
            state.EntityManager.AddBuffer<TerrainUndergroundChunkVersion>(entity);
            state.EntityManager.SetComponentData(entity, TerrainModificationBudget.Default);

            state.Enabled = false;
        }

        public void OnUpdate(ref SystemState state)
        {
        }
    }
}
