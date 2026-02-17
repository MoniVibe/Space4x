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
        private EntityStorageInfoLookup _entityInfoLookup;
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

            _entityInfoLookup = state.GetEntityStorageInfoLookup();
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

            UpdateLookups(ref state);

            var craftQuery = SystemAPI.QueryBuilder().WithAll<StrikeCraftProfile>().Build();
            var craftCount = craftQuery.CalculateEntityCount();
            var capacity = math.max(1, craftCount);
            var leaderMembers = new NativeParallelMultiHashMap<Entity, Entity>(capacity, Allocator.Temp);
            var leaders = new NativeList<Entity>(capacity, Allocator.Temp);
            var leaderSet = new NativeParallelHashMap<Entity, byte>(capacity, Allocator.Temp);

            foreach (var (profile, entity) in SystemAPI.Query<RefRO<StrikeCraftProfile>>().WithEntityAccess())
            {
                var leader = profile.ValueRO.WingLeader;
                if (leader == Entity.Null || !_entityInfoLookup.Exists(leader))
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
                UpdateLookups(ref state);
                var leader = leaders[i];
                if (!_entityInfoLookup.Exists(leader))
                {
                    continue;
                }

                EnsureGroupScaffold(leader, wingDecisionConfig.MaxWingSize, defaults, ref state);
                UpdateLookups(ref state);
                SyncGroupMembers(ref state, leader, leaderMembers, timeState.Tick);
            }

            CleanupOrphanedGroups(leaderSet, ref state);

            leaders.Dispose();
            leaderMembers.Dispose();
            leaderSet.Dispose();
        }

        private void UpdateLookups(ref SystemState state)
        {
            _entityInfoLookup.Update(ref state);
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
        }

        private void EnsureGroupScaffold(
            Entity leader,
            byte maxSize,
            in WingFormationDefaults defaults,
            ref SystemState state)
        {
            if (!IsValidEntity(leader))
            {
                return;
            }

            var hasGroupTag = _groupTagLookup.HasComponent(leader);
            var hasGroupMeta = _groupMetaLookup.HasComponent(leader);
            var hasGroupFormation = _groupFormationLookup.HasComponent(leader);
            var hasGroupFormationSpread = _groupFormationSpreadLookup.HasComponent(leader);
            var hasCohesionProfile = _cohesionProfileLookup.HasComponent(leader);
            var hasCohesionState = _cohesionStateLookup.HasComponent(leader);
            var hasStance = _stanceLookup.HasComponent(leader);
            var hasThreat = _threatLookup.HasComponent(leader);
            var hasIntent = _intentLookup.HasComponent(leader);
            var hasPlanner = _plannerLookup.HasComponent(leader);
            var hasCommsOutbox = _commsOutboxLookup.HasBuffer(leader);
            var hasGroupMemberBuffer = _groupMemberLookup.HasBuffer(leader);
            var hasWingFormationState = _wingFormationStateLookup.HasComponent(leader);
            var hasWingGroupSyncState = _wingGroupSyncStateLookup.HasComponent(leader);
            var hasAnchorRefs = _anchorRefLookup.HasBuffer(leader);
            var hasLocalTransform = _localTransformLookup.HasComponent(leader);

            if (hasGroupMeta)
            {
                var meta = _groupMetaLookup[leader];
                meta.Kind = GroupKind.StrikeWing;
                meta.Leader = leader;
                meta.MaxSize = maxSize;
                _groupMetaLookup[leader] = meta;
            }

            var em = state.EntityManager;
            if (!hasGroupTag)
            {
                em.AddComponent<GroupTag>(leader);
            }

            if (!hasGroupMeta)
            {
                em.AddComponentData(leader, new GroupMeta
                {
                    Kind = GroupKind.StrikeWing,
                    Leader = leader,
                    MaxSize = maxSize
                });
            }

            if (!hasGroupFormation)
            {
                em.AddComponentData(leader, new GroupFormation
                {
                    Type = PureDOTS.Runtime.Groups.FormationType.Line,
                    Spacing = defaults.LooseSpacing,
                    Cohesion = defaults.LooseCohesion,
                    FacingWeight = defaults.LooseFacingWeight
                });
            }

            if (!hasGroupFormationSpread)
            {
                em.AddComponentData(leader, new GroupFormationSpread
                {
                    CohesionNormalized = 0f
                });
            }

            if (!hasCohesionProfile)
            {
                em.AddComponentData(leader, SquadCohesionProfile.Default);
            }

            if (!hasCohesionState)
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

            if (!hasStance)
            {
                em.AddComponentData(leader, new GroupStanceState
                {
                    Stance = GroupStance.Hold,
                    PrimaryTarget = Entity.Null,
                    Aggression = 0f,
                    Discipline = 0.5f
                });
            }

            if (!hasThreat)
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

            if (!hasIntent)
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

            if (!hasPlanner)
            {
                em.AddComponentData(leader, new EngagementPlannerState
                {
                    LastIntentTick = 0,
                    LastTacticTick = 0,
                    LastTargetingTick = 0
                });
            }

            if (!hasCommsOutbox)
            {
                em.AddBuffer<CommsOutboxEntry>(leader);
            }

            if (!hasGroupMemberBuffer)
            {
                em.AddBuffer<GroupMember>(leader);
            }

            if (!hasWingFormationState)
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

            if (!hasWingGroupSyncState)
            {
                em.AddComponentData(leader, new WingGroupSyncState
                {
                    LastMemberCount = 0,
                    LastMemberHash = 0u
                });
            }

            if (!hasAnchorRefs)
            {
                em.AddBuffer<WingFormationAnchorRef>(leader);
            }

            if (!hasLocalTransform)
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

                if (em.HasComponent<CommsOutboxEntry>(groupEntity))
                {
                    em.RemoveComponent<CommsOutboxEntry>(groupEntity);
                }

                if (em.HasComponent<GroupMember>(groupEntity))
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

                if (em.HasComponent<PureDOTS.Runtime.Formation.FormationSlot>(groupEntity))
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
            if (!IsValidEntity(leader))
            {
                return;
            }

            if (!_wingGroupSyncStateLookup.HasComponent(leader) || !_groupMemberLookup.HasBuffer(leader))
            {
                return;
            }

            var syncState = _wingGroupSyncStateLookup[leader];
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

                    if (!IsValidEntity(member))
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

            var membersBuffer = _groupMemberLookup[leader];
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

                    if (!IsValidEntity(member))
                    {
                        continue;
                    }

                    AddMember(ref state, ref ecb, membersBuffer, leader, member, GroupRole.Member, tick);
                } while (leaderMembers.TryGetNextValue(out member, ref iterator));
            }

            syncState.LastMemberCount = clampedCount;
            syncState.LastMemberHash = memberHash;
            _wingGroupSyncStateLookup[leader] = syncState;

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
            if (!IsValidEntity(member))
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

            if (!_membershipLookup.HasComponent(member))
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

        private bool IsValidEntity(Entity entity)
        {
            return entity != Entity.Null && _entityInfoLookup.Exists(entity);
        }
    }
}
