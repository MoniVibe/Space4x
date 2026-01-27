using PureDOTS.Runtime.AI;
using PureDOTS.Runtime.Comms;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Individual;
using PureDOTS.Runtime.Interrupts;
using PureDOTS.Runtime.Perception;
using PureDOTS.Runtime.Performance;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Comms
{
    /// <summary>
    /// Delivers targeted Hearing/EM comm messages directly to the intended receiver.
    /// Uses receiver SignalPerceptionState (medium-first) instead of relying on threshold interrupts,
    /// enabling repeat->yield and long arguments deterministically.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PerceptionSystemGroup))]
    [UpdateAfter(typeof(PureDOTS.Systems.Perception.PerceptionSignalSamplingSystem))]
    public partial struct CommsTargetedMediumDeliverySystem : ISystem
    {
        private ComponentLookup<CommsReceiverConfig> _receiverLookup;
        private ComponentLookup<SignalPerceptionState> _signalLookup;
        private ComponentLookup<IndividualStats> _statsLookup;
        private ComponentLookup<PersonalityAxes> _personalityLookup;
        private ComponentLookup<FocusBudget> _focusLookup;
        private BufferLookup<Interrupt> _interruptLookup;
        private BufferLookup<CommsInboxEntry> _inboxLookup;
        private BufferLookup<CommsOutboxEntry> _outboxLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<CommsMessageStreamTag>();
            state.RequireForUpdate<UniversalPerformanceBudget>();

            _receiverLookup = state.GetComponentLookup<CommsReceiverConfig>(true);
            _signalLookup = state.GetComponentLookup<SignalPerceptionState>(true);
            _statsLookup = state.GetComponentLookup<IndividualStats>(true);
            _personalityLookup = state.GetComponentLookup<PersonalityAxes>(true);
            _focusLookup = state.GetComponentLookup<FocusBudget>(false);
            _interruptLookup = state.GetBufferLookup<Interrupt>(false);
            _inboxLookup = state.GetBufferLookup<CommsInboxEntry>(false);
            _outboxLookup = state.GetBufferLookup<CommsOutboxEntry>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            if (time.IsPaused)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewind) || rewind.Mode != RewindMode.Record)
            {
                return;
            }

            var commEntity = SystemAPI.GetSingletonEntity<CommsMessageStreamTag>();
            var comms = state.EntityManager.GetBuffer<CommsMessage>(commEntity);
            if (comms.Length == 0)
            {
                return;
            }

            var budget = SystemAPI.GetSingleton<UniversalPerformanceBudget>();
            var remaining = math.max(0, budget.MaxCommsMessagesPerTick);

            var countersRW = SystemAPI.HasSingleton<UniversalPerformanceCounters>()
                ? SystemAPI.GetSingletonRW<UniversalPerformanceCounters>()
                : default;
            var diagnosticsRW = SystemAPI.HasSingleton<CommsDeliveryDiagnostics>()
                ? SystemAPI.GetSingletonRW<CommsDeliveryDiagnostics>()
                : default;
            var hasDiagnostics = diagnosticsRW.IsValid;

            _receiverLookup.Update(ref state);
            _signalLookup.Update(ref state);
            _statsLookup.Update(ref state);
            _personalityLookup.Update(ref state);
            _focusLookup.Update(ref state);
            _interruptLookup.Update(ref state);
            _inboxLookup.Update(ref state);
            _outboxLookup.Update(ref state);

            for (int mi = 0; mi < comms.Length && remaining > 0; mi++)
            {
                var message = comms[mi];
                if (time.Tick < message.EmittedTick || time.Tick > message.ExpirationTick)
                {
                    if (hasDiagnostics && message.IntendedReceiver != Entity.Null)
                    {
                        diagnosticsRW.ValueRW.TargetedExpired++;
                    }
                    continue;
                }

                if (message.TransportUsed != PerceptionChannel.Hearing && message.TransportUsed != PerceptionChannel.EM)
                {
                    if (hasDiagnostics)
                    {
                        diagnosticsRW.ValueRW.TargetedWrongTransport++;
                    }
                    continue;
                }

                if (message.IntendedReceiver == Entity.Null)
                {
                    // Broadcast delivery is handled by signal interrupts / higher-level systems.
                    continue;
                }

                if (hasDiagnostics)
                {
                    diagnosticsRW.ValueRW.TargetedConsidered++;
                }

                var receiverEntity = message.IntendedReceiver;
                if (!_receiverLookup.HasComponent(receiverEntity))
                {
                    if (hasDiagnostics)
                    {
                        diagnosticsRW.ValueRW.TargetedMissingReceiverConfig++;
                    }
                    continue;
                }

                if (!_interruptLookup.HasBuffer(receiverEntity))
                {
                    if (hasDiagnostics)
                    {
                        diagnosticsRW.ValueRW.TargetedMissingInterrupt++;
                    }
                    continue;
                }

                var receiver = _receiverLookup[receiverEntity];
                if (receiver.Enabled == 0 || (receiver.TransportMask & message.TransportUsed) == 0)
                {
                    if (hasDiagnostics)
                    {
                        diagnosticsRW.ValueRW.TargetedReceiverDisabled++;
                    }
                    continue;
                }

                if (!_signalLookup.HasComponent(receiverEntity))
                {
                    if (hasDiagnostics)
                    {
                        diagnosticsRW.ValueRW.TargetedMissingSignal++;
                    }
                    continue;
                }

                var signalState = _signalLookup[receiverEntity];
                var signal01 = message.TransportUsed == PerceptionChannel.EM
                    ? math.saturate(signalState.EMLevel) * math.saturate(signalState.EMConfidence)
                    : math.saturate(signalState.SoundLevel) * math.saturate(signalState.SoundConfidence);

                // Targeted delivery no longer gates on focus stored in the message payload.

                var integrity01 = math.saturate(signal01 * message.Clarity01 * math.saturate(receiver.DecodeSkill) - math.saturate(receiver.NoiseFloor));

                var interrupts = _interruptLookup[receiverEntity];

                // Dedupe + repeat count based on emitted tick.
                var repeatCount = (byte)1;
                if (_inboxLookup.HasBuffer(receiverEntity))
                {
                    var inbox = _inboxLookup[receiverEntity];
                    for (int ii = inbox.Length - 1; ii >= 0; ii--)
                    {
                        if (inbox[ii].Token == message.Token && inbox[ii].SourceEmittedTick == message.EmittedTick)
                        {
                            if (hasDiagnostics)
                            {
                                diagnosticsRW.ValueRW.TargetedDuplicateEmission++;
                            }
                            goto NextMessage; // already processed this emission
                        }

                        if (inbox[ii].Token == message.Token)
                        {
                            repeatCount = (byte)math.min(255, inbox[ii].RepeatCount + 1);
                            break;
                        }
                    }
                }

                // Always emit "message received".
                InterruptUtils.Emit(
                    ref interrupts,
                    InterruptType.CommsMessageReceived,
                    InterruptPriority.Low,
                    message.Sender,
                    time.Tick,
                    targetEntity: receiverEntity,
                    targetPosition: message.Origin,
                    payloadValue: integrity01,
                    payloadId: TokenToPayload(message.Token));

                // Ack receipts clear repeats.
                if (message.InterruptType == InterruptType.CommsAckReceived)
                {
                    if (_outboxLookup.HasBuffer(receiverEntity))
                    {
                        var outbox = _outboxLookup[receiverEntity];
                        for (int oi = outbox.Length - 1; oi >= 0; oi--)
                        {
                            var e = outbox[oi];
                            if (e.Token == message.Token && (e.Flags & CommsMessageFlags.RequestsAck) != 0)
                            {
                                outbox.RemoveAtSwapBack(oi);
                            }
                        }
                    }

                    InterruptUtils.Emit(
                        ref interrupts,
                        InterruptType.CommsAckReceived,
                        InterruptPriority.Low,
                        message.Sender,
                        time.Tick,
                        payloadValue: 1f,
                        payloadId: TokenToPayload(message.Token));
                }
                else
                {
                    var acceptThreshold = 0.55f;
                    var intellect01 = 0.5f;
                    if (_statsLookup.HasComponent(receiverEntity))
                    {
                        intellect01 = math.saturate(_statsLookup[receiverEntity].Intellect / 10f);
                    }

                    var conviction01 = 0.5f;
                    if (_personalityLookup.HasComponent(receiverEntity))
                    {
                        conviction01 = math.saturate((_personalityLookup[receiverEntity].Conviction + 1f) * 0.5f);
                    }

                    var yieldThreshold = (byte)math.clamp(2 + (int)math.round((1f - intellect01) * 4f + conviction01 * 8f), 2, 30);
                    var accepted = integrity01 >= acceptThreshold || repeatCount >= yieldThreshold;

                    if (accepted && message.InterruptType != InterruptType.None)
                    {
                        var payload = message.PayloadId.IsEmpty ? TokenToPayload(message.Token) : message.PayloadId;
                        InterruptUtils.Emit(
                            ref interrupts,
                            message.InterruptType,
                            message.Priority,
                            message.Sender,
                            time.Tick,
                            targetEntity: receiverEntity,
                            targetPosition: message.Origin,
                            payloadValue: integrity01,
                            payloadId: payload);

                        if ((message.Flags & CommsMessageFlags.RequestsAck) != 0 && _outboxLookup.HasBuffer(receiverEntity))
                        {
                            var outbox = _outboxLookup[receiverEntity];
                            var alreadyQueued = false;
                            for (int oi = outbox.Length - 1; oi >= 0; oi--)
                            {
                                if (outbox[oi].Token == message.Token && outbox[oi].InterruptType == InterruptType.CommsAckReceived)
                                {
                                    alreadyQueued = true;
                                    break;
                                }
                            }

                            if (!alreadyQueued)
                            {
                                outbox.Add(new CommsOutboxEntry
                                {
                                    Token = message.Token,
                                    InterruptType = InterruptType.CommsAckReceived,
                                    Priority = InterruptPriority.Low,
                                    PayloadId = default,
                                    TransportMaskPreferred = message.TransportUsed,
                                    Strength01 = 0.5f,
                                    Clarity01 = 1f,
                                    DeceptionStrength01 = 0f,
                                    Secrecy01 = 0f,
                                    TtlTicks = 10,
                                    IntendedReceiver = message.Sender,
                                    Flags = CommsMessageFlags.None,
                                    FocusCost = 0f,
                                    MinCohesion01 = 0f,
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

                // Store inbox entry (optional).
                if (_inboxLookup.HasBuffer(receiverEntity))
                {
                    var inbox = _inboxLookup[receiverEntity];
                    if (inbox.Length >= receiver.MaxInbox && receiver.MaxInbox > 0)
                    {
                        inbox.RemoveAt(0);
                    }
                    inbox.Add(new CommsInboxEntry
                    {
                        ReceivedTick = time.Tick,
                        SourceEmittedTick = message.EmittedTick,
                        Token = message.Token,
                        Sender = message.Sender,
                        Origin = message.Origin,
                        IntendedInterrupt = message.InterruptType,
                        Priority = message.Priority,
                        PayloadId = message.PayloadId.IsEmpty ? TokenToPayload(message.Token) : message.PayloadId,
                        TransportUsed = message.TransportUsed,
                        Integrity01 = integrity01,
                        MisreadSeverity = MiscommunicationSeverity.None,
                        MisreadType = MiscommunicationType.None,
                        WasDeceptionDetected = 0,
                        WasProcessed = 0,
                        RepeatCount = repeatCount
                    });
                }

                if (hasDiagnostics)
                {
                    diagnosticsRW.ValueRW.TargetedDelivered++;
                }

                remaining--;
                if (countersRW.IsValid)
                {
                    countersRW.ValueRW.CommsReceiptsThisTick++;
                    countersRW.ValueRW.TotalWarmOperationsThisTick++;
                }

                NextMessage: ;
            }
        }

        private static FixedString32Bytes TokenToPayload(uint token)
        {
            var s = new FixedString32Bytes();
            s.Append('c');
            s.Append(':');
            s.Append(token);
            return s;
        }
    }
}



