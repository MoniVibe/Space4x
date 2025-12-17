#if UNITY_EDITOR
using PureDOTS.Rendering;
using Space4X.Debug;
using Unity.Entities;
using Unity.Rendering;

namespace Space4X.DebugSystems
{
    using Debug = UnityEngine.Debug;

    [WorldSystemFilter(WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4X_RenderDebugSystem : ISystem
    {
        private EntityQuery _renderKeyQuery;
        private EntityQuery _materialMeshQuery;

        public void OnCreate(ref SystemState state)
        {
            _renderKeyQuery = state.GetEntityQuery(ComponentType.ReadOnly<RenderKey>());
            _materialMeshQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<RenderKey>(),
                ComponentType.ReadOnly<MaterialMeshInfo>());
        }

        public void OnDestroy(ref SystemState state) { }

        public void OnUpdate(ref SystemState state)
        {
            if (!Space4XDebugSettings.TryConsumeRenderSnapshotRequest())
                return;

            int keyCount = _renderKeyQuery.CalculateEntityCount();
            int meshCount = _materialMeshQuery.CalculateEntityCount();

            Space4XBurstDebug.Log($"[Space4X RenderDebug] keys:{keyCount} meshEntities:{meshCount}");
        }
    }
}
#endif





