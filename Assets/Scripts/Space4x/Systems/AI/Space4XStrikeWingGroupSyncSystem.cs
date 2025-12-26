using PureDOTS.Runtime.Aggregate;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Groups;
using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Systems.AI
{
    [BurstCompile]
    [UpdateInGroup(typeof(PureDOTS.Systems.GroupDecisionSystemGroup), OrderFirst = true)]
    public partial struct Space4XStrikeWingGroupSyncSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StrikeCraftProfile>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var wingDecisionConfig = StrikeCraftWingDecisionConfig.Default;
            if (SystemAPI.TryGetSingleton<StrikeCraftWingDecisionConfig>(out var wingDecisionSingleton))
            {
                wingDecisionConfig = wingDecisionSingleton;
            }

            var formationConfig = WingFormationConfig.Default;
            if (SystemAPI.TryGetSingleton<WingFormationConfig>(out var formationSingleton))
            {
                formationConfig = formationSingleton;
            }

            var craftQuery = SystemAPI.QueryBuilder().WithAll<StrikeCraftProfile>().Build();
            var craftCount = craftQuery.CalculateEntityCount();
            var capacity = math.max(1, craftCount);
            var leaderMembers = new NativeParallelMultiHashMap<Entity, Entity>(capacity, Allocator.Temp);
            var leaders = new NativeList<Entity>(capacity, Allocator.Temp);
            var leaderSet = new NativeParallelHashMap<Entity, byte>(capacity, Allocator.Temp);

            foreach (var (profile, entity) in SystemAPI.Query<RefRO<StrikeCraftProfile>>().WithEntityAccess())
            {
                var leader = profile.ValueRO.WingLeader;
                if (leader == Entity.Null || !state.EntityManager.Exists(leader))
                {
                    leader = entity;
                }

                leaderMembers.Add(leader, entity);
                if (leaderSet.TryAdd(leader, 1))
                {
                    leaders.Add(leader);
                }
            }

            var defaults = formationConfig.StrikeDefaults;
            for (int i = 0; i < leaders.Length; i++)
            {
                var leader = leaders[i];
                if (!state.EntityManager.Exists(leader))
                {
                    continue;
                }

                EnsureGroupScaffold(leader, wingDecisionConfig.MaxWingSize, defaults, ref state);
                SyncGroupMembers(ref state, leader, leaderMembers, timeState.Tick);
            }

            CleanupOrphanedGroups(leaderSet, ref state);

            leaders.Dispose();
            leaderMembers.Dispose();
            leaderSet.Dispose();
        }

        private void EnsureGroupScaffold(
            Entity leader,
            byte maxSize,
            in WingFormationDefaults defaults,
            ref SystemState state)
        {
            if (!state.EntityManager.HasComponent<GroupTag>(leader))
            {
                state.EntityManager.AddComponent<GroupTag>(leader);
            }

            if (!state.EntityManager.HasComponent<GroupMeta>(leader))
            {
                state.EntityManager.AddComponentData(leader, new GroupMeta
                {
                    Kind = GroupKind.StrikeWing,
                    Leader = leader,
                    MaxSize = maxSize
                });
            }
            else
            {
                var meta = state.EntityManager.GetComponentData<GroupMeta>(leader);
                meta.Kind = GroupKind.StrikeWing;
                meta.Leader = leader;
                meta.MaxSize = maxSize;
                state.EntityManager.SetComponentData(leader, meta);
            }

            if (!state.EntityManager.HasComponent<GroupFormation>(leader))
            {
                state.EntityManager.AddComponentData(leader, new GroupFormation
                {
                    Type = PureDOTS.Runtime.Groups.FormationType.Line,
                    Spacing = defaults.LooseSpacing,
                    Cohesion = defaults.LooseCohesion,
                    FacingWeight = defaults.LooseFacingWeight
                });
            }

            if (!state.EntityManager.HasBuffer<GroupMember>(leader))
            {
                state.EntityManager.AddBuffer<GroupMember>(leader);
            }

            if (!state.EntityManager.HasComponent<WingFormationState>(leader))
            {
                state.EntityManager.AddComponentData(leader, new WingFormationState
                {
                    LastDecisionTick = 0,
                    LastTacticKind = 0,
                    SplitCount = 1,
                    LastAckRatio = 0f,
                    LastMemberCount = 0,
                    LastAcked = 0,
                    LastMemberHash = 0u
                });
            }

            if (!state.EntityManager.HasComponent<WingGroupSyncState>(leader))
            {
                state.EntityManager.AddComponentData(leader, new WingGroupSyncState
                {
                    LastMemberCount = 0,
                    LastMemberHash = 0u
                });
            }

            if (!state.EntityManager.HasBuffer<WingFormationAnchorRef>(leader))
            {
                state.EntityManager.AddBuffer<WingFormationAnchorRef>(leader);
            }

            if (!state.EntityManager.HasComponent<LocalTransform>(leader))
            {
                state.EntityManager.AddComponentData(leader, LocalTransform.Identity);
            }
        }

        private void CleanupOrphanedGroups(NativeParallelHashMap<Entity, byte> leaderSet, ref SystemState state)
        {
            var groupQuery = SystemAPI.QueryBuilder().WithAll<GroupTag, GroupMeta>().Build();
            var groups = groupQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < groups.Length; i++)
            {
                var groupEntity = groups[i];
                if (!state.EntityManager.HasComponent<GroupMeta>(groupEntity))
                {
                    continue;
                }

                var meta = state.EntityManager.GetComponentData<GroupMeta>(groupEntity);
                if (meta.Kind != GroupKind.StrikeWing)
                {
                    continue;
                }

                if (leaderSet.TryGetValue(groupEntity, out _))
                {
                    continue;
                }

                if (state.EntityManager.HasBuffer<WingFormationAnchorRef>(groupEntity))
                {
                    var anchors = state.EntityManager.GetBuffer<WingFormationAnchorRef>(groupEntity);
                    for (int a = 0; a < anchors.Length; a++)
                    {
                        var anchorEntity = anchors[a].Anchor;
                        if (anchorEntity != Entity.Null && state.EntityManager.Exists(anchorEntity))
                        {
                            state.EntityManager.DestroyEntity(anchorEntity);
                        }
                    }

                    state.EntityManager.RemoveComponent<WingFormationAnchorRef>(groupEntity);
                }

                if (state.EntityManager.HasComponent<WingFormationState>(groupEntity))
                {
                    state.EntityManager.RemoveComponent<WingFormationState>(groupEntity);
                }

                if (state.EntityManager.HasComponent<WingGroupSyncState>(groupEntity))
                {
                    state.EntityManager.RemoveComponent<WingGroupSyncState>(groupEntity);
                }

                if (state.EntityManager.HasComponent<GroupFormation>(groupEntity))
                {
                    state.EntityManager.RemoveComponent<GroupFormation>(groupEntity);
                }

                if (state.EntityManager.HasBuffer<GroupMember>(groupEntity))
                {
                    state.EntityManager.RemoveComponent<GroupMember>(groupEntity);
                }

                if (state.EntityManager.HasComponent<SquadTacticOrder>(groupEntity))
                {
                    state.EntityManager.RemoveComponent<SquadTacticOrder>(groupEntity);
                }

                if (state.EntityManager.HasComponent<PureDOTS.Runtime.Formation.FormationState>(groupEntity))
                {
                    state.EntityManager.RemoveComponent<PureDOTS.Runtime.Formation.FormationState>(groupEntity);
                }

                if (state.EntityManager.HasBuffer<PureDOTS.Runtime.Formation.FormationSlot>(groupEntity))
                {
                    state.EntityManager.RemoveComponent<PureDOTS.Runtime.Formation.FormationSlot>(groupEntity);
                }

                if (state.EntityManager.HasComponent<GroupTag>(groupEntity))
                {
                    state.EntityManager.RemoveComponent<GroupTag>(groupEntity);
                }

                if (state.EntityManager.HasComponent<GroupMeta>(groupEntity))
                {
                    state.EntityManager.RemoveComponent<GroupMeta>(groupEntity);
                }
            }

            groups.Dispose();
        }

        private void SyncGroupMembers(
            ref SystemState state,
            Entity leader,
            NativeParallelMultiHashMap<Entity, Entity> leaderMembers,
            uint tick)
        {
            var syncState = state.EntityManager.GetComponentData<WingGroupSyncState>(leader);
            var memberCount = 1;
            var memberHash = HashEntity(leader);

            if (leaderMembers.TryGetFirstValue(leader, out var member, out var iterator))
            {
                do
                {
                    if (member == leader)
                    {
                        continue;
                    }

                    if (!state.EntityManager.Exists(member))
                    {
                        continue;
                    }

                    memberCount++;
                    memberHash ^= HashEntity(member);
                } while (leaderMembers.TryGetNextValue(out member, ref iterator));
            }

            var clampedCount = (ushort)math.min(memberCount, ushort.MaxValue);
            var needsRebuild = syncState.LastMemberCount != clampedCount || syncState.LastMemberHash != memberHash;
            if (!needsRebuild)
            {
                return;
            }

            var membersBuffer = state.EntityManager.GetBuffer<GroupMember>(leader);
            membersBuffer.Clear();
            AddMember(ref state, membersBuffer, leader, leader, GroupRole.Leader, tick);

            if (leaderMembers.TryGetFirstValue(leader, out member, out iterator))
            {
                do
                {
                    if (member == leader)
                    {
                        continue;
                    }

                    if (!state.EntityManager.Exists(member))
                    {
                        continue;
                    }

                    AddMember(ref state, membersBuffer, leader, member, GroupRole.Member, tick);
                } while (leaderMembers.TryGetNextValue(out member, ref iterator));
            }

            syncState.LastMemberCount = clampedCount;
            syncState.LastMemberHash = memberHash;
            state.EntityManager.SetComponentData(leader, syncState);
        }

        private static uint HashEntity(Entity entity)
        {
            return math.hash(new int2(entity.Index, entity.Version));
        }

        private void AddMember(
            ref SystemState state,
            DynamicBuffer<GroupMember> membersBuffer,
            Entity groupEntity,
            Entity member,
            GroupRole role,
            uint tick)
        {
            if (!state.EntityManager.Exists(member))
            {
                return;
            }

            membersBuffer.Add(new GroupMember
            {
                MemberEntity = member,
                Weight = 1f,
                Role = role,
                JoinedTick = tick,
                Flags = GroupMemberFlags.Active
            });

            if (!state.EntityManager.HasComponent<GroupMembership>(member))
            {
                state.EntityManager.AddComponentData(member, new GroupMembership
                {
                    Group = Entity.Null,
                    Role = 0
                });
            }

            var membership = state.EntityManager.GetComponentData<GroupMembership>(member);
            membership.Group = groupEntity;
            membership.Role = (byte)role;
            state.EntityManager.SetComponentData(member, membership);
        }
    }
}
