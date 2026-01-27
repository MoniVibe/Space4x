using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace PureDOTS.Rendering
{
    /// <summary>
    /// Ensures every RenderKey entity carries a LocalTransform so rendering systems have a position.
    /// Adds an identity transform if missing.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct EnsureRenderTransformSystem : ISystem
    {
        private EntityQuery _missingTransformQuery;

        public void OnCreate(ref SystemState state)
        {
            _missingTransformQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<RenderKey>()
                },
                None = new[]
                {
                    ComponentType.ReadOnly<LocalTransform>()
                }
            });

            state.RequireForUpdate(_missingTransformQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_missingTransformQuery.IsEmptyIgnoreFilter)
                return;

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            using var entities = _missingTransformQuery.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                ecb.AddComponent(entities[i], LocalTransform.Identity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
