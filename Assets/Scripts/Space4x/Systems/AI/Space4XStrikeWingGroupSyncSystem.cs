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
                var leader = leaders[i];
                if (!_entityInfoLookup.Exists(leader))
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
            if (!IsValidEntity(leader))
            {
                return;
            }

            if (!_groupTagLookup.HasComponent(leader))
            {
                state.EntityManager.AddComponent<GroupTag>(leader);
            }

            if (!_groupMetaLookup.HasComponent(leader))
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
                var meta = _groupMetaLookup[leader];
                meta.Kind = GroupKind.StrikeWing;
                meta.Leader = leader;
                meta.MaxSize = maxSize;
                _groupMetaLookup[leader] = meta;
            }

            if (!_groupFormationLookup.HasComponent(leader))
            {
                state.EntityManager.AddComponentData(leader, new GroupFormation
                {
                    Type = PureDOTS.Runtime.Groups.FormationType.Line,
                    Spacing = defaults.LooseSpacing,
                    Cohesion = defaults.LooseCohesion,
                    FacingWeight = defaults.LooseFacingWeight
                });
            }

            if (!_groupFormationSpreadLookup.HasComponent(leader))
            {
                state.EntityManager.AddComponentData(leader, new GroupFormationSpread
                {
                    CohesionNormalized = 0f
                });
            }

            if (!_cohesionProfileLookup.HasComponent(leader))
            {
                state.EntityManager.AddComponentData(leader, SquadCohesionProfile.Default);
            }

            if (!_cohesionStateLookup.HasComponent(leader))
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

            if (!_stanceLookup.HasComponent(leader))
            {
                state.EntityManager.AddComponentData(leader, new GroupStanceState
                {
                    Stance = GroupStance.Hold,
                    PrimaryTarget = Entity.Null,
                    Aggression = 0f,
                    Discipline = 0.5f
                });
            }

            if (!_threatLookup.HasComponent(leader))
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

            if (!_intentLookup.HasComponent(leader))
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

            if (!_plannerLookup.HasComponent(leader))
            {
                state.EntityManager.AddComponentData(leader, new EngagementPlannerState
                {
                    LastIntentTick = 0,
                    LastTacticTick = 0,
                    LastTargetingTick = 0
                });
            }

            if (!_commsOutboxLookup.HasBuffer(leader))
            {
                state.EntityManager.AddBuffer<CommsOutboxEntry>(leader);
            }

            if (!_groupMemberLookup.HasBuffer(leader))
            {
                state.EntityManager.AddBuffer<GroupMember>(leader);
            }

            if (!_wingFormationStateLookup.HasComponent(leader))
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

            if (!_wingGroupSyncStateLookup.HasComponent(leader))
            {
                state.EntityManager.AddComponentData(leader, new WingGroupSyncState
                {
                    LastMemberCount = 0,
                    LastMemberHash = 0u
                });
            }

            if (!_anchorRefLookup.HasBuffer(leader))
            {
                state.EntityManager.AddBuffer<WingFormationAnchorRef>(leader);
            }

            if (!_localTransformLookup.HasComponent(leader))
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
                if (!IsValidEntity(groupEntity))
                {
                    continue;
                }

                if (!_groupMetaLookup.HasComponent(groupEntity))
                {
                    continue;
                }

                var meta = _groupMetaLookup[groupEntity];
                if (meta.Kind != GroupKind.StrikeWing)
                {
                    continue;
                }

                if (leaderSet.TryGetValue(groupEntity, out _))
                {
                    continue;
                }

                if (_anchorRefLookup.HasBuffer(groupEntity))
                {
                    var anchors = _anchorRefLookup[groupEntity];
                    for (int a = 0; a < anchors.Length; a++)
                    {
                        var anchorEntity = anchors[a].Anchor;
                        if (IsValidEntity(anchorEntity))
                        {
                            state.EntityManager.DestroyEntity(anchorEntity);
                        }
                    }

                    state.EntityManager.RemoveComponent<WingFormationAnchorRef>(groupEntity);
                }

                if (_wingFormationStateLookup.HasComponent(groupEntity))
                {
                    state.EntityManager.RemoveComponent<WingFormationState>(groupEntity);
                }

                if (_wingGroupSyncStateLookup.HasComponent(groupEntity))
                {
                    state.EntityManager.RemoveComponent<WingGroupSyncState>(groupEntity);
                }

                if (_groupFormationLookup.HasComponent(groupEntity))
                {
                    state.EntityManager.RemoveComponent<GroupFormation>(groupEntity);
                }

                if (_groupFormationSpreadLookup.HasComponent(groupEntity))
                {
                    state.EntityManager.RemoveComponent<GroupFormationSpread>(groupEntity);
                }

                if (_cohesionProfileLookup.HasComponent(groupEntity))
                {
                    state.EntityManager.RemoveComponent<SquadCohesionProfile>(groupEntity);
                }

                if (_cohesionStateLookup.HasComponent(groupEntity))
                {
                    state.EntityManager.RemoveComponent<SquadCohesionState>(groupEntity);
                }

                if (_stanceLookup.HasComponent(groupEntity))
                {
                    state.EntityManager.RemoveComponent<GroupStanceState>(groupEntity);
                }

                if (_threatLookup.HasComponent(groupEntity))
                {
                    state.EntityManager.RemoveComponent<EngagementThreatSummary>(groupEntity);
                }

                if (_intentLookup.HasComponent(groupEntity))
                {
                    state.EntityManager.RemoveComponent<EngagementIntent>(groupEntity);
                }

                if (_plannerLookup.HasComponent(groupEntity))
                {
                    state.EntityManager.RemoveComponent<EngagementPlannerState>(groupEntity);
                }

                if (_commsOutboxLookup.HasBuffer(groupEntity))
                {
                    state.EntityManager.RemoveComponent<CommsOutboxEntry>(groupEntity);
                }

                if (_groupMemberLookup.HasBuffer(groupEntity))
                {
                    state.EntityManager.RemoveComponent<GroupMember>(groupEntity);
                }

                if (_squadTacticLookup.HasComponent(groupEntity))
                {
                    state.EntityManager.RemoveComponent<SquadTacticOrder>(groupEntity);
                }

                if (_formationStateLookup.HasComponent(groupEntity))
                {
                    state.EntityManager.RemoveComponent<PureDOTS.Runtime.Formation.FormationState>(groupEntity);
                }

                if (_formationSlotLookup.HasBuffer(groupEntity))
                {
                    state.EntityManager.RemoveComponent<PureDOTS.Runtime.Formation.FormationSlot>(groupEntity);
                }

                if (_groupTagLookup.HasComponent(groupEntity))
                {
                    state.EntityManager.RemoveComponent<GroupTag>(groupEntity);
                }

                if (_groupMetaLookup.HasComponent(groupEntity))
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
