using PureDOTS.Runtime.Aggregate;
using PureDOTS.Runtime.Comms;
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

            // Sort leaders for deterministic processing order
            leaders.Sort(new EntityComparer());

            // Single ECB for entire update to consolidate sync points
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            var defaults = formationConfig.StrikeDefaults;
            for (int i = 0; i < leaders.Length; i++)
            {
                var leader = leaders[i];
                if (!state.EntityManager.Exists(leader))
                {
                    continue;
                }

                EnsureGroupScaffold(leader, wingDecisionConfig.MaxWingSize, defaults, ref state);
                SyncGroupMembers(ref state, leader, leaderMembers, timeState.Tick, ref ecb);
            }

            CleanupOrphanedGroups(leaderSet, ref state, ref ecb);

            // Single playback point for entire update
            ecb.Playback(state.EntityManager);
            ecb.Dispose();

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
            // GroupTag is now enableable - add if missing, enable if disabled
            if (!state.EntityManager.HasComponent<GroupTag>(leader))
            {
                state.EntityManager.AddComponent<GroupTag>(leader);
            }
            else
            {
                // Re-enable if it was disabled (orphaned group being reactivated)
                state.EntityManager.SetComponentEnabled<GroupTag>(leader, true);
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

            if (!state.EntityManager.HasComponent<GroupFormationSpread>(leader))
            {
                state.EntityManager.AddComponentData(leader, new GroupFormationSpread
                {
                    CohesionNormalized = 0f
                });
            }

            if (!state.EntityManager.HasComponent<SquadCohesionProfile>(leader))
            {
                state.EntityManager.AddComponentData(leader, SquadCohesionProfile.Default);
            }

            if (!state.EntityManager.HasComponent<SquadCohesionState>(leader))
            {
                state.EntityManager.AddComponentData(leader, new SquadCohesionState
                {
                    NormalizedCohesion = 0f,
                    Flags = 0,
                    LastUpdateTick = 0,
                    LastBroadcastTick = 0,
                    LastTelemetryTick = 0
                });
            }

            if (!state.EntityManager.HasComponent<GroupStanceState>(leader))
            {
                state.EntityManager.AddComponentData(leader, new GroupStanceState
                {
                    Stance = GroupStance.Hold,
                    PrimaryTarget = Entity.Null,
                    Aggression = 0f,
                    Discipline = 0.5f
                });
            }

            if (!state.EntityManager.HasComponent<EngagementThreatSummary>(leader))
            {
                state.EntityManager.AddComponentData(leader, new EngagementThreatSummary
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

            if (!state.EntityManager.HasComponent<EngagementIntent>(leader))
            {
                state.EntityManager.AddComponentData(leader, new EngagementIntent
                {
                    Kind = EngagementIntentKind.None,
                    PrimaryTarget = Entity.Null,
                    AdvantageRatio = 1f,
                    ThreatPressure = 0f,
                    LastUpdateTick = 0
                });
            }

            if (!state.EntityManager.HasComponent<EngagementPlannerState>(leader))
            {
                state.EntityManager.AddComponentData(leader, new EngagementPlannerState
                {
                    LastIntentTick = 0,
                    LastTacticTick = 0,
                    LastTargetingTick = 0
                });
            }

            if (!state.EntityManager.HasBuffer<CommsOutboxEntry>(leader))
            {
                state.EntityManager.AddBuffer<CommsOutboxEntry>(leader);
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

        private void CleanupOrphanedGroups(NativeParallelHashMap<Entity, byte> leaderSet, ref SystemState state, ref EntityCommandBuffer ecb)
        {
            foreach (var (metaRef, groupEntity) in SystemAPI.Query<RefRO<GroupMeta>>()
                         .WithAll<GroupTag>()
                         .WithEntityAccess())
            {
                var meta = metaRef.ValueRO;
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

                if (state.EntityManager.HasComponent<GroupFormationSpread>(groupEntity))
                {
                    state.EntityManager.RemoveComponent<GroupFormationSpread>(groupEntity);
                }

                if (state.EntityManager.HasComponent<SquadCohesionProfile>(groupEntity))
                {
                    state.EntityManager.RemoveComponent<SquadCohesionProfile>(groupEntity);
                }

                if (state.EntityManager.HasComponent<SquadCohesionState>(groupEntity))
                {
                    state.EntityManager.RemoveComponent<SquadCohesionState>(groupEntity);
                }

                if (state.EntityManager.HasComponent<GroupStanceState>(groupEntity))
                {
                    state.EntityManager.RemoveComponent<GroupStanceState>(groupEntity);
                }

                if (state.EntityManager.HasComponent<EngagementThreatSummary>(groupEntity))
                {
                    state.EntityManager.RemoveComponent<EngagementThreatSummary>(groupEntity);
                }

                if (state.EntityManager.HasComponent<EngagementIntent>(groupEntity))
                {
                    state.EntityManager.RemoveComponent<EngagementIntent>(groupEntity);
                }

                if (state.EntityManager.HasComponent<EngagementPlannerState>(groupEntity))
                {
                    state.EntityManager.RemoveComponent<EngagementPlannerState>(groupEntity);
                }

                // Clear buffers instead of removing to avoid archetype changes
                if (state.EntityManager.HasBuffer<CommsOutboxEntry>(groupEntity))
                {
                    var commsBuffer = state.EntityManager.GetBuffer<CommsOutboxEntry>(groupEntity);
                    commsBuffer.Clear();
                }

                if (state.EntityManager.HasBuffer<GroupMember>(groupEntity))
                {
                    var memberBuffer = state.EntityManager.GetBuffer<GroupMember>(groupEntity);
                    memberBuffer.Clear();
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

                // Disable GroupTag instead of removing to avoid archetype churn
                if (state.EntityManager.HasComponent<GroupTag>(groupEntity))
                {
                    state.EntityManager.SetComponentEnabled<GroupTag>(groupEntity, false);
                }

                // Note: GroupMeta removal kept for now (not frequently toggled)
                // Consider making enableable if group creation/disbanding becomes frequent
                if (state.EntityManager.HasComponent<GroupMeta>(groupEntity))
                {
                    state.EntityManager.RemoveComponent<GroupMeta>(groupEntity);
                }
            }
        }

        private void SyncGroupMembers(
            ref SystemState state,
            Entity leader,
            NativeParallelMultiHashMap<Entity, Entity> leaderMembers,
            uint tick,
            ref EntityCommandBuffer ecb)
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
            AddMember(ref state, ref ecb, membersBuffer, leader, leader, GroupRole.Leader, tick);

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

                    AddMember(ref state, ref ecb, membersBuffer, leader, member, GroupRole.Member, tick);
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

        /// <summary>
        /// Comparer for deterministic entity ordering (by Index, then Version).
        /// </summary>
        private struct EntityComparer : System.Collections.Generic.IComparer<Entity>
        {
            public int Compare(Entity x, Entity y)
            {
                int indexCompare = x.Index.CompareTo(y.Index);
                if (indexCompare != 0)
                    return indexCompare;
                return x.Version.CompareTo(y.Version);
            }
        }

        private void AddMember(
            ref SystemState state,
            ref EntityCommandBuffer ecb,
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
                ecb.AddComponent(member, new GroupMembership
                {
                    Group = Entity.Null,
                    Role = 0
                });
            }

            ecb.SetComponent(member, new GroupMembership
            {
                Group = groupEntity,
                Role = (byte)role
            });
        }
    }
}
