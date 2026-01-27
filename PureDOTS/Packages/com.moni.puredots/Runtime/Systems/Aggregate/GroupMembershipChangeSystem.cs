using PureDOTS.Runtime.Aggregate;
using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Aggregate
{
    /// <summary>
    /// Detects when group composition changes and marks aggregates as dirty for stats recalculation.
    /// Watches GroupMembership component add/remove/modify events.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct GroupMembershipChangeSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState))
            {
                return;
            }

            // Skip if paused or rewinding
            if (timeState.IsPaused || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

            // Detect changes: entities that gained or lost GroupMembership
            // We check for entities that have GroupMembership but their group doesn't have AggregateStatsDirtyTag
            foreach (var (membership, entity) in SystemAPI.Query<RefRO<GroupMembership>>().WithEntityAccess())
            {
                var groupEntity = membership.ValueRO.Group;
                if (groupEntity == Entity.Null)
                    continue;

                // Check if group exists and has AggregateIdentity (is a valid aggregate)
                if (!SystemAPI.Exists(groupEntity) || !SystemAPI.HasComponent<AggregateIdentity>(groupEntity))
                    continue;

                // Mark group as dirty if not already marked
                if (!SystemAPI.HasComponent<AggregateStatsDirtyTag>(groupEntity))
                {
                    ecb.AddComponent<AggregateStatsDirtyTag>(groupEntity);
                }
            }

            // Also check for entities that lost GroupMembership (would need change tracking)
            // For now, we rely on the above check which runs every frame
            // In Tier-2, we can add proper change tracking with ComponentSystemBase
        }
    }
}
























