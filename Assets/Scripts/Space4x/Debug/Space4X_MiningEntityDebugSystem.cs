using Unity.Entities;
using Space4X.Registry;

#if UNITY_EDITOR
using Space4X.Debug;

namespace Space4X.DebugSystems
{
    using Debug = UnityEngine.Debug;

    [WorldSystemFilter(WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4X_MiningEntityDebugSystem : ISystem
    {
        private EntityQuery _carrierQuery;
        private EntityQuery _minerQuery;
        private EntityQuery _rockQuery;

        public void OnCreate(ref SystemState state)
        {
            _carrierQuery = state.GetEntityQuery(ComponentType.ReadOnly<Carrier>());
            _minerQuery = state.GetEntityQuery(ComponentType.ReadOnly<MiningVessel>());
            _rockQuery = state.GetEntityQuery(ComponentType.ReadOnly<Asteroid>());
        }

        public void OnDestroy(ref SystemState state) { }

        public void OnUpdate(ref SystemState state)
        {
            if (!Space4XDebugSettings.TryConsumeMiningSnapshotRequest())
                return;

            int carrierCount = _carrierQuery.CalculateEntityCount();
            int minerCount = _minerQuery.CalculateEntityCount();
            int rockCount = _rockQuery.CalculateEntityCount();

            Space4XBurstDebug.Log($"[Space4X MiningDebug] carriers:{carrierCount} miners:{minerCount} rocks:{rockCount}");
        }
    }
}
#endif
