using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using Space4X.Rendering;

namespace Space4X.Rendering.Systems
{
    /// <summary>
    /// Assigns RenderKey + RenderFlags to Space4X renderable entities that are missing them.
    /// Uses a local ECB to avoid structural changes during iteration.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.Default)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XAssignRenderKeySystem : ISystem
    {
        private EntityQuery _assignQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _assignQuery = SystemAPI.QueryBuilder()
                .WithAll<LocalTransform>()
                .WithNone<RenderKey>()
                .Build();

            state.RequireForUpdate(_assignQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (_assignQuery.IsEmpty)
                return;

            using var entities = _assignQuery.ToEntityArray(Allocator.TempJob);
            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            foreach (var entity in entities)
            {
                var key = new RenderKey
                {
                    ArchetypeId = Space4XRenderKeys.Miner, // adjust per-entity if needed
                    LOD = 0
                };

                var flags = new RenderFlags
                {
                    Visible = 1,
                    ShadowCaster = 1,
                    HighlightMask = 0
                };

                ecb.AddComponent(entity, key);
                ecb.AddComponent(entity, flags);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) { }
    }
}


