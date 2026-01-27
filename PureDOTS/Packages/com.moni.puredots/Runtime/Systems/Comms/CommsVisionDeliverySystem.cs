using PureDOTS.Runtime.AI;
using PureDOTS.Runtime.Comms;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Individual;
using PureDOTS.Runtime.Interrupts;
using PureDOTS.Runtime.Perception;
using PureDOTS.Runtime.Performance;
using PureDOTS.Runtime.Spatial;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Comms
{
    /// <summary>
    /// Delivers targeted Vision comm messages ("eye contact") when the receiver is actively perceiving the sender via Vision,
    /// and can spend the message FocusCost. Cohesion is derived from PerceivedEntity.Relationship and compared to MinCohesion01.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PerceptionSystemGroup))]
    [UpdateAfter(typeof(PureDOTS.Systems.Perception.PerceptionUpdateSystem))]
    public partial struct CommsVisionDeliverySystem : ISystem
    {
        private ComponentLookup<CommsReceiverConfig> _receiverLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<IndividualStats> _statsLookup;
        private ComponentLookup<PersonalityAxes> _personalityLookup;
        private ComponentLookup<FocusBudget> _focusLookup;
        private BufferLookup<PerceivedEntity> _perceivedLookup;
        private BufferLookup<Interrupt> _interruptLookup;
        private BufferLookup<CommsInboxEntry> _inboxLookup;
        private BufferLookup<CommsOutboxEntry> _outboxLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<SpatialGridConfig>();
            state.RequireForUpdate<CommsMessageStreamTag>();
            state.RequireForUpdate<UniversalPerformanceBudget>();

            _receiverLookup = state.GetComponentLookup<CommsReceiverConfig>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _statsLookup = state.GetComponentLookup<IndividualStats>(true);
            _personalityLookup = state.GetComponentLookup<PersonalityAxes>(true);
            _focusLookup = state.GetComponentLookup<FocusBudget>(false);
            _perceivedLookup = state.GetBufferLookup<PerceivedEntity>(true);
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

            var gridConfig = SystemAPI.GetSingleton<SpatialGridConfig>();
            var budget = SystemAPI.GetSingleton<UniversalPerformanceBudget>();
            var remaining = math.max(0, budget.MaxCommsMessagesPerTick);

            var countersRW = SystemAPI.HasSingleton<UniversalPerformanceCounters>()
                ? SystemAPI.GetSingletonRW<UniversalPerformanceCounters>()
                : default;

            _receiverLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _statsLookup.Update(ref state);
            _personalityLookup.Update(ref state);
            _focusLookup.Update(ref state);
            _perceivedLookup.Update(ref state);
            _interruptLookup.Update(ref state);
            _inboxLookup.Update(ref state);
            _outboxLookup.Update(ref state);

            for (int mi = 0; mi < comms.Length && remaining > 0; mi++)
            {
                var message = comms[mi];
                if (time.Tick < message.EmittedTick || time.Tick > message.ExpirationTick)
                {
                    continue;
                }

                if (message.TransportUsed != PerceptionChannel.Vision)
                {
                    continue;
                }

                if (message.IntendedReceiver == Entity.Null)
                {
                    continue;
                }

                var receiverEntity = message.IntendedReceiver;
                if (!_receiverLookup.HasComponent(receiverEntity) ||
                    !_interruptLookup.HasBuffer(receiverEntity) ||
                    !_perceivedLookup.HasBuffer(receiverEntity))
                {
                    continue;
                }

                var receiver = _receiverLookup[receiverEntity];
                if (receiver.Enabled == 0 || (receiver.TransportMask & PerceptionChannel.Vision) == 0)
                {
                    continue;
                }

                // Must be currently perceived via Vision.
                var perceived = _perceivedLookup[receiverEntity];
                if (!TryFindPerception(perceived, message.Sender, out var p))
                {
                    continue;
                }

                if ((p.DetectedChannels & PerceptionChannel.Vision) == 0)
                {
                    continue;
                }

                // Vision deliveries no longer gate on cohesion/focus stored in the message payload.

                var signal01 = math.saturate(p.Confidence);
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
                            goto NextMessage;
                        }
                        if (inbox[ii].Token == message.Token)
                        {
                            repeatCount = (byte)math.min(255, inbox[ii].RepeatCount + 1);
                            break;
                        }
                    }
                }

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

                var acceptThreshold = 0.55f;
                var intellect01 = _statsLookup.HasComponent(receiverEntity) ? math.saturate(_statsLookup[receiverEntity].Intellect / 10f) : 0.5f;
                var conviction01 = _personalityLookup.HasComponent(receiverEntity) ? math.saturate((_personalityLookup[receiverEntity].Conviction + 1f) * 0.5f) : 0.5f;
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
                                TransportMaskPreferred = PerceptionChannel.Vision,
                                Strength01 = 0.4f,
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

                remaining--;
                if (countersRW.IsValid)
                {
                    countersRW.ValueRW.CommsReceiptsThisTick++;
                    countersRW.ValueRW.TotalWarmOperationsThisTick++;
                }

                NextMessage: ;
            }
        }

        private static bool TryFindPerception(DynamicBuffer<PerceivedEntity> perceived, Entity sender, out PerceivedEntity entry)
        {
            for (int i = 0; i < perceived.Length; i++)
            {
                var p = perceived[i];
                if (p.TargetEntity == sender)
                {
                    entry = p;
                    return true;
                }
            }

            entry = default;
            return false;
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





