#if UNITY_EDITOR
using Space4X.Rendering;
using Unity.Burst;
using Unity.Entities;
using Unity.Rendering;

namespace Space4X.DebugSystems
{
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4X_RenderDebugSystem : ISystem
    {
        private EntityQuery _renderKeyQuery;
        private EntityQuery _materialMeshQuery;

        public void OnCreate(ref SystemState state)
        {
            _renderKeyQuery = SystemAPI.QueryBuilder()
                .WithAll<RenderKey>()
                .Build();

            _materialMeshQuery = SystemAPI.QueryBuilder()
                .WithAll<RenderKey, MaterialMeshInfo>()
                .Build();

        }

        public void OnDestroy(ref SystemState state) { }

        public void OnUpdate(ref SystemState state)
        {
            if (!Space4XDebugSettings.TryConsumeRenderSnapshotRequest())
                return;

            int keyCount = _renderKeyQuery.CalculateEntityCount();
            int meshCount = _materialMeshQuery.CalculateEntityCount();

            UnityEngine.Debug.Log($"[Space4X RenderDebug] keys:{keyCount} meshEntities:{meshCount}");
        }
    }
}
#endif







