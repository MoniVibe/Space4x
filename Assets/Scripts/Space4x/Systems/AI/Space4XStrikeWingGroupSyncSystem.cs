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
        private ComponentLookup<GroupTag> _groupTagLookup;
        private ComponentLookup<GroupMeta> _groupMetaLookup;
        private ComponentLookup<GroupFormation> _groupFormationLookup;
        private ComponentLookup<GroupFormationSpread> _groupFormationSpreadLookup;
        private ComponentLookup<SquadCohesionProfile> _cohesionProfileLookup;
        private ComponentLookup<SquadCohesionState> _cohesionStateLookup;
        private ComponentLookup<GroupStanceState> _stanceLookup;
        private ComponentLookup<EngagementThreatSummary> _threatLookup;
        private ComponentLookup<EngagementIntent> _intentLookup;
        private ComponentLookup<EngagementPlannerState> _plannerLookup;
        private ComponentLookup<WingFormationState> _wingFormationStateLookup;
        private ComponentLookup<WingGroupSyncState> _wingGroupSyncStateLookup;
        private ComponentLookup<LocalTransform> _localTransformLookup;
        private ComponentLookup<SquadTacticOrder> _squadTacticLookup;
        private ComponentLookup<PureDOTS.Runtime.Formation.FormationState> _formationStateLookup;
        private ComponentLookup<GroupMembership> _membershipLookup;
        private BufferLookup<CommsOutboxEntry> _commsOutboxLookup;
        private BufferLookup<GroupMember> _groupMemberLookup;
        private BufferLookup<WingFormationAnchorRef> _anchorRefLookup;
        private BufferLookup<PureDOTS.Runtime.Formation.FormationSlot> _formationSlotLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StrikeCraftProfile>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _groupTagLookup = state.GetComponentLookup<GroupTag>(true);
            _groupMetaLookup = state.GetComponentLookup<GroupMeta>(false);
            _groupFormationLookup = state.GetComponentLookup<GroupFormation>(true);
            _groupFormationSpreadLookup = state.GetComponentLookup<GroupFormationSpread>(true);
            _cohesionProfileLookup = state.GetComponentLookup<SquadCohesionProfile>(true);
            _cohesionStateLookup = state.GetComponentLookup<SquadCohesionState>(true);
            _stanceLookup = state.GetComponentLookup<GroupStanceState>(true);
            _threatLookup = state.GetComponentLookup<EngagementThreatSummary>(true);
            _intentLookup = state.GetComponentLookup<EngagementIntent>(true);
            _plannerLookup = state.GetComponentLookup<EngagementPlannerState>(true);
            _wingFormationStateLookup = state.GetComponentLookup<WingFormationState>(true);
            _wingGroupSyncStateLookup = state.GetComponentLookup<WingGroupSyncState>(false);
            _localTransformLookup = state.GetComponentLookup<LocalTransform>(true);
            _squadTacticLookup = state.GetComponentLookup<SquadTacticOrder>(true);
            _formationStateLookup = state.GetComponentLookup<PureDOTS.Runtime.Formation.FormationState>(true);
            _membershipLookup = state.GetComponentLookup<GroupMembership>(true);
            _commsOutboxLookup = state.GetBufferLookup<CommsOutboxEntry>(true);
            _groupMemberLookup = state.GetBufferLookup<GroupMember>(false);
            _anchorRefLookup = state.GetBufferLookup<WingFormationAnchorRef>(true);
            _formationSlotLookup = state.GetBufferLookup<PureDOTS.Runtime.Formation.FormationSlot>(true);
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

            var entityManager = state.EntityManager;
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

            _groupTagLookup.Update(ref state);
            _groupMetaLookup.Update(ref state);
            _groupFormationLookup.Update(ref state);
            _groupFormationSpreadLookup.Update(ref state);
            _cohesionProfileLookup.Update(ref state);
            _cohesionStateLookup.Update(ref state);
            _stanceLookup.Update(ref state);
            _threatLookup.Update(ref state);
            _intentLookup.Update(ref state);
            _plannerLookup.Update(ref state);
            _wingFormationStateLookup.Update(ref state);
            _wingGroupSyncStateLookup.Update(ref state);
            _localTransformLookup.Update(ref state);
            _squadTacticLookup.Update(ref state);
            _formationStateLookup.Update(ref state);
            _membershipLookup.Update(ref state);
            _commsOutboxLookup.Update(ref state);
            _groupMemberLookup.Update(ref state);
            _anchorRefLookup.Update(ref state);
            _formationSlotLookup.Update(ref state);

            var craftQuery = SystemAPI.QueryBuilder().WithAll<StrikeCraftProfile>().Build();
            var craftCount = craftQuery.CalculateEntityCount();
            var capacity = math.max(1, craftCount);
            var leaderMembers = new NativeParallelMultiHashMap<Entity, Entity>(capacity, Allocator.Temp);
            var leaders = new NativeList<Entity>(capacity, Allocator.Temp);
            var leaderSet = new NativeParallelHashMap<Entity, byte>(capacity, Allocator.Temp);

            foreach (var (profile, entity) in SystemAPI.Query<RefRO<StrikeCraftProfile>>().WithEntityAccess())
            {
                var leader = profile.ValueRO.WingLeader;
                if (leader == Entity.Null || !entityManager.Exists(leader))
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
                if (!entityManager.Exists(leader))
                {
                    continue;
                }

                EnsureGroupScaffold(leader, wingDecisionConfig.MaxWingSize, defaults, entityManager);
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
            EntityManager entityManager)
        {
            if (!IsValidEntity(leader, entityManager))
            {
                return;
            }

            if (!entityManager.HasComponent<GroupTag>(leader))
            {
                entityManager.AddComponent<GroupTag>(leader);
            }

            if (!entityManager.HasComponent<GroupMeta>(leader))
            {
                entityManager.AddComponentData(leader, new GroupMeta
                {
                    Kind = GroupKind.StrikeWing,
                    Leader = leader,
                    MaxSize = maxSize
                });
            }
            else
            {
                var meta = entityManager.GetComponentData<GroupMeta>(leader);
                meta.Kind = GroupKind.StrikeWing;
                meta.Leader = leader;
                meta.MaxSize = maxSize;
                entityManager.SetComponentData(leader, meta);
            }

            if (!entityManager.HasComponent<GroupFormation>(leader))
            {
                entityManager.AddComponentData(leader, new GroupFormation
                {
                    Type = PureDOTS.Runtime.Groups.FormationType.Line,
                    Spacing = defaults.LooseSpacing,
                    Cohesion = defaults.LooseCohesion,
                    FacingWeight = defaults.LooseFacingWeight
                });
            }

            if (!entityManager.HasComponent<GroupFormationSpread>(leader))
            {
                entityManager.AddComponentData(leader, new GroupFormationSpread
                {
                    CohesionNormalized = 0f
                });
            }

            if (!entityManager.HasComponent<SquadCohesionProfile>(leader))
            {
                entityManager.AddComponentData(leader, SquadCohesionProfile.Default);
            }

            if (!entityManager.HasComponent<SquadCohesionState>(leader))
            {
                entityManager.AddComponentData(leader, new SquadCohesionState
                {
                    NormalizedCohesion = 0f,
                    Flags = 0,
                    LastUpdateTick = 0,
                    LastBroadcastTick = 0,
                    LastTelemetryTick = 0
                });
            }

            if (!entityManager.HasComponent<GroupStanceState>(leader))
            {
                entityManager.AddComponentData(leader, new GroupStanceState
                {
                    Stance = GroupStance.Hold,
                    PrimaryTarget = Entity.Null,
                    Aggression = 0f,
                    Discipline = 0.5f
                });
            }

            if (!entityManager.HasComponent<EngagementThreatSummary>(leader))
            {
                entityManager.AddComponentData(leader, new EngagementThreatSummary
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

            if (!entityManager.HasComponent<EngagementIntent>(leader))
            {
                entityManager.AddComponentData(leader, new EngagementIntent
                {
                    Kind = EngagementIntentKind.None,
                    PrimaryTarget = Entity.Null,
                    AdvantageRatio = 1f,
                    ThreatPressure = 0f,
                    LastUpdateTick = 0
                });
            }

            if (!entityManager.HasComponent<EngagementPlannerState>(leader))
            {
                entityManager.AddComponentData(leader, new EngagementPlannerState
                {
                    LastIntentTick = 0,
                    LastTacticTick = 0,
                    LastTargetingTick = 0
                });
            }

            if (!entityManager.HasBuffer<CommsOutboxEntry>(leader))
            {
                entityManager.AddBuffer<CommsOutboxEntry>(leader);
            }

            if (!entityManager.HasBuffer<GroupMember>(leader))
            {
                entityManager.AddBuffer<GroupMember>(leader);
            }

            if (!entityManager.HasComponent<WingFormationState>(leader))
            {
                entityManager.AddComponentData(leader, new WingFormationState
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

            if (!entityManager.HasComponent<WingGroupSyncState>(leader))
            {
                entityManager.AddComponentData(leader, new WingGroupSyncState
                {
                    LastMemberCount = 0,
                    LastMemberHash = 0u
                });
            }

            if (!entityManager.HasBuffer<WingFormationAnchorRef>(leader))
            {
                entityManager.AddBuffer<WingFormationAnchorRef>(leader);
            }

            if (!entityManager.HasComponent<LocalTransform>(leader))
            {
                entityManager.AddComponentData(leader, LocalTransform.Identity);
            }
        }

        private void CleanupOrphanedGroups(NativeParallelHashMap<Entity, byte> leaderSet, ref SystemState state)
        {
            var entityManager = state.EntityManager;
            var groupQuery = SystemAPI.QueryBuilder().WithAll<GroupTag, GroupMeta>().Build();
            var groups = groupQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < groups.Length; i++)
            {
                var groupEntity = groups[i];
                if (!IsValidEntity(groupEntity, entityManager))
                {
                    continue;
                }

                if (!entityManager.HasComponent<GroupMeta>(groupEntity))
                {
                    continue;
                }

                var meta = entityManager.GetComponentData<GroupMeta>(groupEntity);
                if (meta.Kind != GroupKind.StrikeWing)
                {
                    continue;
                }

                if (leaderSet.TryGetValue(groupEntity, out _))
                {
                    continue;
                }

                if (entityManager.HasBuffer<WingFormationAnchorRef>(groupEntity))
                {
                    var anchors = entityManager.GetBuffer<WingFormationAnchorRef>(groupEntity);
                    for (int a = 0; a < anchors.Length; a++)
                    {
                        var anchorEntity = anchors[a].Anchor;
                        if (IsValidEntity(anchorEntity, entityManager))
                        {
                            entityManager.DestroyEntity(anchorEntity);
                        }
                    }

                    entityManager.RemoveComponent<WingFormationAnchorRef>(groupEntity);
                }

                if (entityManager.HasComponent<WingFormationState>(groupEntity))
                {
                    entityManager.RemoveComponent<WingFormationState>(groupEntity);
                }

                if (entityManager.HasComponent<WingGroupSyncState>(groupEntity))
                {
                    entityManager.RemoveComponent<WingGroupSyncState>(groupEntity);
                }

                if (entityManager.HasComponent<GroupFormation>(groupEntity))
                {
                    entityManager.RemoveComponent<GroupFormation>(groupEntity);
                }

                if (entityManager.HasComponent<GroupFormationSpread>(groupEntity))
                {
                    entityManager.RemoveComponent<GroupFormationSpread>(groupEntity);
                }

                if (entityManager.HasComponent<SquadCohesionProfile>(groupEntity))
                {
                    entityManager.RemoveComponent<SquadCohesionProfile>(groupEntity);
                }

                if (entityManager.HasComponent<SquadCohesionState>(groupEntity))
                {
                    entityManager.RemoveComponent<SquadCohesionState>(groupEntity);
                }

                if (entityManager.HasComponent<GroupStanceState>(groupEntity))
                {
                    entityManager.RemoveComponent<GroupStanceState>(groupEntity);
                }

                if (entityManager.HasComponent<EngagementThreatSummary>(groupEntity))
                {
                    entityManager.RemoveComponent<EngagementThreatSummary>(groupEntity);
                }

                if (entityManager.HasComponent<EngagementIntent>(groupEntity))
                {
                    entityManager.RemoveComponent<EngagementIntent>(groupEntity);
                }

                if (entityManager.HasComponent<EngagementPlannerState>(groupEntity))
                {
                    entityManager.RemoveComponent<EngagementPlannerState>(groupEntity);
                }

                if (entityManager.HasBuffer<CommsOutboxEntry>(groupEntity))
                {
                    entityManager.RemoveComponent<CommsOutboxEntry>(groupEntity);
                }

                if (entityManager.HasBuffer<GroupMember>(groupEntity))
                {
                    entityManager.RemoveComponent<GroupMember>(groupEntity);
                }

                if (entityManager.HasComponent<SquadTacticOrder>(groupEntity))
                {
                    entityManager.RemoveComponent<SquadTacticOrder>(groupEntity);
                }

                if (entityManager.HasComponent<PureDOTS.Runtime.Formation.FormationState>(groupEntity))
                {
                    entityManager.RemoveComponent<PureDOTS.Runtime.Formation.FormationState>(groupEntity);
                }

                if (entityManager.HasBuffer<PureDOTS.Runtime.Formation.FormationSlot>(groupEntity))
                {
                    entityManager.RemoveComponent<PureDOTS.Runtime.Formation.FormationSlot>(groupEntity);
                }

                if (entityManager.HasComponent<GroupTag>(groupEntity))
                {
                    entityManager.RemoveComponent<GroupTag>(groupEntity);
                }

                if (entityManager.HasComponent<GroupMeta>(groupEntity))
                {
                    entityManager.RemoveComponent<GroupMeta>(groupEntity);
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
            var entityManager = state.EntityManager;
            if (!IsValidEntity(leader, entityManager))
            {
                return;
            }

            if (!entityManager.HasComponent<WingGroupSyncState>(leader) || !entityManager.HasBuffer<GroupMember>(leader))
            {
                return;
            }

            var syncState = entityManager.GetComponentData<WingGroupSyncState>(leader);
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

                    if (!IsValidEntity(member, entityManager))
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

            var membersBuffer = entityManager.GetBuffer<GroupMember>(leader);
            membersBuffer.Clear();
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            AddMember(ref state, ref ecb, membersBuffer, leader, leader, GroupRole.Leader, tick);

            if (leaderMembers.TryGetFirstValue(leader, out member, out iterator))
            {
                do
                {
                    if (member == leader)
                    {
                        continue;
                    }

                    if (!IsValidEntity(member, entityManager))
                    {
                        continue;
                    }

                    AddMember(ref state, ref ecb, membersBuffer, leader, member, GroupRole.Member, tick);
                } while (leaderMembers.TryGetNextValue(out member, ref iterator));
            }

            syncState.LastMemberCount = clampedCount;
            syncState.LastMemberHash = memberHash;
            entityManager.SetComponentData(leader, syncState);

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private static uint HashEntity(Entity entity)
        {
            return math.hash(new int2(entity.Index, entity.Version));
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
            if (!IsValidEntity(member, state.EntityManager))
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

        private static bool IsValidEntity(Entity entity, EntityManager entityManager)
        {
            return entity != Entity.Null && entityManager.Exists(entity);
        }
    }
}
