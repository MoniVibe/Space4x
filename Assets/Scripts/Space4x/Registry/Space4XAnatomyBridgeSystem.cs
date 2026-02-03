using PureDOTS.Runtime.Anatomy;
using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Space4X.Registry
{
    /// <summary>
    /// Assigns a default anatomy to race-bearing entities when none is specified.
    /// Keeps anatomy data-driven while remaining flexible per race/variant later.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4XAnatomyBridgeSystem : ISystem
    {
        private static readonly FixedString64Bytes DefaultAnatomyId = new FixedString64Bytes("humanoid");

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RaceId>();
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            using var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (_, entity) in SystemAPI.Query<RefRO<RaceId>>()
                         .WithNone<AnatomyId>()
                         .WithEntityAccess())
            {
                ecb.AddComponent(entity, new AnatomyId { Value = DefaultAnatomyId });
            }

            ecb.Playback(state.EntityManager);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
