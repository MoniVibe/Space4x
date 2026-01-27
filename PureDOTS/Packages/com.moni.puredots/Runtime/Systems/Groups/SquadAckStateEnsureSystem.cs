using PureDOTS.Runtime.Aggregate;
using PureDOTS.Runtime.Groups;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Groups
{
    /// <summary>
    /// Ensures entities that can participate in squad tactics keep lightweight ack state.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct SquadAckStateEnsureSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GroupMembership>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (_, entity) in SystemAPI.Query<RefRO<GroupMembership>>()
                         .WithNone<SquadAckState>()
                         .WithEntityAccess())
            {
                ecb.AddComponent(entity, new SquadAckState { LastAckTick = 0 });
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}


