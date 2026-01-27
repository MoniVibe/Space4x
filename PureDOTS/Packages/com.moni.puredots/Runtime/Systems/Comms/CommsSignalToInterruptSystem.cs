using PureDOTS.Runtime.Comms;
using PureDOTS.Runtime.AI;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Interrupts;
using PureDOTS.Runtime.Deception;
using PureDOTS.Runtime.Individual;
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
    /// When a receiver gets a Sound/EM signal interrupt, try to match a recent comm message from the same cell,
    /// then emit a decoded interrupt (usually NewOrder) and mark the raw signal interrupt as processed.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InterruptSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(PureDOTS.Systems.Interrupts.InterruptHandlerSystem))]
    public partial struct CommsSignalToInterruptSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<CommsReceiverConfig> _receiverLookup;
        private ComponentLookup<IndividualStats> _statsLookup;
        private ComponentLookup<PersonalityAxes> _personalityLookup;
        private ComponentLookup<FocusBudget> _focusLookup;
        private ComponentLookup<DisguiseIdentity> _disguiseLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<SpatialGridConfig>();
            state.RequireForUpdate<CommsMessageStreamTag>();

            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _receiverLookup = state.GetComponentLookup<CommsReceiverConfig>(true);
            _statsLookup = state.GetComponentLookup<IndividualStats>(true);
            _personalityLookup = state.GetComponentLookup<PersonalityAxes>(true);
            _focusLookup = state.GetComponentLookup<FocusBudget>(false);
            _disguiseLookup = state.GetComponentLookup<DisguiseIdentity>(true);
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

            _transformLookup.Update(ref state);
            _receiverLookup.Update(ref state);
            _statsLookup.Update(ref state);
            _personalityLookup.Update(ref state);
            _focusLookup.Update(ref state);
            _disguiseLookup.Update(ref state);

            var gridConfig = SystemAPI.GetSingleton<SpatialGridConfig>();
            var commEntity = SystemAPI.GetSingletonEntity<CommsMessageStreamTag>();
            var comms = state.EntityManager.GetBuffer<CommsMessage>(commEntity);
            if (comms.Length == 0)
            {
                return;
            }

            var budget = SystemAPI.HasSingleton<UniversalPerformanceBudget>()
                ? SystemAPI.GetSingleton<UniversalPerformanceBudget>()
                : UniversalPerformanceBudget.CreateDefaults();

            var countersRW = SystemAPI.HasSingleton<UniversalPerformanceCounters>()
                ? SystemAPI.GetSingletonRW<UniversalPerformanceCounters>()
                : default;

            var remaining = math.max(0, budget.MaxCommsMessagesPerTick);

            foreach (var (intent, entity) in SystemAPI.Query<RefRO<EntityIntent>>().WithEntityAccess())
            {
                if (remaining <= 0)
                {
                    break;
                }

                if (!_transformLookup.HasComponent(entity) || !_receiverLookup.HasComponent(entity))
                {
                    continue;
                }

                var receiver = _receiverLookup[entity];
                if (receiver.Enabled == 0)
                {
                    continue;
                }

                if (!SystemAPI.HasBuffer<Interrupt>(entity))
                {
                    continue;
                }

                var interrupts = SystemAPI.GetBuffer<Interrupt>(entity);
                if (interrupts.Length == 0)
                {
                    continue;
                }

                var pos = _transformLookup[entity].Position;
                SpatialHash.Quantize(pos, gridConfig, out var cellCoords);
                var cellId = SpatialHash.Flatten(in cellCoords, in gridConfig);
                if ((uint)cellId >= (uint)gridConfig.CellCount)
                {
                    continue;
                }

                for (int i = 0; i < interrupts.Length; i++)
                {
                    if (remaining <= 0)
                    {
                        break;
                    }

                    var interrupt = interrupts[i];
                    if (interrupt.IsProcessed != 0)
                    {
                        continue;
                    }

                    var isSignal = interrupt.Type == InterruptType.SoundSignalDetected || interrupt.Type == InterruptType.EMSignalDetected;
                    if (!isSignal)
                    {
                        continue;
                    }

                    var transport = interrupt.Type == InterruptType.EMSignalDetected ? PerceptionChannel.EM : PerceptionChannel.Hearing;
                    if ((receiver.TransportMask & transport) == 0)
                    {
                        continue;
                    }

                    if (!TryFindBestMessage(comms, entity, cellId, transport, time.Tick, out var message))
                    {
                        continue;
                    }

                    // Ack receipts: clear pending repeat entries for this token.
                    if (message.InterruptType == InterruptType.CommsAckReceived)
                    {
                        if (SystemAPI.HasBuffer<CommsOutboxEntry>(entity))
                        {
                            var outbox = SystemAPI.GetBuffer<CommsOutboxEntry>(entity);
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
                            targetEntity: Entity.Null,
                            targetPosition: message.Origin,
                            payloadValue: 1f,
                            payloadId: TokenToPayload(message.Token));

                        interrupt.IsProcessed = 1;
                        interrupts[i] = interrupt;
                        remaining--;
                        if (countersRW.IsValid)
                        {
                            countersRW.ValueRW.CommsReceiptsThisTick++;
                            countersRW.ValueRW.TotalWarmOperationsThisTick++;
                        }
                        continue;
                    }

                    // Decode: integrity based on signal payload (interrupt payload value) × message clarity × receiver skill.
                    var signal01 = math.saturate(interrupt.PayloadValue);
                    var integrity01 = math.saturate(signal01 * message.Clarity01 * math.saturate(receiver.DecodeSkill) - math.saturate(receiver.NoiseFloor));

                    var wasDeceptionDetected = (byte)0;
                    var misType = MiscommunicationType.None;
                    var misSeverity = MiscommunicationSeverity.None;

                    var deceptive = message.DeceptionStrength01 > 0.01f || (message.Flags & CommsMessageFlags.IsDeceptive) != 0;
                    if (deceptive)
                    {
                        var liarSkill01 = 0.5f;
                        if (_disguiseLookup.HasComponent(message.Sender) && _disguiseLookup[message.Sender].IsActive != 0)
                        {
                            liarSkill01 = math.saturate(_disguiseLookup[message.Sender].LieSkill01);
                        }
                        else if (_statsLookup.HasComponent(message.Sender))
                        {
                            liarSkill01 = math.saturate(_statsLookup[message.Sender].Social / 10f);
                        }

                        // Higher liar skill reduces detection odds; still deterministic.
                        var detectChance =
                            math.saturate(receiver.DeceptionDetectSkill) *
                            math.saturate(message.DeceptionStrength01) *
                            (1f - liarSkill01 * 0.6f);
                        var h = math.hash(new uint2(message.Token, (uint)entity.Index));
                        var r01 = (h & 0x00FFFFFFu) / 16777216f;
                        if (r01 < detectChance)
                        {
                            wasDeceptionDetected = 1;
                            misType = MiscommunicationType.DeceptionFalsePositive; // "caught a lie" (even if it's just suspect).
                        }
                        else
                        {
                            misType = MiscommunicationType.DeceptionUndetected;
                        }
                    }

                    // Secrecy/encryption can cause message loss unless bypassed.
                    if (message.Secrecy01 > 0.01f || (message.Flags & CommsMessageFlags.IsEncrypted) != 0)
                    {
                        var bypass = math.saturate(receiver.SecrecyBypassSkill);
                        var effectiveSecrecy = math.saturate(message.Secrecy01 * (1f - bypass));
                        if (effectiveSecrecy > 0.5f && integrity01 < 0.25f)
                        {
                            misType = MiscommunicationType.MessageLost;
                            misSeverity = MiscommunicationSeverity.Major;
                            // Mark raw signal processed to avoid Custom0 intent spam.
                            interrupt.IsProcessed = 1;
                            interrupts[i] = interrupt;
                            remaining--;
                            if (countersRW.IsValid)
                            {
                                countersRW.ValueRW.CommsReceiptsThisTick++;
                                countersRW.ValueRW.TotalWarmOperationsThisTick++;
                            }
                            continue;
                        }
                    }

                    // Misread classification (cheap, deterministic).
                    var misChance = math.saturate(receiver.MisreadChanceScale) * (1f - integrity01);
                    if (misChance > 0.01f)
                    {
                        var h = math.hash(new uint3(message.Token, (uint)entity.Index, 0xC0FFEEu));
                        var r01 = (h & 0x00FFFFFFu) / 16777216f;
                        if (r01 < misChance)
                        {
                            misType = misType == MiscommunicationType.None ? MiscommunicationType.IntentMisread : misType;
                            misSeverity = integrity01 < 0.15f ? MiscommunicationSeverity.Critical
                                : integrity01 < 0.3f ? MiscommunicationSeverity.Major
                                : MiscommunicationSeverity.Moderate;
                        }
                    }

                    // Emit decoded interrupt (keeps creative passes flexible via PayloadId + Token).
                    var payload = message.PayloadId;
                    var payloadValue = integrity01;

                    // Encode token into payloadId when empty (so receivers can correlate).
                    if (payload.IsEmpty)
                    {
                        payload = TokenToPayload(message.Token);
                    }

                    // Repeat/yield tracking via small inbox ring (optional).
                    var repeatCount = (byte)1;
                    if (SystemAPI.HasBuffer<CommsInboxEntry>(entity))
                    {
                        var inbox = SystemAPI.GetBuffer<CommsInboxEntry>(entity);
                        for (int ii = inbox.Length - 1; ii >= 0; ii--)
                        {
                            if (inbox[ii].Token == message.Token && inbox[ii].SourceEmittedTick == message.EmittedTick)
                            {
                                // Already processed this emission; skip to avoid duplicate interrupts this tick.
                                interrupt.IsProcessed = 1;
                                interrupts[i] = interrupt;
                                remaining--;
                                if (countersRW.IsValid)
                                {
                                    countersRW.ValueRW.CommsReceiptsThisTick++;
                                    countersRW.ValueRW.TotalWarmOperationsThisTick++;
                                }
                                goto NextInterrupt;
                            }
                            if (inbox[ii].Token == message.Token)
                            {
                                repeatCount = (byte)math.min(255, inbox[ii].RepeatCount + 1);
                                break;
                            }
                        }
                    }

                    // Always emit "message received" (cheap hook for tactics/strategy layers).
                    InterruptUtils.Emit(
                        ref interrupts,
                        InterruptType.CommsMessageReceived,
                        InterruptPriority.Low,
                        message.Sender,
                        time.Tick,
                        targetEntity: message.IntendedReceiver != Entity.Null ? message.IntendedReceiver : Entity.Null,
                        targetPosition: message.Origin,
                        payloadValue: integrity01,
                        payloadId: TokenToPayload(message.Token));

                    var acceptThreshold = 0.55f;
                    var intellect01 = 0.5f;
                    if (_statsLookup.HasComponent(entity))
                    {
                        intellect01 = math.saturate(_statsLookup[entity].Intellect / 10f);
                    }

                    var conviction01 = 0.5f;
                    if (_personalityLookup.HasComponent(entity))
                    {
                        conviction01 = math.saturate((_personalityLookup[entity].Conviction + 1f) * 0.5f);
                    }

                    var yieldThreshold = (byte)math.clamp(2 + (int)math.round((1f - intellect01) * 4f + conviction01 * 8f), 2, 30);
                    var accepted = integrity01 >= acceptThreshold || repeatCount >= yieldThreshold;

                    if (accepted)
                    {
                        InterruptUtils.Emit(
                            ref interrupts,
                            message.InterruptType,
                            message.Priority,
                            message.Sender,
                            time.Tick,
                            targetEntity: message.IntendedReceiver != Entity.Null ? message.IntendedReceiver : Entity.Null,
                            targetPosition: message.Origin,
                            payloadValue: payloadValue,
                            payloadId: payload);

                        // Auto-ack if requested (optional; receiver can opt out by lacking outbox buffer).
                        if ((message.Flags & CommsMessageFlags.RequestsAck) != 0 &&
                            SystemAPI.HasBuffer<CommsOutboxEntry>(entity))
                        {
                            var outbox = SystemAPI.GetBuffer<CommsOutboxEntry>(entity);
                            var alreadyQueued = false;
                            for (int oi = outbox.Length - 1; oi >= 0; oi--)
                            {
                                if (outbox[oi].Token == message.Token && outbox[oi].InterruptType == InterruptType.CommsAckReceived)
                                {
                                    alreadyQueued = true;
                                    break;
                                }
                            }

                            var focusOk = true;
                            var ackCost = math.max(0.01f, receiver.NoiseFloor);
                            if (ackCost > 0f && _focusLookup.HasComponent(entity))
                            {
                                var focus = _focusLookup.GetRefRW(entity);
                                if (!focus.ValueRO.CanReserve(ackCost))
                                {
                                    focusOk = false;
                                }
                                else
                                {
                                    focus.ValueRW.Current = math.max(0f, focus.ValueRO.Current - ackCost);
                                }
                            }

                            if (!alreadyQueued && focusOk)
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

                    // Mark the raw signal interrupt processed to prevent IntentMode.Custom0 spam.
                    interrupt.IsProcessed = 1;
                    interrupts[i] = interrupt;

                    remaining--;
                    if (countersRW.IsValid)
                    {
                        countersRW.ValueRW.CommsReceiptsThisTick++;
                        countersRW.ValueRW.TotalWarmOperationsThisTick++;
                    }

                    // Optional: write into inbox buffer if present (for debugging / creative passes).
                    if (SystemAPI.HasBuffer<CommsInboxEntry>(entity))
                    {
                        var inbox = SystemAPI.GetBuffer<CommsInboxEntry>(entity);
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
                            PayloadId = payload,
                            TransportUsed = message.TransportUsed,
                            Integrity01 = integrity01,
                            MisreadSeverity = misSeverity,
                            MisreadType = misType,
                            WasDeceptionDetected = wasDeceptionDetected,
                            WasProcessed = 0,
                            RepeatCount = repeatCount
                        });
                    }

                    NextInterrupt: ;
                }
            }
        }

        private static bool TryFindBestMessage(DynamicBuffer<CommsMessage> comms, Entity receiver, int cellId, PerceptionChannel transport, uint tick, out CommsMessage best)
        {
            best = default;
            var found = false;
            var bestScore = -1f;
            for (int i = 0; i < comms.Length; i++)
            {
                var msg = comms[i];
                if (msg.CellId != cellId || msg.TransportUsed != transport)
                {
                    continue;
                }

                // Targeting: never decode messages intended for someone else.
                if (msg.IntendedReceiver != Entity.Null && msg.IntendedReceiver != receiver)
                {
                    continue;
                }

                if (msg.IntendedReceiver == Entity.Null && (msg.Flags & CommsMessageFlags.IsBroadcast) == 0)
                {
                    continue;
                }

                if (tick < msg.EmittedTick || tick > msg.ExpirationTick)
                {
                    continue;
                }

                // Prefer strongest + newest.
                var age = (float)(tick - msg.EmittedTick);
                var score = msg.Strength01 - age * 0.01f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = msg;
                    found = true;
                }
            }

            return found;
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


