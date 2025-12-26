using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Formation;
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
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XStrikeWingGroupSyncSystem))]
    [UpdateBefore(typeof(PureDOTS.Runtime.Systems.Formation.FormationSlotUpdateSystem))]
    public partial struct Space4XWingFormationPlannerSystem : ISystem
    {
        private ComponentLookup<StrikeCraftWingDirective> _wingDirectiveLookup;
        private ComponentLookup<StrikeCraftProfile> _strikeProfileLookup;
        private ComponentLookup<StrikeCraftState> _strikeStateLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<SquadAckState> _ackLookup;
        private ComponentLookup<FormationMember> _formationMemberLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<GroupFormation>();

            _wingDirectiveLookup = state.GetComponentLookup<StrikeCraftWingDirective>(true);
            _strikeProfileLookup = state.GetComponentLookup<StrikeCraftProfile>(true);
            _strikeStateLookup = state.GetComponentLookup<StrikeCraftState>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _ackLookup = state.GetComponentLookup<SquadAckState>(true);
            _formationMemberLookup = state.GetComponentLookup<FormationMember>(false);
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

            var formationConfig = WingFormationConfig.Default;
            if (SystemAPI.TryGetSingleton<WingFormationConfig>(out var formationSingleton))
            {
                formationConfig = formationSingleton;
            }

            _wingDirectiveLookup.Update(ref state);
            _strikeProfileLookup.Update(ref state);
            _strikeStateLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _ackLookup.Update(ref state);
            _formationMemberLookup.Update(ref state);

            var groupQuery = SystemAPI.QueryBuilder()
                .WithAll<GroupTag, GroupMeta, GroupFormation>()
                .WithAll<WingFormationState>()
                .WithAll<GroupMember>()
                .Build();

            var groups = groupQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < groups.Length; i++)
            {
                var groupEntity = groups[i];
                if (!state.EntityManager.HasComponent<GroupMeta>(groupEntity))
                {
                    continue;
                }

                var meta = state.EntityManager.GetComponentData<GroupMeta>(groupEntity);
                if (meta.Kind != GroupKind.StrikeWing && meta.Kind != GroupKind.MiningWing)
                {
                    continue;
                }

                var defaults = meta.Kind == GroupKind.StrikeWing
                    ? formationConfig.StrikeDefaults
                    : formationConfig.MiningDefaults;

                if (!state.EntityManager.HasBuffer<GroupMember>(groupEntity))
                {
                    continue;
                }

                var membersBuffer = state.EntityManager.GetBuffer<GroupMember>(groupEntity);
                if (membersBuffer.Length == 0)
                {
                    continue;
                }

                var tactic = EnsureTacticOrder(groupEntity, defaults, timeState.Tick, ref state);
                var ackRatio = ComputeAckRatio(tactic, membersBuffer);
                var acked = tactic.AckMode == 0 || ackRatio >= defaults.AckSuccessThreshold;

                var wantsTight = WantsTight(tactic.Kind);
                var spacing = wantsTight && acked ? defaults.TightSpacing : defaults.LooseSpacing;
                var cohesion = wantsTight && acked ? defaults.TightCohesion : defaults.LooseCohesion;
                var facing = wantsTight && acked ? defaults.TightFacingWeight : defaults.LooseFacingWeight;
                var formationType = wantsTight && acked ? defaults.TightFormation : defaults.LooseFormation;

                var groupFormation = state.EntityManager.GetComponentData<GroupFormation>(groupEntity);
                groupFormation.Spacing = spacing;
                groupFormation.Cohesion = cohesion;
                groupFormation.FacingWeight = facing;
                groupFormation.Type = wantsTight ? PureDOTS.Runtime.Groups.FormationType.Line : PureDOTS.Runtime.Groups.FormationType.Swarm;
                state.EntityManager.SetComponentData(groupEntity, groupFormation);

                var activeMembers = new NativeList<Entity>(membersBuffer.Length, Allocator.Temp);
                for (int m = 0; m < membersBuffer.Length; m++)
                {
                    var member = membersBuffer[m];
                    if ((member.Flags & GroupMemberFlags.Active) == 0)
                    {
                        continue;
                    }

                    if (member.MemberEntity == Entity.Null || !state.EntityManager.Exists(member.MemberEntity))
                    {
                        continue;
                    }

                    activeMembers.Add(member.MemberEntity);
                }

                if (activeMembers.Length == 0)
                {
                    activeMembers.Dispose();
                    continue;
                }

                byte splitCount = 1;
                if (acked && IsSplitTactic(tactic.Kind))
                {
                    splitCount = (byte)math.max(1, defaults.MaxSplitGroups);
                }

                splitCount = (byte)math.clamp(splitCount, 1, math.min(activeMembers.Length, 255));

                var wingState = state.EntityManager.GetComponentData<WingFormationState>(groupEntity);
                wingState.LastDecisionTick = tactic.IssueTick;
                wingState.LastTacticKind = (byte)tactic.Kind;
                wingState.SplitCount = splitCount;
                wingState.LastAckRatio = ackRatio;
                state.EntityManager.SetComponentData(groupEntity, wingState);

                var anchorRefs = EnsureAnchorRefs(groupEntity, splitCount, ref state);
                var anchorEntities = new NativeArray<Entity>(splitCount, Allocator.Temp);
                anchorEntities[0] = groupEntity;
                for (int a = 1; a < splitCount; a++)
                {
                    anchorEntities[a] = anchorRefs[a - 1].Anchor;
                }

                var anchorCounts = new NativeArray<int>(splitCount, Allocator.Temp);
                for (int m = 0; m < activeMembers.Length; m++)
                {
                    var anchorIndex = m % splitCount;
                    anchorCounts[anchorIndex]++;
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
                    EnsureFormationState(anchorEntity, formationType, spacing, anchorPosition, anchorRotation, anchorCounts[a], timeState.Tick, ref state);

                    var slots = state.EntityManager.GetBuffer<FormationSlot>(anchorEntity);
                    slots.Clear();
                }

                var slotIndices = new NativeArray<int>(splitCount, Allocator.Temp);
                var arrivalThreshold = math.max(0.5f, spacing * 0.25f);
                for (int m = 0; m < activeMembers.Length; m++)
                {
                    var memberEntity = activeMembers[m];
                    var anchorIndex = m % splitCount;
                    var anchorEntity = anchorEntities[anchorIndex];
                    var slotIndex = slotIndices[anchorIndex]++;

                    AssignFormationMember(memberEntity, anchorEntity, slotIndex, arrivalThreshold, timeState.Tick, ref state);

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
                anchorCounts.Dispose();
                anchorEntities.Dispose();
                activeMembers.Dispose();
            }

            groups.Dispose();
        }

        private SquadTacticOrder EnsureTacticOrder(Entity groupEntity, in WingFormationDefaults defaults, uint tick, ref SystemState state)
        {
            if (!state.EntityManager.HasComponent<SquadTacticOrder>(groupEntity))
            {
                state.EntityManager.AddComponentData(groupEntity, new SquadTacticOrder
                {
                    Kind = SquadTacticKind.None,
                    Issuer = Entity.Null,
                    Target = Entity.Null,
                    FocusBudgetCost = 0f,
                    DisciplineRequired = defaults.DisciplineRequired,
                    AckMode = 0,
                    IssueTick = 0
                });
            }

            var tactic = state.EntityManager.GetComponentData<SquadTacticOrder>(groupEntity);
            var desiredKind = tactic.Kind;
            var ackMode = tactic.AckMode;

            if (state.EntityManager.HasComponent<GroupMeta>(groupEntity))
            {
                var meta = state.EntityManager.GetComponentData<GroupMeta>(groupEntity);
                if (meta.Kind == GroupKind.StrikeWing && _wingDirectiveLookup.HasComponent(groupEntity))
                {
                    var directive = _wingDirectiveLookup[groupEntity];
                    desiredKind = directive.Mode == 0 ? SquadTacticKind.Tighten : SquadTacticKind.Loosen;
                    ackMode = desiredKind == SquadTacticKind.Tighten
                        ? defaults.RequireAckForTighten
                        : (byte)0;
                }
            }

            if (desiredKind != tactic.Kind || tactic.IssueTick == 0)
            {
                tactic.Kind = desiredKind;
                tactic.Issuer = groupEntity;
                tactic.Target = ResolveGroupTarget(groupEntity);
                tactic.DisciplineRequired = defaults.DisciplineRequired;
                tactic.AckMode = ackMode;
                tactic.IssueTick = tick;
                state.EntityManager.SetComponentData(groupEntity, tactic);
            }

            return tactic;
        }

        private float ComputeAckRatio(in SquadTacticOrder tactic, DynamicBuffer<GroupMember> members)
        {
            if (tactic.AckMode == 0 || tactic.IssueTick == 0)
            {
                return 1f;
            }

            var activeCount = 0;
            var ackCount = 0;
            for (int i = 0; i < members.Length; i++)
            {
                var member = members[i];
                if ((member.Flags & GroupMemberFlags.Active) == 0)
                {
                    continue;
                }

                activeCount++;
                if (_ackLookup.HasComponent(member.MemberEntity))
                {
                    var ack = _ackLookup[member.MemberEntity];
                    if (ack.LastAckTick == tactic.IssueTick)
                    {
                        ackCount++;
                    }
                }
            }

            if (activeCount == 0)
            {
                return 0f;
            }

            return ackCount / (float)activeCount;
        }

        private DynamicBuffer<WingFormationAnchorRef> EnsureAnchorRefs(Entity groupEntity, int splitCount, ref SystemState state)
        {
            if (!state.EntityManager.HasBuffer<WingFormationAnchorRef>(groupEntity))
            {
                state.EntityManager.AddBuffer<WingFormationAnchorRef>(groupEntity);
            }

            var anchors = state.EntityManager.GetBuffer<WingFormationAnchorRef>(groupEntity);
            for (int i = anchors.Length - 1; i >= 0; i--)
            {
                if (anchors[i].Anchor == Entity.Null || !state.EntityManager.Exists(anchors[i].Anchor))
                {
                    anchors.RemoveAt(i);
                }
            }

            var needed = math.max(0, splitCount - 1);
            for (int i = anchors.Length - 1; i >= needed; i--)
            {
                if (state.EntityManager.Exists(anchors[i].Anchor))
                {
                    state.EntityManager.DestroyEntity(anchors[i].Anchor);
                }

                anchors.RemoveAt(i);
            }

            for (int i = anchors.Length; i < needed; i++)
            {
                var anchorEntity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(anchorEntity, new WingFormationAnchor
                {
                    WingGroup = groupEntity,
                    AnchorIndex = (byte)(i + 1),
                    AnchorCount = (byte)splitCount
                });
                state.EntityManager.AddBuffer<FormationSlot>(anchorEntity);
                anchors.Add(new WingFormationAnchorRef { Anchor = anchorEntity });
            }

            for (int i = 0; i < anchors.Length; i++)
            {
                var anchorEntity = anchors[i].Anchor;
                if (anchorEntity == Entity.Null || !state.EntityManager.Exists(anchorEntity))
                {
                    continue;
                }

                if (!state.EntityManager.HasComponent<WingFormationAnchor>(anchorEntity))
                {
                    state.EntityManager.AddComponentData(anchorEntity, new WingFormationAnchor
                    {
                        WingGroup = groupEntity,
                        AnchorIndex = (byte)(i + 1),
                        AnchorCount = (byte)splitCount
                    });
                }
                else
                {
                    var anchor = state.EntityManager.GetComponentData<WingFormationAnchor>(anchorEntity);
                    anchor.WingGroup = groupEntity;
                    anchor.AnchorIndex = (byte)(i + 1);
                    anchor.AnchorCount = (byte)splitCount;
                    state.EntityManager.SetComponentData(anchorEntity, anchor);
                }
            }

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
            ref SystemState state)
        {
            var clampedSlots = (byte)math.clamp(maxSlots, 0, 255);
            if (!state.EntityManager.HasComponent<FormationState>(anchorEntity))
            {
                state.EntityManager.AddComponentData(anchorEntity, new FormationState
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
            }
            else
            {
                var formation = state.EntityManager.GetComponentData<FormationState>(anchorEntity);
                formation.Type = formationType;
                formation.AnchorPosition = position;
                formation.AnchorRotation = rotation;
                formation.Spacing = spacing;
                formation.Scale = 1f;
                formation.MaxSlots = clampedSlots;
                formation.FilledSlots = clampedSlots;
                formation.LastUpdateTick = tick;
                state.EntityManager.SetComponentData(anchorEntity, formation);
            }

            if (!state.EntityManager.HasBuffer<FormationSlot>(anchorEntity))
            {
                state.EntityManager.AddBuffer<FormationSlot>(anchorEntity);
            }
        }

        private void AssignFormationMember(
            Entity memberEntity,
            Entity formationEntity,
            int slotIndex,
            float arrivalThreshold,
            uint tick,
            ref SystemState state)
        {
            if (!_formationMemberLookup.HasComponent(memberEntity))
            {
                state.EntityManager.AddComponentData(memberEntity, new FormationMember
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
