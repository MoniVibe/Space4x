using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Formation;
using PureDOTS.Runtime.Groups;
using PureDOTS.Runtime.Profile;
using Space4X.Registry;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using FormationType = PureDOTS.Runtime.Formation.FormationType;
using FormationSlot = PureDOTS.Runtime.Formation.FormationSlot;

namespace Space4X.Systems.AI
{
    [UpdateInGroup(typeof(PureDOTS.Systems.GroupDecisionSystemGroup))]
    [UpdateAfter(typeof(Space4XStrikeWingGroupSyncSystem))]
    [UpdateBefore(typeof(PureDOTS.Systems.Groups.SquadCohesionSystem))]
    [UpdateBefore(typeof(PureDOTS.Runtime.Systems.Formation.FormationSlotUpdateSystem))]
    public partial struct Space4XWingFormationPlannerSystem : ISystem
    {
        private EntityStorageInfoLookup _entityInfoLookup;
        private ComponentLookup<GroupMeta> _groupMetaLookup;
        private ComponentLookup<GroupFormation> _groupFormationLookup;
        private ComponentLookup<WingFormationState> _wingStateLookup;
        private ComponentLookup<SquadTacticOrder> _tacticLookup;
        private ComponentLookup<WingFormationAnchor> _wingAnchorLookup;
        private ComponentLookup<FormationState> _formationStateLookup;
        private BufferLookup<GroupMember> _groupMemberLookup;
        private BufferLookup<WingFormationAnchorRef> _anchorRefLookup;
        private BufferLookup<FormationSlot> _formationSlotLookup;
        private ComponentLookup<StrikeCraftWingDirective> _wingDirectiveLookup;
        private ComponentLookup<StrikeCraftProfile> _strikeProfileLookup;
        private ComponentLookup<StrikeCraftState> _strikeStateLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<SquadAckState> _ackLookup;
        private ComponentLookup<FormationMember> _formationMemberLookup;
        private ComponentLookup<StrikeCraftPilotLink> _strikePilotLookup;
        private ComponentLookup<VesselPilotLink> _vesselPilotLookup;
        private ComponentLookup<BehaviorDisposition> _behaviorDispositionLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<GroupFormation>();

            _entityInfoLookup = state.GetEntityStorageInfoLookup();
            _groupMetaLookup = state.GetComponentLookup<GroupMeta>(true);
            _groupFormationLookup = state.GetComponentLookup<GroupFormation>(false);
            _wingStateLookup = state.GetComponentLookup<WingFormationState>(false);
            _tacticLookup = state.GetComponentLookup<SquadTacticOrder>(false);
            _wingAnchorLookup = state.GetComponentLookup<WingFormationAnchor>(false);
            _formationStateLookup = state.GetComponentLookup<FormationState>(false);
            _groupMemberLookup = state.GetBufferLookup<GroupMember>(true);
            _anchorRefLookup = state.GetBufferLookup<WingFormationAnchorRef>(false);
            _formationSlotLookup = state.GetBufferLookup<FormationSlot>(false);
            _wingDirectiveLookup = state.GetComponentLookup<StrikeCraftWingDirective>(true);
            _strikeProfileLookup = state.GetComponentLookup<StrikeCraftProfile>(true);
            _strikeStateLookup = state.GetComponentLookup<StrikeCraftState>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _ackLookup = state.GetComponentLookup<SquadAckState>(true);
            _formationMemberLookup = state.GetComponentLookup<FormationMember>(false);
            _strikePilotLookup = state.GetComponentLookup<StrikeCraftPilotLink>(true);
            _vesselPilotLookup = state.GetComponentLookup<VesselPilotLink>(true);
            _behaviorDispositionLookup = state.GetComponentLookup<BehaviorDisposition>(true);
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

            var formationConfig = WingFormationConfig.Default;
            if (SystemAPI.TryGetSingleton<WingFormationConfig>(out var formationSingleton))
            {
                formationConfig = formationSingleton;
            }

            RefreshLookups(ref state);

            var groupQuery = SystemAPI.QueryBuilder()
                .WithAll<GroupTag, GroupMeta, GroupFormation>()
                .WithAll<WingFormationState>()
                .WithAll<GroupMember>()
                .Build();

            var groups = groupQuery.ToEntityArray(Allocator.Temp);
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            for (int i = 0; i < groups.Length; i++)
            {
                var groupEntity = groups[i];
                if (!_groupMetaLookup.HasComponent(groupEntity))
                {
                    continue;
                }

                var meta = _groupMetaLookup[groupEntity];
                if (meta.Kind != GroupKind.StrikeWing && meta.Kind != GroupKind.MiningWing)
                {
                    continue;
                }

                var defaults = meta.Kind == GroupKind.StrikeWing
                    ? formationConfig.StrikeDefaults
                    : formationConfig.MiningDefaults;

                var tactic = EnsureTacticOrder(groupEntity, defaults, timeState.Tick, ref state, ref ecb);
                if (!_groupMemberLookup.HasBuffer(groupEntity))
                {
                    continue;
                }

                var membersBuffer = _groupMemberLookup[groupEntity];
                if (membersBuffer.Length == 0)
                {
                    continue;
                }

                var membersSnapshot = membersBuffer.ToNativeArray(Allocator.Temp);
                var ackRatio = ComputeAckRatio(
                    tactic,
                    membersBuffer,
                    out var activeCount,
                    out var memberHash,
                    out var avgCompliance,
                    out var avgFormationAdherence,
                    out var avgPatience,
                    out var avgAggression,
                    out var avgRiskTolerance);
                if (activeCount == 0)
                {
                    membersSnapshot.Dispose();
                    continue;
                }

                var discipline = math.saturate(avgCompliance * 0.6f + avgFormationAdherence * 0.4f);
                var tightBias = math.saturate(discipline * 0.7f + avgPatience * 0.3f);
                var splitBias = math.saturate(discipline * 0.5f + avgAggression * 0.3f + avgRiskTolerance * 0.2f);
                var ackThreshold = ResolveAckThreshold(defaults.AckSuccessThreshold, discipline);
                var acked = tactic.AckMode == 0 || ackRatio >= ackThreshold;

                var wantsTight = WantsTight(tactic.Kind);
                var tightActive = wantsTight && acked && tightBias >= 0.35f;
                var spacing = tightActive
                    ? math.lerp(defaults.TightSpacing, defaults.LooseSpacing, 1f - tightBias)
                    : defaults.LooseSpacing;
                var cohesion = tightActive
                    ? math.lerp(defaults.TightCohesion, defaults.LooseCohesion, 1f - tightBias)
                    : defaults.LooseCohesion;
                var facing = tightActive
                    ? math.lerp(defaults.TightFacingWeight, defaults.LooseFacingWeight, 1f - tightBias)
                    : defaults.LooseFacingWeight;
                var formationType = tightActive ? defaults.TightFormation : defaults.LooseFormation;

                var groupFormation = _groupFormationLookup[groupEntity];
                groupFormation.Spacing = spacing;
                groupFormation.Cohesion = cohesion;
                groupFormation.FacingWeight = facing;
                groupFormation.Type = tightActive ? PureDOTS.Runtime.Groups.FormationType.Line : PureDOTS.Runtime.Groups.FormationType.Swarm;
                _groupFormationLookup[groupEntity] = groupFormation;

                byte splitCount = 1;
                if (acked && IsSplitTactic(tactic.Kind))
                {
                    var desiredSplit = math.max(1, defaults.MaxSplitGroups);
                    splitCount = (byte)math.max(1, math.round(math.lerp(1f, desiredSplit, splitBias)));
                }

                splitCount = (byte)math.clamp(splitCount, 1, math.min(activeCount, 255));

                var wingState = _wingStateLookup[groupEntity];
                var ackedByte = (byte)(acked ? 1 : 0);
                var memberCount = (ushort)math.min(activeCount, ushort.MaxValue);
                var shouldRebuild = wingState.LastDecisionTick != tactic.IssueTick
                    || wingState.LastTacticKind != (byte)tactic.Kind
                    || wingState.SplitCount != splitCount
                    || wingState.LastMemberCount != memberCount
                    || wingState.LastAcked != ackedByte
                    || wingState.LastMemberHash != memberHash;

                wingState.LastDecisionTick = tactic.IssueTick;
                wingState.LastTacticKind = (byte)tactic.Kind;
                wingState.SplitCount = splitCount;
                wingState.LastAckRatio = ackRatio;
                wingState.LastMemberCount = memberCount;
                wingState.LastAcked = ackedByte;
                wingState.LastMemberHash = memberHash;
                _wingStateLookup[groupEntity] = wingState;

                var formationStructuralChange = false;
                var anchorRefs = EnsureAnchorRefs(groupEntity, splitCount, ref state, ref formationStructuralChange);
                if (formationStructuralChange)
                {
                    RefreshLookups(ref state);
                }

                var anchorEntities = new NativeArray<Entity>(splitCount, Allocator.Temp);
                anchorEntities[0] = groupEntity;
                for (int a = 1; a < splitCount; a++)
                {
                    anchorEntities[a] = anchorRefs[a - 1].Anchor;
                }

                var leaderTransform = _transformLookup.HasComponent(groupEntity)
                    ? _transformLookup[groupEntity]
                    : LocalTransform.Identity;
                var leaderPosition = leaderTransform.Position;
                var anchorRotation = ResolveAnchorRotation(groupEntity, leaderPosition, leaderTransform.Rotation);

                for (int a = 0; a < splitCount; a++)
                {
                    var anchorEntity = anchorEntities[a];
                    var anchorPosition = ResolveAnchorPosition(anchorRotation, leaderPosition, defaults, tactic.Kind, splitCount, a);
                    var anchorCount = ResolveSplitCount(activeCount, splitCount, a);
                    EnsureFormationState(anchorEntity, formationType, spacing, anchorPosition, anchorRotation, anchorCount, timeState.Tick, ref state, ref formationStructuralChange);

                    if (shouldRebuild)
                    {
                        var slots = state.EntityManager.GetBuffer<FormationSlot>(anchorEntity);
                        slots.Clear();
                    }
                }

                if (formationStructuralChange)
                {
                    RefreshLookups(ref state);
                }

                if (shouldRebuild)
                {
                    var slotIndices = new NativeArray<int>(splitCount, Allocator.Temp);
                    var arrivalThreshold = math.max(0.5f, spacing * 0.25f);
                    var activeIndex = 0;
                    for (int m = 0; m < membersSnapshot.Length; m++)
                    {
                        var member = membersSnapshot[m];
                        if ((member.Flags & GroupMemberFlags.Active) == 0)
                        {
                            continue;
                        }

                        var memberEntity = member.MemberEntity;
                        if (memberEntity == Entity.Null || !_entityInfoLookup.Exists(memberEntity))
                        {
                            continue;
                        }

                        var anchorIndex = activeIndex % splitCount;
                        activeIndex++;

                        var anchorEntity = anchorEntities[anchorIndex];
                        var slotIndex = slotIndices[anchorIndex]++;

                        AssignFormationMember(memberEntity, anchorEntity, slotIndex, arrivalThreshold, timeState.Tick, ref state, ref ecb);

                        var slots = state.EntityManager.GetBuffer<FormationSlot>(anchorEntity);
                        slots.Add(new FormationSlot
                        {
                            SlotIndex = (byte)slotIndex,
                            LocalOffset = float3.zero,
                            Role = FormationSlotRole.Any,
                            AssignedEntity = memberEntity,
                            Priority = 0,
                            IsRequired = false
                        });
                    }

                    slotIndices.Dispose();
                }

                membersSnapshot.Dispose();
                anchorEntities.Dispose();
            }

            groups.Dispose();
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private SquadTacticOrder EnsureTacticOrder(
            Entity groupEntity,
            in WingFormationDefaults defaults,
            uint tick,
            ref SystemState state,
            ref EntityCommandBuffer ecb)
        {
            var entityManager = state.EntityManager;
            SquadTacticOrder tactic;
            var hasTactic = _tacticLookup.HasComponent(groupEntity);
            if (!hasTactic)
            {
                tactic = new SquadTacticOrder
                {
                    Kind = SquadTacticKind.None,
                    Issuer = Entity.Null,
                    Target = Entity.Null,
                    FocusBudgetCost = 0f,
                    DisciplineRequired = defaults.DisciplineRequired,
                    AckMode = 0,
                    IssueTick = 0
                };
            }
            else
            {
                tactic = _tacticLookup[groupEntity];
            }

            var desiredKind = tactic.Kind;
            var ackMode = tactic.AckMode;

            if (entityManager.HasComponent<GroupMeta>(groupEntity))
            {
                var meta = entityManager.GetComponentData<GroupMeta>(groupEntity);
                if (meta.Kind == GroupKind.StrikeWing && _wingDirectiveLookup.HasComponent(groupEntity))
                {
                    var directive = _wingDirectiveLookup[groupEntity];
                    desiredKind = directive.Mode == 0 ? SquadTacticKind.Tighten : SquadTacticKind.Loosen;
                    ackMode = desiredKind == SquadTacticKind.Tighten
                        ? defaults.RequireAckForTighten
                        : (byte)0;
                }
            }

            if (desiredKind != tactic.Kind || tactic.IssueTick == 0 || !hasTactic)
            {
                tactic.Kind = desiredKind;
                tactic.Issuer = groupEntity;
                tactic.Target = ResolveGroupTarget(groupEntity);
                tactic.DisciplineRequired = defaults.DisciplineRequired;
                tactic.AckMode = ackMode;
                tactic.IssueTick = tick;
                if (hasTactic)
                {
                    _tacticLookup[groupEntity] = tactic;
                }
                else
                {
                    ecb.AddComponent(groupEntity, tactic);
                }
            }

            return tactic;
        }

        private float ComputeAckRatio(
            in SquadTacticOrder tactic,
            DynamicBuffer<GroupMember> members,
            out int activeCount,
            out uint memberHash,
            out float avgCompliance,
            out float avgFormationAdherence,
            out float avgPatience,
            out float avgAggression,
            out float avgRiskTolerance)
        {
            activeCount = 0;
            memberHash = 0u;
            var ackCount = 0;
            var dispositionCount = 0;
            var complianceSum = 0f;
            var formationSum = 0f;
            var patienceSum = 0f;
            var aggressionSum = 0f;
            var riskSum = 0f;
            var requiresAck = tactic.AckMode != 0 && tactic.IssueTick != 0;
            for (int i = 0; i < members.Length; i++)
            {
                var member = members[i];
                if ((member.Flags & GroupMemberFlags.Active) == 0)
                {
                    continue;
                }

                var memberEntity = member.MemberEntity;
                if (memberEntity == Entity.Null || !_entityInfoLookup.Exists(memberEntity))
                {
                    continue;
                }

                activeCount++;
                memberHash ^= math.hash(new int2(memberEntity.Index, memberEntity.Version));
                if (requiresAck && _ackLookup.HasComponent(memberEntity))
                {
                    var ack = _ackLookup[memberEntity];
                    if (ack.LastAckTick == tactic.IssueTick)
                    {
                        ackCount++;
                    }
                }

                if (TryResolveDisposition(memberEntity, out var disposition))
                {
                    complianceSum += disposition.Compliance;
                    formationSum += disposition.FormationAdherence;
                    patienceSum += disposition.Patience;
                    aggressionSum += disposition.Aggression;
                    riskSum += disposition.RiskTolerance;
                    dispositionCount++;
                }
            }

            if (dispositionCount == 0)
            {
                avgCompliance = 0.5f;
                avgFormationAdherence = 0.5f;
                avgPatience = 0.5f;
                avgAggression = 0.5f;
                avgRiskTolerance = 0.5f;
            }
            else
            {
                var invCount = 1f / dispositionCount;
                avgCompliance = complianceSum * invCount;
                avgFormationAdherence = formationSum * invCount;
                avgPatience = patienceSum * invCount;
                avgAggression = aggressionSum * invCount;
                avgRiskTolerance = riskSum * invCount;
            }

            if (activeCount == 0)
            {
                return 0f;
            }

            if (!requiresAck)
            {
                return 1f;
            }

            return ackCount / (float)activeCount;
        }

        private bool TryResolveDisposition(Entity memberEntity, out BehaviorDisposition disposition)
        {
            var profileEntity = ResolveProfileEntity(memberEntity);
            if (_behaviorDispositionLookup.HasComponent(profileEntity))
            {
                disposition = _behaviorDispositionLookup[profileEntity];
                return true;
            }

            if (_behaviorDispositionLookup.HasComponent(memberEntity))
            {
                disposition = _behaviorDispositionLookup[memberEntity];
                return true;
            }

            disposition = default;
            return false;
        }

        private Entity ResolveProfileEntity(Entity memberEntity)
        {
            if (_strikePilotLookup.HasComponent(memberEntity))
            {
                var pilot = _strikePilotLookup[memberEntity].Pilot;
                if (pilot != Entity.Null)
                {
                    return pilot;
                }
            }

            if (_vesselPilotLookup.HasComponent(memberEntity))
            {
                var pilot = _vesselPilotLookup[memberEntity].Pilot;
                if (pilot != Entity.Null)
                {
                    return pilot;
                }
            }

            return memberEntity;
        }

        private static float ResolveAckThreshold(float baseThreshold, float discipline)
        {
            var maxThreshold = math.min(0.95f, baseThreshold + 0.25f);
            return math.clamp(math.lerp(baseThreshold, maxThreshold, 1f - discipline), baseThreshold, 0.98f);
        }

        private static int ResolveSplitCount(int totalMembers, int splitCount, int anchorIndex)
        {
            if (splitCount <= 0)
            {
                return 0;
            }

            var baseCount = totalMembers / splitCount;
            var remainder = totalMembers - (baseCount * splitCount);
            return baseCount + (anchorIndex < remainder ? 1 : 0);
        }

        private DynamicBuffer<WingFormationAnchorRef> EnsureAnchorRefs(
            Entity groupEntity,
            int splitCount,
            ref SystemState state,
            ref bool structuralChange)
        {
            var anchorStructuralChange = false;
            var refreshNeeded = false;
            if (!_anchorRefLookup.HasBuffer(groupEntity))
            {
                state.EntityManager.AddBuffer<WingFormationAnchorRef>(groupEntity);
                _anchorRefLookup.Update(ref state);
                anchorStructuralChange = true;
            }

            var anchors = _anchorRefLookup[groupEntity];
            for (int i = anchors.Length - 1; i >= 0; i--)
            {
                if (anchors[i].Anchor == Entity.Null || !_entityInfoLookup.Exists(anchors[i].Anchor))
                {
                    anchors.RemoveAt(i);
                }
            }

            var needed = math.max(0, splitCount - 1);
            for (int i = anchors.Length - 1; i >= needed; i--)
            {
                if (_entityInfoLookup.Exists(anchors[i].Anchor))
                {
                    state.EntityManager.DestroyEntity(anchors[i].Anchor);
                    anchorStructuralChange = true;
                    refreshNeeded = true;
                }

                anchors.RemoveAt(i);
            }

            for (int i = anchors.Length; i < needed; i++)
            {
                var anchorEntity = state.EntityManager.CreateEntity();
                anchorStructuralChange = true;
                refreshNeeded = true;
                state.EntityManager.AddComponentData(anchorEntity, new WingFormationAnchor
                {
                    WingGroup = groupEntity,
                    AnchorIndex = (byte)(i + 1),
                    AnchorCount = (byte)splitCount
                });
                state.EntityManager.AddBuffer<FormationSlot>(anchorEntity);
                anchors.Add(new WingFormationAnchorRef { Anchor = anchorEntity });
            }

            if (refreshNeeded)
            {
                _entityInfoLookup.Update(ref state);
                _wingAnchorLookup.Update(ref state);
                _formationSlotLookup.Update(ref state);
                refreshNeeded = false;
            }

            for (int i = 0; i < anchors.Length; i++)
            {
                var anchorEntity = anchors[i].Anchor;
                if (anchorEntity == Entity.Null || !_entityInfoLookup.Exists(anchorEntity))
                {
                    continue;
                }

                if (!_wingAnchorLookup.HasComponent(anchorEntity))
                {
                    state.EntityManager.AddComponentData(anchorEntity, new WingFormationAnchor
                    {
                        WingGroup = groupEntity,
                        AnchorIndex = (byte)(i + 1),
                        AnchorCount = (byte)splitCount
                    });
                    anchorStructuralChange = true;
                    refreshNeeded = true;
                }
                else
                {
                    var anchor = _wingAnchorLookup[anchorEntity];
                    anchor.WingGroup = groupEntity;
                    anchor.AnchorIndex = (byte)(i + 1);
                    anchor.AnchorCount = (byte)splitCount;
                    _wingAnchorLookup[anchorEntity] = anchor;
                }

                if (!_formationSlotLookup.HasBuffer(anchorEntity))
                {
                    state.EntityManager.AddBuffer<FormationSlot>(anchorEntity);
                    anchorStructuralChange = true;
                    refreshNeeded = true;
                }
            }

            if (refreshNeeded)
            {
                _entityInfoLookup.Update(ref state);
                _wingAnchorLookup.Update(ref state);
                _formationSlotLookup.Update(ref state);
            }

            structuralChange |= anchorStructuralChange;
            return anchors;
        }

        private void EnsureFormationState(
            Entity anchorEntity,
            FormationType formationType,
            float spacing,
            float3 position,
            quaternion rotation,
            int maxSlots,
            uint tick,
            ref SystemState state,
            ref bool structuralChange)
        {
            var entityManager = state.EntityManager;
            var clampedSlots = (byte)math.clamp(maxSlots, 0, 255);
            if (!entityManager.HasComponent<FormationState>(anchorEntity))
            {
                entityManager.AddComponentData(anchorEntity, new FormationState
                {
                    Type = formationType,
                    AnchorPosition = position,
                    AnchorRotation = rotation,
                    Spacing = spacing,
                    Scale = 1f,
                    MaxSlots = clampedSlots,
                    FilledSlots = clampedSlots,
                    IsMoving = false,
                    LastUpdateTick = tick
                });
                structuralChange = true;
            }
            else
            {
                var formation = entityManager.GetComponentData<FormationState>(anchorEntity);
                formation.Type = formationType;
                formation.AnchorPosition = position;
                formation.AnchorRotation = rotation;
                formation.Spacing = spacing;
                formation.Scale = 1f;
                formation.MaxSlots = clampedSlots;
                formation.FilledSlots = clampedSlots;
                formation.LastUpdateTick = tick;
                entityManager.SetComponentData(anchorEntity, formation);
            }

            if (!entityManager.HasBuffer<FormationSlot>(anchorEntity))
            {
                entityManager.AddBuffer<FormationSlot>(anchorEntity);
                structuralChange = true;
            }
        }

        private void RefreshLookups(ref SystemState state)
        {
            _entityInfoLookup.Update(ref state);
            _groupMetaLookup.Update(ref state);
            _groupFormationLookup.Update(ref state);
            _wingStateLookup.Update(ref state);
            _tacticLookup.Update(ref state);
            _wingAnchorLookup.Update(ref state);
            _formationStateLookup.Update(ref state);
            _groupMemberLookup.Update(ref state);
            _anchorRefLookup.Update(ref state);
            _formationSlotLookup.Update(ref state);
            _wingDirectiveLookup.Update(ref state);
            _strikeProfileLookup.Update(ref state);
            _strikeStateLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _ackLookup.Update(ref state);
            _formationMemberLookup.Update(ref state);
            _strikePilotLookup.Update(ref state);
            _vesselPilotLookup.Update(ref state);
            _behaviorDispositionLookup.Update(ref state);
        }

        private void AssignFormationMember(
            Entity memberEntity,
            Entity formationEntity,
            int slotIndex,
            float arrivalThreshold,
            uint tick,
            ref SystemState state,
            ref EntityCommandBuffer ecb)
        {
            if (!_formationMemberLookup.HasComponent(memberEntity))
            {
                ecb.AddComponent(memberEntity, new FormationMember
                {
                    FormationEntity = formationEntity,
                    SlotIndex = (byte)slotIndex,
                    TargetPosition = float3.zero,
                    ArrivalThreshold = arrivalThreshold,
                    IsInPosition = false,
                    AssignedTick = tick
                });
                return;
            }

            var member = _formationMemberLookup[memberEntity];
            var reassigned = member.FormationEntity != formationEntity || member.SlotIndex != (byte)slotIndex;
            member.FormationEntity = formationEntity;
            member.SlotIndex = (byte)slotIndex;
            member.ArrivalThreshold = arrivalThreshold;
            if (reassigned)
            {
                member.AssignedTick = tick;
                member.IsInPosition = false;
            }

            _formationMemberLookup[memberEntity] = member;
        }

        private Entity ResolveGroupTarget(Entity groupEntity)
        {
            if (_strikeStateLookup.HasComponent(groupEntity))
            {
                var state = _strikeStateLookup[groupEntity];
                if (state.TargetEntity != Entity.Null)
                {
                    return state.TargetEntity;
                }
            }

            if (_strikeProfileLookup.HasComponent(groupEntity))
            {
                var profile = _strikeProfileLookup[groupEntity];
                if (profile.Target != Entity.Null)
                {
                    return profile.Target;
                }
            }

            return Entity.Null;
        }

        private quaternion ResolveAnchorRotation(Entity groupEntity, float3 leaderPosition, quaternion fallback)
        {
            var targetEntity = ResolveGroupTarget(groupEntity);
            if (targetEntity != Entity.Null && _transformLookup.HasComponent(targetEntity))
            {
                var targetPosition = _transformLookup[targetEntity].Position;
                var toTarget = targetPosition - leaderPosition;
                if (math.lengthsq(toTarget) > 0.001f)
                {
                    return quaternion.LookRotationSafe(math.normalize(toTarget), math.up());
                }
            }

            return fallback;
        }

        private float3 ResolveAnchorPosition(
            quaternion anchorRotation,
            float3 leaderPosition,
            in WingFormationDefaults defaults,
            SquadTacticKind tacticKind,
            int splitCount,
            int anchorIndex)
        {
            if (anchorIndex == 0)
            {
                return leaderPosition;
            }

            var arc = defaults.SplitArcDegrees;
            var startAngle = -arc * 0.5f;
            if (tacticKind == SquadTacticKind.FlankLeft)
            {
                startAngle = -arc;
            }
            else if (tacticKind == SquadTacticKind.FlankRight)
            {
                startAngle = 0f;
            }

            var step = splitCount > 1 ? arc / (splitCount - 1f) : 0f;
            var angle = startAngle + step * (anchorIndex - 1);
            var radians = math.radians(angle);
            var localOffset = new float3(math.sin(radians), 0f, math.cos(radians)) * defaults.SplitRadius;
            return leaderPosition + math.rotate(anchorRotation, localOffset);
        }

        private static bool WantsTight(SquadTacticKind kind)
        {
            return kind == SquadTacticKind.Tighten
                   || kind == SquadTacticKind.Collapse
                   || kind == SquadTacticKind.FlankLeft
                   || kind == SquadTacticKind.FlankRight;
        }

        private static bool IsSplitTactic(SquadTacticKind kind)
        {
            return kind == SquadTacticKind.FlankLeft || kind == SquadTacticKind.FlankRight;
        }
    }
}
