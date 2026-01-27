using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Groups;
using PureDOTS.Runtime.Morale;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Groups
{
    /// <summary>
    /// Processes group membership commands and maintains member buffers.
    /// Game-agnostic: works for any group type (bands, guilds, crews, etc.).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct GroupMembershipSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            
            if (timeState.IsPaused || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            // Get or create command queue
            Entity queueEntity;
            if (!SystemAPI.TryGetSingletonEntity<GroupCommandQueue>(out queueEntity))
            {
                queueEntity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponent<GroupCommandQueue>(queueEntity);
                state.EntityManager.AddBuffer<PendingGroupAddition>(queueEntity);
                state.EntityManager.AddBuffer<PendingGroupRemoval>(queueEntity);
            }

            var queue = SystemAPI.GetComponentRW<GroupCommandQueue>(queueEntity);
            var additions = state.EntityManager.GetBuffer<PendingGroupAddition>(queueEntity);
            var removals = state.EntityManager.GetBuffer<PendingGroupRemoval>(queueEntity);

            // Process removals first (to free slots)
            for (int i = removals.Length - 1; i >= 0; i--)
            {
                var removal = removals[i];
                
                if (!state.EntityManager.Exists(removal.GroupEntity) ||
                    !state.EntityManager.HasBuffer<GroupMember>(removal.GroupEntity))
                {
                    removals.RemoveAt(i);
                    continue;
                }

                var members = state.EntityManager.GetBuffer<GroupMember>(removal.GroupEntity);
                
                for (int j = members.Length - 1; j >= 0; j--)
                {
                    if (members[j].MemberEntity == removal.MemberEntity)
                    {
                        members.RemoveAt(j);
                        break;
                    }
                }

                // Update group identity if leader was removed
                if (state.EntityManager.HasComponent<GroupIdentity>(removal.GroupEntity))
                {
                    var identity = state.EntityManager.GetComponentData<GroupIdentity>(removal.GroupEntity);
                    if (identity.LeaderEntity == removal.MemberEntity)
                    {
                        // Find new leader (first lieutenant or highest weight member)
                        identity.LeaderEntity = FindNewLeader(members);
                        state.EntityManager.SetComponentData(removal.GroupEntity, identity);
                    }
                }

                removals.RemoveAt(i);
            }

            // Process additions
            for (int i = additions.Length - 1; i >= 0; i--)
            {
                var addition = additions[i];
                
                if (!state.EntityManager.Exists(addition.GroupEntity) ||
                    !state.EntityManager.HasBuffer<GroupMember>(addition.GroupEntity))
                {
                    additions.RemoveAt(i);
                    continue;
                }

                var members = state.EntityManager.GetBuffer<GroupMember>(addition.GroupEntity);

                // Check capacity
                if (state.EntityManager.HasComponent<GroupConfig>(addition.GroupEntity))
                {
                    var config = state.EntityManager.GetComponentData<GroupConfig>(addition.GroupEntity);
                    if (members.Length >= config.MaxMembers)
                    {
                        additions.RemoveAt(i);
                        continue; // Group is full
                    }
                }

                // Check if already a member
                var alreadyMember = false;
                for (int j = 0; j < members.Length; j++)
                {
                    if (members[j].MemberEntity == addition.MemberEntity)
                    {
                        alreadyMember = true;
                        break;
                    }
                }

                if (!alreadyMember)
                {
                    members.Add(new GroupMember
                    {
                        MemberEntity = addition.MemberEntity,
                        Weight = addition.Weight,
                        Role = addition.Role,
                        JoinedTick = timeState.Tick,
                        Flags = GroupMemberFlags.Active
                    });

                    // Set as leader if role is Leader and no current leader
                    if (addition.Role == GroupRole.Leader &&
                        state.EntityManager.HasComponent<GroupIdentity>(addition.GroupEntity))
                    {
                        var identity = state.EntityManager.GetComponentData<GroupIdentity>(addition.GroupEntity);
                        if (identity.LeaderEntity == Entity.Null)
                        {
                            identity.LeaderEntity = addition.MemberEntity;
                            state.EntityManager.SetComponentData(addition.GroupEntity, identity);
                        }
                    }
                }

                additions.RemoveAt(i);
            }

            queue.ValueRW.PendingAdditions = additions.Length;
            queue.ValueRW.PendingRemovals = removals.Length;
            queue.ValueRW.LastProcessTick = timeState.Tick;
        }

        private static Entity FindNewLeader(DynamicBuffer<GroupMember> members)
        {
            // First try to find a lieutenant
            for (int i = 0; i < members.Length; i++)
            {
                if (members[i].Role == GroupRole.Lieutenant &&
                    (members[i].Flags & GroupMemberFlags.Active) != 0)
                {
                    return members[i].MemberEntity;
                }
            }

            // Fall back to highest weight active member
            var bestWeight = -1f;
            var bestMember = Entity.Null;
            
            for (int i = 0; i < members.Length; i++)
            {
                if ((members[i].Flags & GroupMemberFlags.Active) != 0 &&
                    members[i].Weight > bestWeight)
                {
                    bestWeight = members[i].Weight;
                    bestMember = members[i].MemberEntity;
                }
            }

            return bestMember;
        }
    }

    /// <summary>
    /// Computes aggregate statistics for groups.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(GroupMembershipSystem))]
    public partial struct GroupAggregationSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<EntityMorale> _moraleLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _moraleLookup = state.GetComponentLookup<PureDOTS.Runtime.Morale.EntityMorale>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            
            if (timeState.IsPaused || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            _transformLookup.Update(ref state);
            _moraleLookup.Update(ref state);

            foreach (var (config, aggregate, members, entity) in 
                SystemAPI.Query<RefRO<GroupConfig>, RefRW<GroupAggregate>, DynamicBuffer<GroupMember>>()
                .WithEntityAccess())
            {
                // Check aggregation interval
                var ticksSinceCompute = timeState.Tick - aggregate.ValueRO.LastComputeTick;
                if (ticksSinceCompute < config.ValueRO.AggregationInterval)
                {
                    continue;
                }

                // Compute aggregates
                var memberCount = 0;
                var centerOfMass = float3.zero;
                var totalWeight = 0f;
                var moraleSum = 0f;

                for (int i = 0; i < members.Length; i++)
                {
                    var member = members[i];
                    if ((member.Flags & GroupMemberFlags.Active) == 0)
                    {
                        continue;
                    }

                    memberCount++;
                    totalWeight += member.Weight;

                    if (_transformLookup.HasComponent(member.MemberEntity))
                    {
                        var pos = _transformLookup[member.MemberEntity].Position;
                        centerOfMass += pos * member.Weight;
                    }

                    if (_moraleLookup.HasComponent(member.MemberEntity))
                    {
                        moraleSum += _moraleLookup[member.MemberEntity].CurrentMorale;
                    }
                }

                if (totalWeight > 0.001f)
                {
                    centerOfMass /= totalWeight;
                }

                // Calculate dispersion
                var dispersion = 0f;
                if (memberCount > 1)
                {
                    for (int i = 0; i < members.Length; i++)
                    {
                        var member = members[i];
                        if ((member.Flags & GroupMemberFlags.Active) == 0)
                        {
                            continue;
                        }

                        if (_transformLookup.HasComponent(member.MemberEntity))
                        {
                            var pos = _transformLookup[member.MemberEntity].Position;
                            dispersion += math.distance(pos, centerOfMass);
                        }
                    }
                    dispersion /= memberCount;
                }

                // Calculate cohesion (inverse of normalized dispersion)
                var maxExpectedDispersion = 50f; // Configurable
                var cohesion = 1f - math.saturate(dispersion / maxExpectedDispersion);

                aggregate.ValueRW.MemberCount = memberCount;
                aggregate.ValueRW.CenterOfMass = centerOfMass;
                aggregate.ValueRW.Dispersion = dispersion;
                aggregate.ValueRW.Cohesion = cohesion;
                aggregate.ValueRW.AverageMorale = memberCount > 0 ? moraleSum / memberCount : 0f;
                aggregate.ValueRW.LastComputeTick = timeState.Tick;

                // Note: AverageHealth, AverageMorale, TotalStrength would require
                // additional component lookups for health/morale/combat stats.
                // Left as exercise for game-specific extensions.
            }
        }
    }

    /// <summary>
    /// Helper for submitting group membership commands.
    /// </summary>
    public static class GroupMembershipHelpers
    {
        private static bool TryGetQueueEntity(ref SystemState state, out Entity queueEntity)
        {
            var query = state.GetEntityQuery(ComponentType.ReadOnly<GroupCommandQueue>());
            return query.TryGetSingletonEntity<GroupCommandQueue>(out queueEntity);
        }

        private static bool TryGetTimeState(ref SystemState state, out TimeState timeState)
        {
            var query = state.GetEntityQuery(ComponentType.ReadOnly<TimeState>());
            return query.TryGetSingleton(out timeState);
        }

        /// <summary>
        /// Queues a member addition.
        /// </summary>
        public static void AddMember(
            ref SystemState state,
            Entity groupEntity,
            Entity memberEntity,
            GroupRole role = GroupRole.Member,
            float weight = 1f)
        {
            if (!TryGetQueueEntity(ref state, out var queueEntity))
            {
                return;
            }

            if (!TryGetTimeState(ref state, out var timeState))
            {
                return;
            }

            var additions = state.EntityManager.GetBuffer<PendingGroupAddition>(queueEntity);
            
            additions.Add(new PendingGroupAddition
            {
                GroupEntity = groupEntity,
                MemberEntity = memberEntity,
                Role = role,
                Weight = weight,
                RequestTick = timeState.Tick
            });
        }

        /// <summary>
        /// Queues a member removal.
        /// </summary>
        public static void RemoveMember(
            ref SystemState state,
            Entity groupEntity,
            Entity memberEntity,
            RemovalReason reason = RemovalReason.Left)
        {
            if (!TryGetQueueEntity(ref state, out var queueEntity))
            {
                return;
            }

            if (!TryGetTimeState(ref state, out var timeState))
            {
                return;
            }

            var removals = state.EntityManager.GetBuffer<PendingGroupRemoval>(queueEntity);
            
            removals.Add(new PendingGroupRemoval
            {
                GroupEntity = groupEntity,
                MemberEntity = memberEntity,
                Reason = reason,
                RequestTick = timeState.Tick
            });
        }
    }
}

