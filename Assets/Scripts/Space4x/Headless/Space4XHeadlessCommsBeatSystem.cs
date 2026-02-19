using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Comms;
using PureDOTS.Runtime.Communication;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Interrupts;
using PureDOTS.Runtime.Performance;
using PureDOTS.Runtime.Perception;
using Space4X.Registry;
using Space4X.Runtime;
using Space4x.Scenario;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Headless
{
    [UpdateInGroup(typeof(PureDOTS.Systems.PerceptionSystemGroup))]
    [UpdateAfter(typeof(PureDOTS.Systems.Comms.CommsTargetedMediumDeliverySystem))]
    public partial struct Space4XHeadlessCommsBeatSystem : ISystem
    {
        private const byte BlockedReasonNone = 0;
        private const byte BlockedReasonWrongTransport = 1;
        private const byte BlockedReasonMissingConfig = 2;
        private const byte BlockedReasonReceiverDisabled = 3;
        private const byte BlockedReasonMissingInterrupt = 4;
        private const byte BlockedReasonMissingSignal = 5;
        private const byte BlockedReasonExpired = 6;
        private const byte BlockedReasonNoEmission = 7;
        private Entity _sender;
        private Entity _receiver;
        private uint _startTick;
        private uint _endTick;
        private uint _sendIntervalTicks;
        private uint _nextSendTick;
        private uint _sentCount;
        private uint _emittedCount;
        private uint _receivedCount;
        private uint _maxOutboxDepth;
        private uint _maxInboxDepth;
        private uint _firstSendTick;
        private uint _firstReceiptTick;
        private byte _initialized;
        private byte _done;
        private byte _hasBudget;
        private byte _receiverHasConfig;
        private byte _receiverHasInbox;
        private byte _receiverHasInterrupt;
        private byte _receiverHasSignal;
        private uint _receiptCounterMax;
        private uint _streamTargetedTotal;
        private uint _streamTargetedEm;
        private uint _streamTargetedHearing;
        private byte _commsEnabled;
        private byte _perceptionEnabled;
        private uint _maxCommsBudget;
        private byte _timePaused;
        private byte _rewindMode;
        private uint _streamLengthMax;
        private uint _diagTargetedConsidered;
        private uint _diagTargetedExpired;
        private uint _diagTargetedWrongTransport;
        private uint _diagTargetedMissingConfig;
        private uint _diagTargetedReceiverDisabled;
        private uint _diagTargetedMissingInterrupt;
        private uint _diagTargetedMissingSignal;
        private uint _diagTargetedDuplicate;
        private uint _diagTargetedDelivered;
        private byte _blockedReason;

        private ComponentLookup<Carrier> _carrierLookup;
        private BufferLookup<CommsMessage> _messageLookup;
        private BufferLookup<CommsInboxEntry> _inboxLookup;
        private BufferLookup<CommsOutboxEntry> _outboxLookup;

        public void OnCreate(ref SystemState state)
        {
            if (!RuntimeMode.IsHeadless || !Application.isBatchMode)
            {
                state.Enabled = false;
                return;
            }

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<Space4XScenarioRuntime>();
            state.RequireForUpdate<Space4XCommsBeatConfig>();
            state.RequireForUpdate<CommsMessageStreamTag>();

            _carrierLookup = state.GetComponentLookup<Carrier>(true);
            _messageLookup = state.GetBufferLookup<CommsMessage>(true);
            _inboxLookup = state.GetBufferLookup<CommsInboxEntry>(true);
            _outboxLookup = state.GetBufferLookup<CommsOutboxEntry>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_done != 0)
            {
                return;
            }

            _carrierLookup.Update(ref state);
            _messageLookup.Update(ref state);
            _inboxLookup.Update(ref state);
            _outboxLookup.Update(ref state);
            _hasBudget = (byte)(SystemAPI.HasSingleton<UniversalPerformanceBudget>() ? 1 : 0);

            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var runtime = SystemAPI.GetSingleton<Space4XScenarioRuntime>();
            var config = SystemAPI.GetSingleton<Space4XCommsBeatConfig>();
            if (config.Initialized == 0)
            {
                InitializeConfig(ref config, runtime.StartTick, timeState.FixedDeltaTime);
                SystemAPI.SetSingleton(config);
            }

            if (_initialized == 0)
            {
                _startTick = config.StartTick;
                _endTick = config.EndTick;
                _sendIntervalTicks = math.max(1u, config.SendIntervalTicks);
                _nextSendTick = _startTick;
                _initialized = 1;
            }

            var tick = timeState.Tick;
            ResolveEntities(ref state, config);
            TrackReceiverState(ref state);
            TrackReceiptCounters();

            if (tick < _startTick)
            {
                return;
            }

            if (tick > _endTick)
            {
                FinalizeReport(ref state, config);
                config.Completed = 1;
                SystemAPI.SetSingleton(config);
                _done = 1;
                return;
            }

            MeasureTick(ref state, config, tick);
            EmitRequestsIfNeeded(ref state, config, tick);
        }

        private void ResolveEntities(ref SystemState state, in Space4XCommsBeatConfig config)
        {
            if (_sender != Entity.Null && state.EntityManager.Exists(_sender) &&
                _receiver != Entity.Null && state.EntityManager.Exists(_receiver))
            {
                return;
            }

            _sender = ResolveCarrier(config.SenderCarrierId, ref state);
            _receiver = ResolveCarrier(config.ReceiverCarrierId, ref state);
        }

        private void EmitRequestsIfNeeded(ref SystemState state, in Space4XCommsBeatConfig config, uint tick)
        {
            if (_sender == Entity.Null || _receiver == Entity.Null)
            {
                return;
            }

            if (tick < _nextSendTick)
            {
                return;
            }

            if (!state.EntityManager.HasBuffer<CommsOutboxEntry>(_sender))
            {
                state.EntityManager.AddBuffer<CommsOutboxEntry>(_sender);
            }

            var outbox = state.EntityManager.GetBuffer<CommsOutboxEntry>(_sender);
            var payloadId = config.PayloadId.IsEmpty ? default : new FixedString32Bytes(config.PayloadId);
            var flags = config.RequireAck != 0 ? CommsMessageFlags.RequestsAck : CommsMessageFlags.None;
            outbox.Add(new CommsOutboxEntry
            {
                Token = 0u,
                InterruptType = InterruptType.CommsMessageReceived,
                Priority = InterruptPriority.Normal,
                PayloadId = payloadId,
                TransportMaskPreferred = config.TransportMask != PerceptionChannel.None ? config.TransportMask : PerceptionChannel.EM,
                Strength01 = 1f,
                Clarity01 = 1f,
                DeceptionStrength01 = 0f,
                Secrecy01 = 0f,
                TtlTicks = math.max(1u, _sendIntervalTicks),
                IntendedReceiver = _receiver,
                Flags = flags,
                FocusCost = 0f,
                MinCohesion01 = 0f,
                RepeatCadenceTicks = 0u,
                Attempts = 0,
                MaxAttempts = 0,
                NextEmitTick = 0u,
                FirstEmitTick = 0u
            });

            _sentCount++;
            if (_firstSendTick == 0u)
            {
                _firstSendTick = tick;
            }

            _nextSendTick = tick + _sendIntervalTicks;
        }

        private void MeasureTick(ref SystemState state, in Space4XCommsBeatConfig config, uint tick)
        {
            if (_sender == Entity.Null || _receiver == Entity.Null)
            {
                return;
            }

            var commStreamEntity = SystemAPI.GetSingletonEntity<CommsMessageStreamTag>();
            if (_messageLookup.HasBuffer(commStreamEntity))
            {
                var messages = _messageLookup[commStreamEntity];
                for (var i = 0; i < messages.Length; i++)
                {
                    var message = messages[i];
                    if (message.Sender == _sender && message.IntendedReceiver == _receiver && message.EmittedTick == tick)
                    {
                        _emittedCount++;
                    }
                }
            }

            if (_inboxLookup.HasBuffer(_receiver))
            {
                var inbox = _inboxLookup[_receiver];
                _maxInboxDepth = math.max(_maxInboxDepth, (uint)inbox.Length);
                for (var i = 0; i < inbox.Length; i++)
                {
                    var entry = inbox[i];
                    if (entry.Sender == _sender && entry.ReceivedTick == tick)
                    {
                        _receivedCount++;
                        if (_firstReceiptTick == 0u)
                        {
                            _firstReceiptTick = tick;
                        }
                    }
                }
            }

            if (_outboxLookup.HasBuffer(_sender))
            {
                var outbox = _outboxLookup[_sender];
                _maxOutboxDepth = math.max(_maxOutboxDepth, (uint)outbox.Length);
            }
        }

        private void FinalizeReport(ref SystemState state, in Space4XCommsBeatConfig config)
        {
            SnapshotStream(ref state);
            SnapshotDiagnostics(ref state);
            if (_receivedCount == 0 && _diagTargetedDelivered > 0 && _sentCount > 0)
            {
                _receivedCount = (uint)math.min(_diagTargetedDelivered, _sentCount);
            }

            _blockedReason = BlockedReasonNone;
            if (_sentCount > 0 && _receivedCount == 0)
            {
                if (_diagTargetedWrongTransport > 0)
                {
                    _blockedReason = BlockedReasonWrongTransport;
                }
                else if (_diagTargetedMissingConfig > 0)
                {
                    _blockedReason = BlockedReasonMissingConfig;
                }
                else if (_diagTargetedReceiverDisabled > 0)
                {
                    _blockedReason = BlockedReasonReceiverDisabled;
                }
                else if (_diagTargetedMissingInterrupt > 0)
                {
                    _blockedReason = BlockedReasonMissingInterrupt;
                }
                else if (_diagTargetedMissingSignal > 0)
                {
                    _blockedReason = BlockedReasonMissingSignal;
                }
                else if (_diagTargetedExpired > 0)
                {
                    _blockedReason = BlockedReasonExpired;
                }
                else if (_emittedCount == 0)
                {
                    _blockedReason = BlockedReasonNoEmission;
                }
            }

            var hasBlackCats = Space4XOperatorReportUtility.TryGetBlackCatBuffer(ref state, out var blackCats);

            var classification = (byte)0;
            if (_sender == Entity.Null)
            {
                classification = 1;
            }
            else if (_receiver == Entity.Null)
            {
                classification = 2;
            }
            else if (_sentCount == 0)
            {
                classification = 3;
            }
            else if (_emittedCount == 0)
            {
                classification = 4;
            }
            else if (_hasBudget == 0)
            {
                classification = 5;
            }

            if (classification != 0 && hasBlackCats)
            {
                blackCats.Add(new Space4XOperatorBlackCat
                {
                    Id = new FixedString64Bytes("COMMS_BEAT_SKIPPED"),
                    Primary = _sender,
                    Secondary = _receiver,
                    StartTick = _startTick,
                    EndTick = _endTick,
                    MetricA = _sentCount,
                    MetricB = _emittedCount,
                    MetricC = _receivedCount,
                    MetricD = classification,
                    Classification = classification
                });
            }

            EmitOperatorSummary(ref state, config);
        }

        private void EmitOperatorSummary(ref SystemState state, in Space4XCommsBeatConfig config)
        {
            if (!Space4XOperatorReportUtility.TryGetMetricBuffer(ref state, out var buffer))
            {
                return;
            }

            var deliveryRatio = _sentCount > 0 ? (float)_receivedCount / _sentCount : 0f;
            var emittedRatio = _sentCount > 0 ? (float)_emittedCount / _sentCount : 0f;
            var firstLatency = _firstSendTick > 0 && _firstReceiptTick > 0 && _firstReceiptTick >= _firstSendTick
                ? _firstReceiptTick - _firstSendTick
                : 0u;

            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.comms.sent"), _sentCount);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.comms.emitted"), _emittedCount);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.comms.received"), _receivedCount);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.comms.delivery_ratio"), deliveryRatio);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.comms.emit_ratio"), emittedRatio);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.comms.first_latency_ticks"), firstLatency);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.comms.max_outbox_depth"), _maxOutboxDepth);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.comms.max_inbox_depth"), _maxInboxDepth);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.comms.interval_ticks"), _sendIntervalTicks);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.comms.has_budget"), _hasBudget);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.comms.receiver_has_config"), _receiverHasConfig);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.comms.receiver_has_inbox"), _receiverHasInbox);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.comms.receiver_has_interrupt"), _receiverHasInterrupt);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.comms.receiver_has_signal"), _receiverHasSignal);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.comms.receipts_counter_max"), _receiptCounterMax);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.comms.stream_targeted_total"), _streamTargetedTotal);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.comms.stream_targeted_em"), _streamTargetedEm);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.comms.stream_targeted_hearing"), _streamTargetedHearing);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.comms.feature_comms_enabled"), _commsEnabled);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.comms.feature_perception_enabled"), _perceptionEnabled);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.comms.max_messages_budget"), _maxCommsBudget);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.comms.time_paused"), _timePaused);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.comms.rewind_mode"), _rewindMode);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.comms.stream_length_max"), _streamLengthMax);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.comms.diag.targeted_considered"), _diagTargetedConsidered);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.comms.diag.targeted_expired"), _diagTargetedExpired);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.comms.diag.targeted_wrong_transport"), _diagTargetedWrongTransport);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.comms.diag.targeted_missing_config"), _diagTargetedMissingConfig);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.comms.diag.targeted_receiver_disabled"), _diagTargetedReceiverDisabled);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.comms.diag.targeted_missing_interrupt"), _diagTargetedMissingInterrupt);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.comms.diag.targeted_missing_signal"), _diagTargetedMissingSignal);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.comms.diag.targeted_duplicate"), _diagTargetedDuplicate);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.comms.diag.targeted_delivered"), _diagTargetedDelivered);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.comms.blocked_reason"), _blockedReason);
        }

        private static void InitializeConfig(ref Space4XCommsBeatConfig config, uint startTick, float fixedDt)
        {
            var start = startTick + SecondsToTicks(config.StartSeconds, fixedDt);
            var duration = SecondsToTicks(config.DurationSeconds, fixedDt);
            var interval = math.max(1u, SecondsToTicks(config.SendIntervalSeconds, fixedDt));

            config.StartTick = start;
            config.EndTick = start + duration;
            config.SendIntervalTicks = interval;
            config.Initialized = 1;
        }

        private Entity ResolveCarrier(FixedString64Bytes carrierId, ref SystemState state)
        {
            if (carrierId.IsEmpty)
            {
                return Entity.Null;
            }

            foreach (var (carrier, entity) in SystemAPI.Query<RefRO<Carrier>>().WithEntityAccess())
            {
                if (carrier.ValueRO.CarrierId.Equals(carrierId))
                {
                    return entity;
                }
            }

            return Entity.Null;
        }

        private static uint SecondsToTicks(float seconds, float fixedDt)
        {
            if (seconds <= 0f || fixedDt <= 0f)
            {
                return 0u;
            }

            return (uint)math.ceil(seconds / fixedDt);
        }

        private static void AddOrUpdateMetric(
            DynamicBuffer<Space4XOperatorMetric> buffer,
            FixedString64Bytes key,
            float value)
        {
            for (var i = 0; i < buffer.Length; i++)
            {
                var metric = buffer[i];
                if (!metric.Key.Equals(key))
                {
                    continue;
                }

                metric.Value = value;
                buffer[i] = metric;
                return;
            }

            buffer.Add(new Space4XOperatorMetric
            {
                Key = key,
                Value = value
            });
        }

        private void TrackReceiverState(ref SystemState state)
        {
            if (_receiver == Entity.Null || !state.EntityManager.Exists(_receiver))
            {
                return;
            }

            _receiverHasConfig = (byte)(state.EntityManager.HasComponent<CommsReceiverConfig>(_receiver) ? 1 : 0);
            _receiverHasInbox = (byte)(_inboxLookup.HasBuffer(_receiver) ? 1 : 0);
            _receiverHasInterrupt = (byte)(state.EntityManager.HasBuffer<Interrupt>(_receiver) ? 1 : 0);
            _receiverHasSignal = (byte)(state.EntityManager.HasComponent<SignalPerceptionState>(_receiver) ? 1 : 0);
            if (SystemAPI.HasSingleton<SimulationFeatureFlags>())
            {
                var flags = SystemAPI.GetSingleton<SimulationFeatureFlags>().Flags;
                _commsEnabled = (byte)((flags & SimulationFeatureFlags.CommsEnabled) != 0 ? 1 : 0);
                _perceptionEnabled = (byte)((flags & SimulationFeatureFlags.PerceptionEnabled) != 0 ? 1 : 0);
            }

            if (SystemAPI.HasSingleton<UniversalPerformanceBudget>())
            {
                _maxCommsBudget = (uint)math.max(0, SystemAPI.GetSingleton<UniversalPerformanceBudget>().MaxCommsMessagesPerTick);
            }

            if (SystemAPI.HasSingleton<TimeState>())
            {
                _timePaused = (byte)(SystemAPI.GetSingleton<TimeState>().IsPaused ? 1 : 0);
            }

            if (SystemAPI.HasSingleton<RewindState>())
            {
                _rewindMode = (byte)SystemAPI.GetSingleton<RewindState>().Mode;
            }
        }

        private void TrackReceiptCounters()
        {
            if (!SystemAPI.HasSingleton<UniversalPerformanceCounters>())
            {
                return;
            }

            var counters = SystemAPI.GetSingleton<UniversalPerformanceCounters>();
            var receipts = (uint)math.max(0, counters.CommsReceiptsThisTick);
            if (receipts > _receiptCounterMax)
            {
                _receiptCounterMax = receipts;
            }
        }

        private void SnapshotStream(ref SystemState state)
        {
            if (_sender == Entity.Null || _receiver == Entity.Null)
            {
                return;
            }

            var commStreamEntity = SystemAPI.GetSingletonEntity<CommsMessageStreamTag>();
            if (!_messageLookup.HasBuffer(commStreamEntity))
            {
                return;
            }

            var messages = _messageLookup[commStreamEntity];
            if ((uint)messages.Length > _streamLengthMax)
            {
                _streamLengthMax = (uint)messages.Length;
            }
            for (var i = 0; i < messages.Length; i++)
            {
                var message = messages[i];
                if (message.Sender != _sender || message.IntendedReceiver != _receiver)
                {
                    continue;
                }

                _streamTargetedTotal++;
                if (message.TransportUsed == PerceptionChannel.EM)
                {
                    _streamTargetedEm++;
                }
                else if (message.TransportUsed == PerceptionChannel.Hearing)
                {
                    _streamTargetedHearing++;
                }
            }
        }

        private void SnapshotDiagnostics(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<CommsDeliveryDiagnostics>())
            {
                return;
            }

            var diag = SystemAPI.GetSingleton<CommsDeliveryDiagnostics>();
            _diagTargetedConsidered = diag.TargetedConsidered;
            _diagTargetedExpired = diag.TargetedExpired;
            _diagTargetedWrongTransport = diag.TargetedWrongTransport;
            _diagTargetedMissingConfig = diag.TargetedMissingReceiverConfig;
            _diagTargetedReceiverDisabled = diag.TargetedReceiverDisabled;
            _diagTargetedMissingInterrupt = diag.TargetedMissingInterrupt;
            _diagTargetedMissingSignal = diag.TargetedMissingSignal;
            _diagTargetedDuplicate = diag.TargetedDuplicateEmission;
            _diagTargetedDelivered = diag.TargetedDelivered;
        }
    }
}
