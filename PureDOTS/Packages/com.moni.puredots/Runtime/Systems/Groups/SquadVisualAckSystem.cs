using PureDOTS.Runtime.AI;
using PureDOTS.Runtime.Comms;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Groups;
using PureDOTS.Runtime.Individual;
using PureDOTS.Runtime.Interrupts;
using PureDOTS.Runtime.Perception;
using PureDOTS.Runtime.Villagers;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Groups
{
    /// <summary>
    /// Emits vision-only acknowledgement comms when squads receive tighten/flank orders and members qualify.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GroupDecisionSystemGroup))]
    [UpdateAfter(typeof(SquadCohesionSystem))]
    public partial struct SquadVisualAckSystem : ISystem
    {
        private BufferLookup<GroupMember> _memberLookup;
        private ComponentLookup<SquadTacticOrder> _tacticLookup;
        private ComponentLookup<SquadCohesionState> _cohesionLookup;
        private BufferLookup<CommsOutboxEntry> _outboxLookup;
        private ComponentLookup<SquadAckState> _ackStateLookup;
        private ComponentLookup<AIAckConfig> _ackConfigLookup;
        private ComponentLookup<AlignmentTriplet> _alignmentLookup;
        private ComponentLookup<FocusBudget> _focusLookup;
        private ComponentLookup<ResourcePools> _poolsLookup;
        private ComponentLookup<VillagerNeedState> _needsLookup;
        private ComponentLookup<IndividualStats> _statsLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<SquadTacticOrder>();
            _memberLookup = state.GetBufferLookup<GroupMember>(true);
            _tacticLookup = state.GetComponentLookup<SquadTacticOrder>(true);
            _cohesionLookup = state.GetComponentLookup<SquadCohesionState>(true);
            _outboxLookup = state.GetBufferLookup<CommsOutboxEntry>(false);
            _ackStateLookup = state.GetComponentLookup<SquadAckState>(false);
            _ackConfigLookup = state.GetComponentLookup<AIAckConfig>(true);
            _alignmentLookup = state.GetComponentLookup<AlignmentTriplet>(true);
            _focusLookup = state.GetComponentLookup<FocusBudget>(true);
            _poolsLookup = state.GetComponentLookup<ResourcePools>(true);
            _needsLookup = state.GetComponentLookup<VillagerNeedState>(true);
            _statsLookup = state.GetComponentLookup<IndividualStats>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused
                || !SystemAPI.TryGetSingleton<RewindState>(out var rewind)
                || rewind.Mode != RewindMode.Record)
            {
                return;
            }

            _memberLookup.Update(ref state);
            _tacticLookup.Update(ref state);
            _cohesionLookup.Update(ref state);
            _outboxLookup.Update(ref state);
            _ackStateLookup.Update(ref state);
            _ackConfigLookup.Update(ref state);
            _alignmentLookup.Update(ref state);
            _focusLookup.Update(ref state);
            _poolsLookup.Update(ref state);
            _needsLookup.Update(ref state);
            _statsLookup.Update(ref state);

            foreach (var (tactic, entity) in SystemAPI.Query<RefRO<SquadTacticOrder>>().WithEntityAccess())
            {
                if (tactic.ValueRO.Kind == SquadTacticKind.None || tactic.ValueRO.AckMode == 0)
                {
                    continue;
                }

                if (!_cohesionLookup.HasComponent(entity)
                    || !_memberLookup.HasBuffer(entity))
                {
                    continue;
                }

                var cohesion = _cohesionLookup[entity];
                if (!cohesion.IsTight || cohesion.NormalizedCohesion < tactic.ValueRO.DisciplineRequired)
                {
                    continue;
                }

                var members = _memberLookup[entity];
                for (int i = 0; i < members.Length; i++)
                {
                    var memberEntity = members[i].MemberEntity;
                    if (!_ackStateLookup.HasComponent(memberEntity)
                        || !_outboxLookup.HasBuffer(memberEntity)
                        || !_ackConfigLookup.HasComponent(memberEntity))
                    {
                        continue;
                    }

                    var ackState = _ackStateLookup[memberEntity];
                    if (ackState.LastAckTick == tactic.ValueRO.IssueTick)
                    {
                        continue;
                    }

                    var ackConfig = _ackConfigLookup[memberEntity];
                    var hasAlignment = _alignmentLookup.HasComponent(memberEntity);
                    var alignment = hasAlignment ? _alignmentLookup[memberEntity] : default;
                    var hasFocus = _focusLookup.HasComponent(memberEntity);
                    var focus = hasFocus ? _focusLookup[memberEntity] : default;
                    var hasPools = _poolsLookup.HasComponent(memberEntity);
                    var pools = hasPools ? _poolsLookup[memberEntity] : default;
                    var hasNeeds = _needsLookup.HasComponent(memberEntity);
                    var needs = hasNeeds ? _needsLookup[memberEntity] : default;
                    var hasStats = _statsLookup.HasComponent(memberEntity);
                    var stats = hasStats ? _statsLookup[memberEntity] : default;

                    var chaos01 = hasAlignment ? AIAckPolicyUtility.ComputeChaos01(alignment) : 0f;
                    var focusRatio = AIAckPolicyUtility.ComputeFocusRatio01(hasFocus, focus, hasPools, pools);
                    var sleepPressure = AIAckPolicyUtility.ComputeSleepPressure01(hasNeeds, needs, hasPools, pools, hasStats, stats);

                    if (!AIAckPolicyUtility.ShouldEmitReceiptAcks(ackConfig, focusRatio, sleepPressure, chaos01, tactic.ValueRO.IssueTick))
                    {
                        continue;
                    }

                    ackState.LastAckTick = tactic.ValueRO.IssueTick;
                    _ackStateLookup[memberEntity] = ackState;

                    var outbox = _outboxLookup[memberEntity];
                    outbox.Add(new CommsOutboxEntry
                    {
                        Token = 0,
                        InterruptType = InterruptType.CommsAckReceived,
                        Priority = InterruptPriority.Normal,
                        PayloadId = (FixedString32Bytes)"ack.visual",
                        TransportMaskPreferred = PerceptionChannel.Vision,
                        Strength01 = 0.45f,
                        Clarity01 = 0.85f,
                        DeceptionStrength01 = 0f,
                        Secrecy01 = 0f,
                        TtlTicks = 4,
                        IntendedReceiver = tactic.ValueRO.Issuer,
                        Flags = CommsMessageFlags.None,
                        FocusCost = tactic.ValueRO.FocusBudgetCost,
                        MinCohesion01 = tactic.ValueRO.DisciplineRequired,
                        RepeatCadenceTicks = 0,
                        Attempts = 0,
                        MaxAttempts = 0,
                        NextEmitTick = 0,
                        FirstEmitTick = 0
                    });
                }
            }
        }
    }
}

