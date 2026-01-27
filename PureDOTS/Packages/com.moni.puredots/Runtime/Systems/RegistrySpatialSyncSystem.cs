using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Registry;
using PureDOTS.Runtime.Spatial;
using PureDOTS.Systems.Spatial;
using Unity.Entities;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Publishes the latest spatial grid version so registry systems can align their continuity snapshots.
    /// </summary>
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(SpatialGridBuildSystem))]
    public partial struct RegistrySpatialSyncSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RegistrySpatialSyncState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var syncEntity = SystemAPI.GetSingletonEntity<RegistrySpatialSyncState>();
            ref var syncState = ref SystemAPI.GetComponentRW<RegistrySpatialSyncState>(syncEntity).ValueRW;

            if (SystemAPI.TryGetSingleton<SpatialGridState>(out var gridState))
            {
                var tick = SystemAPI.TryGetSingleton<TimeState>(out var timeState) ? timeState.Tick : syncState.LastPublishedTick;
                syncState.Publish(gridState.Version, tick);
            }
            else
            {
                syncState.Reset();
            }
        }
    }
}
