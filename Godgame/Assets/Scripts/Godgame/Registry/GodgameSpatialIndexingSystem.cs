using PureDOTS.Runtime.Spatial;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

namespace Godgame.Registry
{
    /// <summary>
    /// Ensures Godgame villagers and storehouses participate in the shared spatial grid.
    /// Adds <see cref="SpatialIndexedTag" /> whenever eligible entities are missing it so
    /// PureDOTS spatial systems can track residency automatically.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup), OrderFirst = true)]
    public partial struct GodgameSpatialIndexingSystem : ISystem
    {
        private EntityQuery _villagerQuery;
        private EntityQuery _storehouseQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _villagerQuery = SystemAPI.QueryBuilder()
                .WithAll<GodgameVillager, LocalTransform>()
                .WithNone<SpatialIndexedTag>()
                .Build();

            _storehouseQuery = SystemAPI.QueryBuilder()
                .WithAll<GodgameStorehouse, LocalTransform>()
                .WithNone<SpatialIndexedTag>()
                .Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!_villagerQuery.IsEmptyIgnoreFilter)
            {
                state.EntityManager.AddComponent<SpatialIndexedTag>(_villagerQuery);
            }

            if (!_storehouseQuery.IsEmptyIgnoreFilter)
            {
                state.EntityManager.AddComponent<SpatialIndexedTag>(_storehouseQuery);
            }
        }
    }
}
