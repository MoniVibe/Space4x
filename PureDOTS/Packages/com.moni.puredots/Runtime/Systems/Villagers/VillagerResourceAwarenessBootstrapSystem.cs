using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Villagers
{
    /// <summary>
    /// Ensures villagers have resource awareness state and a config singleton exists.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct VillagerResourceAwarenessBootstrapSystem : ISystem
    {
        private EntityQuery _missingAwareness;
        private EntityQuery _missingNeed;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<VillagerAIState>();
            _missingAwareness = SystemAPI.QueryBuilder()
                .WithAll<VillagerAIState>()
                .WithNone<VillagerResourceAwareness>()
                .Build();
            _missingNeed = SystemAPI.QueryBuilder()
                .WithAll<VillagerAIState>()
                .WithNone<VillagerResourceNeed>()
                .Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<VillagerResourceAwarenessConfig>())
            {
                var configEntity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(configEntity, VillagerResourceAwarenessConfig.Default);
            }

            if (_missingAwareness.IsEmptyIgnoreFilter && _missingNeed.IsEmptyIgnoreFilter)
            {
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            if (!_missingAwareness.IsEmptyIgnoreFilter)
            {
                ecb.AddComponent(_missingAwareness, new VillagerResourceAwareness
                {
                    KnownNode = Entity.Null,
                    ResourceTypeIndex = ushort.MaxValue,
                    Confidence = 0f,
                    LastSeenTick = 0u,
                    KnownStorehouse = Entity.Null
                });
            }

            if (!_missingNeed.IsEmptyIgnoreFilter)
            {
                ecb.AddComponent(_missingNeed, new VillagerResourceNeed
                {
                    ResourceTypeIndex = ushort.MaxValue
                });
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
