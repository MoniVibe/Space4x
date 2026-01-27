using PureDOTS.Runtime.Comms;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Groups;
using PureDOTS.Runtime.Interrupts;
using PureDOTS.Runtime.Perception;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Groups
{
    /// <summary>
    /// Updates squad cohesion state, emits tighten/loosen comms, and throttles opportunistic hauling when squads are tight.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GroupDecisionSystemGroup))]
    public partial struct SquadCohesionSystem : ISystem
    {
        private BufferLookup<CommsOutboxEntry> _outboxLookup;
        private ComponentLookup<SquadCohesionProfile> _profileLookup;
        private ComponentLookup<SquadCohesionState> _stateLookup;
        private ComponentLookup<GroupFormationSpread> _spreadLookup;
        private ComponentLookup<SquadTacticOrder> _tacticLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<SquadCohesionState>();
            state.RequireForUpdate<GroupFormationSpread>();
            _outboxLookup = state.GetBufferLookup<CommsOutboxEntry>(false);
            _profileLookup = state.GetComponentLookup<SquadCohesionProfile>(true);
            _stateLookup = state.GetComponentLookup<SquadCohesionState>(false);
            _spreadLookup = state.GetComponentLookup<GroupFormationSpread>(true);
            _tacticLookup = state.GetComponentLookup<SquadTacticOrder>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused
                || !SystemAPI.TryGetSingleton<RewindState>(out var rewindState)
                || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            _outboxLookup.Update(ref state);
            _profileLookup.Update(ref state);
            _stateLookup.Update(ref state);
            _spreadLookup.Update(ref state);
            _tacticLookup.Update(ref state);

            foreach (var (profile, spread, entity) in SystemAPI.Query<RefRO<SquadCohesionProfile>, RefRO<GroupFormationSpread>>().WithEntityAccess())
            {
                if (!_stateLookup.HasComponent(entity))
                {
                    continue;
                }

                var cohesionState = _stateLookup[entity];
                cohesionState.LastUpdateTick = timeState.Tick;

                var normalized = spread.ValueRO.CohesionNormalized;
                cohesionState.NormalizedCohesion = normalized;

                var wasTight = cohesionState.IsTight;
                var wasLoose = cohesionState.IsLoose;

                var isTight = normalized >= profile.ValueRO.TightThreshold01;
                var isLoose = normalized <= profile.ValueRO.LooseThreshold01;

                cohesionState.Flags = 0;
                if (isTight) cohesionState.Flags |= SquadCohesionState.FlagTight;
                if (isLoose) cohesionState.Flags |= SquadCohesionState.FlagLoose;

                var hasTactic = _tacticLookup.HasComponent(entity);
                var tactic = hasTactic ? _tacticLookup[entity] : default;

                if (hasTactic && tactic.IssueTick != 0 && tactic.IssueTick != cohesionState.LastBroadcastTick)
                {
                    EmitOrderComm(entity, profile.ValueRO, tactic);
                    cohesionState.LastBroadcastTick = tactic.IssueTick;
                }

                if (isTight && !wasTight)
                {
                    var requiresAck = hasTactic && tactic.AckMode != 0 && RequiresTightAck(tactic.Kind);
                    EmitStatusComm(entity, profile.ValueRO, (FixedString32Bytes)"status.tight", requiresAck);
                }
                else if (isLoose && !wasLoose)
                {
                    var requiresAck = hasTactic && tactic.AckMode != 0 && RequiresLooseAck(tactic.Kind);
                    EmitStatusComm(entity, profile.ValueRO, (FixedString32Bytes)"status.loose", requiresAck);
                }

                _stateLookup[entity] = cohesionState;
            }
        }

        private void EmitOrderComm(Entity squadEntity, in SquadCohesionProfile profile, in SquadTacticOrder tactic)
        {
            if (!_outboxLookup.HasBuffer(squadEntity))
            {
                return;
            }

            var payload = TacticPayload(tactic.Kind);
            if (payload.Length == 0)
            {
                return;
            }

            var outbox = _outboxLookup[squadEntity];
            var flags = CommsMessageFlags.IsBroadcast;
            if (tactic.AckMode != 0)
            {
                flags |= CommsMessageFlags.RequestsAck;
            }

            outbox.Add(new CommsOutboxEntry
            {
                Token = 0,
                InterruptType = InterruptType.CommsMessageReceived,
                Priority = InterruptPriority.High,
                PayloadId = payload,
                TransportMaskPreferred = tactic.AckMode != 0 ? PerceptionChannel.Vision : (PerceptionChannel.Hearing | PerceptionChannel.Vision),
                Strength01 = 0.85f,
                Clarity01 = 0.95f,
                DeceptionStrength01 = 0f,
                Secrecy01 = 0f,
                TtlTicks = 10,
                IntendedReceiver = Entity.Null,
                Flags = flags,
                FocusCost = tactic.FocusBudgetCost,
                MinCohesion01 = tactic.DisciplineRequired,
                RepeatCadenceTicks = 0,
                Attempts = 0,
                MaxAttempts = 0,
                NextEmitTick = 0,
                FirstEmitTick = 0
            });
        }

        private void EmitStatusComm(Entity squadEntity, in SquadCohesionProfile profile, FixedString32Bytes payloadId, bool requiresAck)
        {
            if (!_outboxLookup.HasBuffer(squadEntity))
            {
                return;
            }

            var outbox = _outboxLookup[squadEntity];
            var flags = CommsMessageFlags.IsBroadcast;
            if (requiresAck)
            {
                flags |= CommsMessageFlags.RequestsAck;
            }

            outbox.Add(new CommsOutboxEntry
            {
                Token = 0,
                InterruptType = InterruptType.CommsMessageReceived,
                Priority = InterruptPriority.Normal,
                PayloadId = payloadId,
                TransportMaskPreferred = requiresAck ? PerceptionChannel.Vision : (PerceptionChannel.Hearing | PerceptionChannel.Vision),
                Strength01 = 0.75f,
                Clarity01 = 0.9f,
                DeceptionStrength01 = 0f,
                Secrecy01 = 0f,
                TtlTicks = 5,
                IntendedReceiver = profile.CommandAuthority,
                Flags = flags,
                FocusCost = requiresAck ? profile.AckDisciplineRequirement : 0f,
                MinCohesion01 = requiresAck ? profile.AckDisciplineRequirement : 0f,
                RepeatCadenceTicks = 0,
                Attempts = 0,
                MaxAttempts = 0,
                NextEmitTick = 0,
                FirstEmitTick = 0
            });
        }

        private static bool RequiresTightAck(SquadTacticKind kind)
        {
            return kind == SquadTacticKind.Tighten
                   || kind == SquadTacticKind.Collapse
                   || kind == SquadTacticKind.FlankLeft
                   || kind == SquadTacticKind.FlankRight;
        }

        private static bool RequiresLooseAck(SquadTacticKind kind)
        {
            return kind == SquadTacticKind.Loosen
                   || kind == SquadTacticKind.Retreat;
        }

        private static FixedString32Bytes TacticPayload(SquadTacticKind kind)
        {
            return kind switch
            {
                SquadTacticKind.Tighten => (FixedString32Bytes)"tactic.tighten",
                SquadTacticKind.Loosen => (FixedString32Bytes)"tactic.loosen",
                SquadTacticKind.FlankLeft => (FixedString32Bytes)"tactic.flank.left",
                SquadTacticKind.FlankRight => (FixedString32Bytes)"tactic.flank.right",
                SquadTacticKind.Collapse => (FixedString32Bytes)"tactic.collapse",
                SquadTacticKind.Retreat => (FixedString32Bytes)"tactic.retreat",
                _ => default
            };
        }
    }

}
