using PureDOTS.Runtime.Aggregate;
using PureDOTS.Runtime.Comms;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Groups;
using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Systems.AI
{
    [UpdateInGroup(typeof(PureDOTS.Systems.GroupDecisionSystemGroup), OrderFirst = true)]
    public partial struct Space4XStrikeWingGroupSyncSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StrikeCraftProfile>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

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

            var em = state.EntityManager;

            var craftQuery = SystemAPI.QueryBuilder().WithAll<StrikeCraftProfile>().Build();
            var craftCount = craftQuery.CalculateEntityCount();
            var capacity = math.max(1, craftCount);
            var leaderMembers = new NativeParallelMultiHashMap<Entity, Entity>(capacity, Allocator.Temp);
            var leaders = new NativeList<Entity>(capacity, Allocator.Temp);
            var leaderSet = new NativeParallelHashMap<Entity, byte>(capacity, Allocator.Temp);

            foreach (var (profile, entity) in SystemAPI.Query<RefRO<StrikeCraftProfile>>().WithEntityAccess())
            {
                var leader = profile.ValueRO.WingLeader;
                if (leader == Entity.Null || !em.Exists(leader))
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
                if (!em.Exists(leader))
                {
                    continue;
                }

                EnsureGroupScaffold(em, leader, wingDecisionConfig.MaxWingSize, defaults);
                SyncGroupMembers(ref state, leader, leaderMembers, timeState.Tick);
            }

            CleanupOrphanedGroups(leaderSet, ref state);

            leaders.Dispose();
            leaderMembers.Dispose();
            leaderSet.Dispose();
        }

        private void EnsureGroupScaffold(
            EntityManager em,
            Entity leader,
            byte maxSize,
            in WingFormationDefaults defaults)
        {
            if (!IsValidEntity(em, leader))
            {
                return;
            }

            if (!em.HasComponent<GroupTag>(leader))
            {
                em.AddComponent<GroupTag>(leader);
            }

            if (!em.HasComponent<GroupMeta>(leader))
            {
                em.AddComponentData(leader, new GroupMeta
                {
                    Kind = GroupKind.StrikeWing,
                    Leader = leader,
                    MaxSize = maxSize
                });
            }
            else
            {
                var meta = em.GetComponentData<GroupMeta>(leader);
                meta.Kind = GroupKind.StrikeWing;
                meta.Leader = leader;
                meta.MaxSize = maxSize;
                em.SetComponentData(leader, meta);
            }

            if (!em.HasComponent<GroupFormation>(leader))
            {
                em.AddComponentData(leader, new GroupFormation
                {
                    Type = PureDOTS.Runtime.Groups.FormationType.Line,
                    Spacing = defaults.LooseSpacing,
                    Cohesion = defaults.LooseCohesion,
                    FacingWeight = defaults.LooseFacingWeight
                });
            }

            if (!em.HasComponent<GroupFormationSpread>(leader))
            {
                em.AddComponentData(leader, new GroupFormationSpread
                {
                    CohesionNormalized = 0f
                });
            }

            if (!em.HasComponent<SquadCohesionProfile>(leader))
            {
                em.AddComponentData(leader, SquadCohesionProfile.Default);
            }

            if (!em.HasComponent<SquadCohesionState>(leader))
            {
                em.AddComponentData(leader, new SquadCohesionState
                {
                    NormalizedCohesion = 0f,
                    Flags = 0,
                    LastUpdateTick = 0,
                    LastBroadcastTick = 0,
                    LastTelemetryTick = 0
                });
            }

            if (!em.HasComponent<GroupStanceState>(leader))
            {
                em.AddComponentData(leader, new GroupStanceState
                {
                    Stance = GroupStance.Hold,
                    PrimaryTarget = Entity.Null,
                    Aggression = 0f,
                    Discipline = 0.5f
                });
            }

            if (!em.HasComponent<EngagementThreatSummary>(leader))
            {
                em.AddComponentData(leader, new EngagementThreatSummary
                {
                    PrimaryThreat = Entity.Null,
                    PrimaryThreatDistance = 0f,
                    PrimaryThreatHullRatio = 0f,
                    FriendlyAverageHull = 0f,
                    ThreatAverageHull = 0f,
                    FriendlyStrength = 0f,
                    ThreatStrength = 0f,
                    ThreatPressure = 0f,
                    AdvantageRatio = 1f,
                    FriendlyCount = 0,
                    ThreatCount = 0,
                    EscapeProbability = 1f,
                    LastUpdateTick = 0
                });
            }

            if (!em.HasComponent<EngagementIntent>(leader))
            {
                em.AddComponentData(leader, new EngagementIntent
                {
                    Kind = EngagementIntentKind.None,
                    PrimaryTarget = Entity.Null,
                    AdvantageRatio = 1f,
                    ThreatPressure = 0f,
                    LastUpdateTick = 0
                });
            }

            if (!em.HasComponent<EngagementPlannerState>(leader))
            {
                em.AddComponentData(leader, new EngagementPlannerState
                {
                    LastIntentTick = 0,
                    LastTacticTick = 0,
                    LastTargetingTick = 0
                });
            }

            if (!em.HasBuffer<CommsOutboxEntry>(leader))
            {
                em.AddBuffer<CommsOutboxEntry>(leader);
            }

            if (!em.HasBuffer<GroupMember>(leader))
            {
                em.AddBuffer<GroupMember>(leader);
            }

            if (!em.HasComponent<WingFormationState>(leader))
            {
                em.AddComponentData(leader, new WingFormationState
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

            if (!em.HasComponent<WingGroupSyncState>(leader))
            {
                em.AddComponentData(leader, new WingGroupSyncState
                {
                    LastMemberCount = 0,
                    LastMemberHash = 0u
                });
            }

            if (!em.HasBuffer<WingFormationAnchorRef>(leader))
            {
                em.AddBuffer<WingFormationAnchorRef>(leader);
            }

            if (!em.HasComponent<LocalTransform>(leader))
            {
                em.AddComponentData(leader, LocalTransform.Identity);
            }
        }

        private void CleanupOrphanedGroups(NativeParallelHashMap<Entity, byte> leaderSet, ref SystemState state)
        {
            var em = state.EntityManager;
            var groupQuery = SystemAPI.QueryBuilder().WithAll<GroupTag, GroupMeta>().Build();
            var groups = groupQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < groups.Length; i++)
            {
                var groupEntity = groups[i];
                if (groupEntity == Entity.Null || !em.Exists(groupEntity))
                {
                    continue;
                }

                if (!em.HasComponent<GroupMeta>(groupEntity))
                {
                    continue;
                }

                var meta = em.GetComponentData<GroupMeta>(groupEntity);
                if (meta.Kind != GroupKind.StrikeWing)
                {
                    continue;
                }

                if (leaderSet.TryGetValue(groupEntity, out _))
                {
                    continue;
                }

                if (em.HasBuffer<WingFormationAnchorRef>(groupEntity))
                {
                    var anchors = em.GetBuffer<WingFormationAnchorRef>(groupEntity);
                    for (int a = 0; a < anchors.Length; a++)
                    {
                        var anchorEntity = anchors[a].Anchor;
                        if (anchorEntity != Entity.Null && em.Exists(anchorEntity))
                        {
                            em.DestroyEntity(anchorEntity);
                        }
                    }

                    em.RemoveComponent<WingFormationAnchorRef>(groupEntity);
                }

                if (em.HasComponent<WingFormationState>(groupEntity))
                {
                    em.RemoveComponent<WingFormationState>(groupEntity);
                }

                if (em.HasComponent<WingGroupSyncState>(groupEntity))
                {
                    em.RemoveComponent<WingGroupSyncState>(groupEntity);
                }

                if (em.HasComponent<GroupFormation>(groupEntity))
                {
                    em.RemoveComponent<GroupFormation>(groupEntity);
                }

                if (em.HasComponent<GroupFormationSpread>(groupEntity))
                {
                    em.RemoveComponent<GroupFormationSpread>(groupEntity);
                }

                if (em.HasComponent<SquadCohesionProfile>(groupEntity))
                {
                    em.RemoveComponent<SquadCohesionProfile>(groupEntity);
                }

                if (em.HasComponent<SquadCohesionState>(groupEntity))
                {
                    em.RemoveComponent<SquadCohesionState>(groupEntity);
                }

                if (em.HasComponent<GroupStanceState>(groupEntity))
                {
                    em.RemoveComponent<GroupStanceState>(groupEntity);
                }

                if (em.HasComponent<EngagementThreatSummary>(groupEntity))
                {
                    em.RemoveComponent<EngagementThreatSummary>(groupEntity);
                }

                if (em.HasComponent<EngagementIntent>(groupEntity))
                {
                    em.RemoveComponent<EngagementIntent>(groupEntity);
                }

                if (em.HasComponent<EngagementPlannerState>(groupEntity))
                {
                    em.RemoveComponent<EngagementPlannerState>(groupEntity);
                }

                if (em.HasBuffer<CommsOutboxEntry>(groupEntity))
                {
                    em.RemoveComponent<CommsOutboxEntry>(groupEntity);
                }

                if (em.HasBuffer<GroupMember>(groupEntity))
                {
                    em.RemoveComponent<GroupMember>(groupEntity);
                }

                if (em.HasComponent<SquadTacticOrder>(groupEntity))
                {
                    em.RemoveComponent<SquadTacticOrder>(groupEntity);
                }

                if (em.HasComponent<PureDOTS.Runtime.Formation.FormationState>(groupEntity))
                {
                    em.RemoveComponent<PureDOTS.Runtime.Formation.FormationState>(groupEntity);
                }

                if (em.HasBuffer<PureDOTS.Runtime.Formation.FormationSlot>(groupEntity))
                {
                    em.RemoveComponent<PureDOTS.Runtime.Formation.FormationSlot>(groupEntity);
                }

                if (em.HasComponent<GroupTag>(groupEntity))
                {
                    em.RemoveComponent<GroupTag>(groupEntity);
                }

                if (em.HasComponent<GroupMeta>(groupEntity))
                {
                    em.RemoveComponent<GroupMeta>(groupEntity);
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
            var em = state.EntityManager;
            if (!IsValidEntity(em, leader))
            {
                return;
            }

            if (!em.HasComponent<WingGroupSyncState>(leader) || !em.HasBuffer<GroupMember>(leader))
            {
                return;
            }

            var syncState = em.GetComponentData<WingGroupSyncState>(leader);
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

                    if (!IsValidEntity(em, member))
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

            var stagedMembers = new NativeList<GroupMember>(memberCount, Allocator.Temp);
            stagedMembers.Add(new GroupMember
            {
                MemberEntity = leader,
                Weight = 1f,
                Role = GroupRole.Leader,
                JoinedTick = tick,
                Flags = GroupMemberFlags.Active
            });

            if (leaderMembers.TryGetFirstValue(leader, out member, out iterator))
            {
                do
                {
                    if (member == leader)
                    {
                        continue;
                    }

                    if (!IsValidEntity(em, member))
                    {
                        continue;
                    }

                    stagedMembers.Add(new GroupMember
                    {
                        MemberEntity = member,
                        Weight = 1f,
                        Role = GroupRole.Member,
                        JoinedTick = tick,
                        Flags = GroupMemberFlags.Active
                    });
                } while (leaderMembers.TryGetNextValue(out member, ref iterator));
            }

            for (int i = 0; i < stagedMembers.Length; i++)
            {
                var entry = stagedMembers[i];
                if (!em.HasComponent<GroupMembership>(entry.MemberEntity))
                {
                    em.AddComponentData(entry.MemberEntity, new GroupMembership
                    {
                        Group = Entity.Null,
                        Role = 0
                    });
                }

                em.SetComponentData(entry.MemberEntity, new GroupMembership
                {
                    Group = leader,
                    Role = (byte)entry.Role
                });
            }

            var membersBuffer = em.GetBuffer<GroupMember>(leader);
            membersBuffer.Clear();
            for (int i = 0; i < stagedMembers.Length; i++)
            {
                membersBuffer.Add(stagedMembers[i]);
            }
            stagedMembers.Dispose();

            syncState.LastMemberCount = clampedCount;
            syncState.LastMemberHash = memberHash;
            em.SetComponentData(leader, syncState);
        }

        private static uint HashEntity(Entity entity)
        {
            return math.hash(new int2(entity.Index, entity.Version));
        }

        private static bool IsValidEntity(EntityManager em, Entity entity)
        {
            return entity != Entity.Null && em.Exists(entity);
        }
    }
}
