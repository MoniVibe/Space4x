using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Communication;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Perception;
using PureDOTS.Runtime.Villagers;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Communication
{
    /// <summary>
    /// Decodes incoming comm receipts into decisions, acknowledgements, or clarification requests.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PerceptionSystemGroup))]
    [UpdateAfter(typeof(CommunicationDispatchSystem))]
    public partial struct CommunicationDecodeDecideSystem : ISystem
    {
        private ComponentLookup<CommDecisionConfig> _configLookup;
        private ComponentLookup<CommBudgetState> _budgetLookup;
        private ComponentLookup<CommDecodeFactors> _factorLookup;
        private ComponentLookup<VillagerAttributes> _villagerAttrLookup;
        private ComponentLookup<PilotAttributes> _pilotAttrLookup;
        private BufferLookup<CommRecentMessage> _recentLookup;
        private BufferLookup<CommPendingClarify> _pendingClarifyLookup;
        private BufferLookup<CommOutboundEntry> _outboundLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<SimulationFeatureFlags>();
            state.RequireForUpdate<CommEndpoint>();

            _configLookup = state.GetComponentLookup<CommDecisionConfig>(true);
            _budgetLookup = state.GetComponentLookup<CommBudgetState>();
            _factorLookup = state.GetComponentLookup<CommDecodeFactors>(true);
            _villagerAttrLookup = state.GetComponentLookup<VillagerAttributes>(true);
            _pilotAttrLookup = state.GetComponentLookup<PilotAttributes>(true);
            _recentLookup = state.GetBufferLookup<CommRecentMessage>();
            _pendingClarifyLookup = state.GetBufferLookup<CommPendingClarify>();
            _outboundLookup = state.GetBufferLookup<CommOutboundEntry>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var features = SystemAPI.GetSingleton<SimulationFeatureFlags>();
            if ((features.Flags & SimulationFeatureFlags.CommsEnabled) == 0)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            _configLookup.Update(ref state);
            _budgetLookup.Update(ref state);
            _factorLookup.Update(ref state);
            _villagerAttrLookup.Update(ref state);
            _pilotAttrLookup.Update(ref state);
            _recentLookup.Update(ref state);
            _pendingClarifyLookup.Update(ref state);
            _outboundLookup.Update(ref state);

            foreach (var (endpoint, receipts, decisions, sendRequestsBuffer, entity) in
                SystemAPI.Query<RefRO<CommEndpoint>, DynamicBuffer<CommReceipt>, DynamicBuffer<CommDecision>, DynamicBuffer<CommSendRequest>>()
                    .WithEntityAccess())
            {
                var sendRequests = sendRequestsBuffer;
                if (receipts.Length == 0)
                {
                    continue;
                }

                var config = _configLookup.HasComponent(entity)
                    ? _configLookup[entity]
                    : CommDecisionConfig.Default;

                if (!_budgetLookup.HasComponent(entity))
                {
                    continue;
                }

                var budget = _budgetLookup[entity];
                if (budget.LastTick != timeState.Tick)
                {
                    budget.LastTick = timeState.Tick;
                    budget.ClarifyUsed = 0;
                }

                var recent = _recentLookup.HasBuffer(entity) ? _recentLookup[entity] : default;
                var pendingClarify = _pendingClarifyLookup.HasBuffer(entity) ? _pendingClarifyLookup[entity] : default;

                for (int i = 0; i < receipts.Length; i++)
                {
                    var receipt = receipts[i];
                    var messageId = receipt.MessageId != 0
                        ? receipt.MessageId
                        : BuildFallbackMessageId(entity, receipt, (uint)i);
                    var clarifyKey = receipt.MessageType == CommMessageType.ClarifyResponse && receipt.RelatedMessageId != 0
                        ? receipt.RelatedMessageId
                        : messageId;

                    if (IsDuplicate(recent, messageId))
                    {
                        continue;
                    }

                    RecordRecent(ref recent, messageId, timeState.Tick, receipt.MessageType, config.DuplicateHistorySize);

                    if (receipt.MessageType == CommMessageType.Ack || receipt.MessageType == CommMessageType.Nack)
                    {
                        if (_outboundLookup.HasBuffer(entity))
                        {
                            var outbound = _outboundLookup[entity];
                            var targetId = receipt.RelatedMessageId != 0 ? receipt.RelatedMessageId : messageId;
                            RemoveOutbound(ref outbound, targetId);
                        }
                        continue;
                    }

                    if (receipt.MessageType == CommMessageType.ClarifyRequest)
                    {
                        TryRespondToClarify(receipt, ref sendRequests, entity);
                        continue;
                    }

                    var factors = ResolveFactors(entity);
                    var confidence = math.saturate(receipt.Integrity
                        * math.saturate(factors.Cohesion)
                        * math.saturate(factors.ProtocolFamiliarity)
                        * math.saturate(factors.ContextFit));

                    var risk = CommunicationRiskUtilities.GetOrderRisk(receipt.OrderVerb);
                    var actThreshold = CommunicationRiskUtilities.ResolveActThreshold(config, risk);
                    var canReply = receipt.Integrity >= config.MinBacklinkIntegrity && endpoint.ValueRO.SupportedChannels != PerceptionChannel.None;

                    var needClarify = (1f - confidence) * risk;
                    var canClarify = canReply
                        && needClarify > config.ClarifyThreshold
                        && budget.ClarifyUsed < config.ClarifyBudgetPerTick
                        && ClarifyAvailable(ref pendingClarify, clarifyKey, config.ClarifyPerMessageMax);

                    var clarifyMask = BuildClarifyMask(receipt);
                    if (canClarify && clarifyMask != CommClarifyQuestionMask.None)
                    {
                        sendRequests.Add(new CommSendRequest
                        {
                            Receiver = receipt.Sender,
                            MessageType = CommMessageType.ClarifyRequest,
                            RelatedMessageId = clarifyKey,
                            TrueIntent = CommunicationIntent.NeutralIntent,
                            StatedIntent = CommunicationIntent.NeutralIntent,
                            PayloadId = receipt.PayloadId,
                            ClarifyMask = clarifyMask
                        });

                        ConsumeClarify(ref pendingClarify, clarifyKey, timeState.Tick);
                        budget.ClarifyUsed = (byte)math.min(255, budget.ClarifyUsed + 1);

                        decisions.Add(new CommDecision
                        {
                            Type = CommDecisionType.ClarifyRequested,
                            MessageId = messageId,
                            Sender = receipt.Sender,
                            OrderVerb = receipt.OrderVerb,
                            OrderTarget = receipt.OrderTarget,
                            OrderTargetPosition = receipt.OrderTargetPosition,
                            OrderSide = receipt.OrderSide,
                            OrderPriority = receipt.OrderPriority,
                            TimingWindowTicks = receipt.TimingWindowTicks,
                            ContextHash = receipt.ContextHash,
                            Confidence = confidence,
                            ClarifyMask = clarifyMask
                        });
                        continue;
                    }

                    if (confidence >= actThreshold)
                    {
                        decisions.Add(new CommDecision
                        {
                            Type = CommDecisionType.Accepted,
                            MessageId = messageId,
                            Sender = receipt.Sender,
                            OrderVerb = receipt.OrderVerb,
                            OrderTarget = receipt.OrderTarget,
                            OrderTargetPosition = receipt.OrderTargetPosition,
                            OrderSide = receipt.OrderSide,
                            OrderPriority = receipt.OrderPriority,
                            TimingWindowTicks = receipt.TimingWindowTicks,
                            ContextHash = receipt.ContextHash,
                            Confidence = confidence
                        });

                        TrySendAck(receipt, messageId, ref sendRequests);
                        continue;
                    }

                    if (!canClarify && TryInferAction(confidence, factors, config, receipt.OrderVerb, out var inferredVerb))
                    {
                        decisions.Add(new CommDecision
                        {
                            Type = CommDecisionType.Accepted,
                            MessageId = messageId,
                            Sender = receipt.Sender,
                            OrderVerb = inferredVerb,
                            OrderTarget = receipt.OrderTarget,
                            OrderTargetPosition = receipt.OrderTargetPosition,
                            OrderSide = receipt.OrderSide,
                            OrderPriority = receipt.OrderPriority,
                            TimingWindowTicks = receipt.TimingWindowTicks,
                            ContextHash = receipt.ContextHash,
                            Confidence = confidence,
                            Inferred = 1
                        });

                        TrySendAck(receipt, messageId, ref sendRequests);
                        continue;
                    }

                    if (risk <= config.DefaultSafeRiskCutoff)
                    {
                        decisions.Add(new CommDecision
                        {
                            Type = CommDecisionType.DefaultSafe,
                            MessageId = messageId,
                            Sender = receipt.Sender,
                            OrderVerb = receipt.OrderVerb,
                            OrderTarget = receipt.OrderTarget,
                            OrderTargetPosition = receipt.OrderTargetPosition,
                            OrderSide = receipt.OrderSide,
                            OrderPriority = receipt.OrderPriority,
                            TimingWindowTicks = receipt.TimingWindowTicks,
                            ContextHash = receipt.ContextHash,
                            Confidence = confidence
                        });
                    }
                    else
                    {
                        decisions.Add(new CommDecision
                        {
                            Type = CommDecisionType.Ignored,
                            MessageId = messageId,
                            Sender = receipt.Sender,
                            OrderVerb = receipt.OrderVerb,
                            OrderTarget = receipt.OrderTarget,
                            OrderTargetPosition = receipt.OrderTargetPosition,
                            OrderSide = receipt.OrderSide,
                            OrderPriority = receipt.OrderPriority,
                            TimingWindowTicks = receipt.TimingWindowTicks,
                            ContextHash = receipt.ContextHash,
                            Confidence = confidence
                        });
                    }
                }

                _budgetLookup[entity] = budget;
            }
        }

        private CommDecodeFactors ResolveFactors(Entity entity)
        {
            var factors = _factorLookup.HasComponent(entity)
                ? _factorLookup[entity]
                : CommDecodeFactors.Default;

            if (_villagerAttrLookup.HasComponent(entity))
            {
                var attributes = _villagerAttrLookup[entity];
                factors.Intelligence = math.saturate(attributes.Intelligence / 100f);
                factors.Wisdom = math.saturate(attributes.Wisdom / 100f);
            }
            else if (_pilotAttrLookup.HasComponent(entity))
            {
                var attributes = _pilotAttrLookup[entity];
                factors.Intelligence = math.saturate(attributes.Intelligence / 100f);
            }

            return factors;
        }

        private static bool TryInferAction(float confidence, in CommDecodeFactors factors, in CommDecisionConfig config, CommOrderVerb verb, out CommOrderVerb inferredVerb)
        {
            inferredVerb = verb;
            if (factors.Intelligence < config.MinIntelligenceForInference)
            {
                return false;
            }

            var doctrineWeight = CommunicationDoctrineUtilities.GetDoctrineWeight(verb);
            var contextScore = math.saturate(factors.ContextFit);
            var posterior = confidence * math.max(0.5f, contextScore * 0.7f + doctrineWeight * 0.3f);
            var threshold = math.lerp(config.InferThresholdLowWisdom, config.InferThresholdHighWisdom, math.saturate(factors.Wisdom));
            if (posterior < threshold)
            {
                return false;
            }

            var risk = CommunicationRiskUtilities.GetOrderRisk(verb);
            inferredVerb = CommunicationDoctrineUtilities.ResolveInferredVerb(verb, contextScore, risk);
            return true;
        }

        private static void TrySendAck(in CommReceipt receipt, uint messageId, ref DynamicBuffer<CommSendRequest> sendRequests)
        {
            if (receipt.AckPolicy == CommAckPolicy.None)
            {
                return;
            }

            if (receipt.AckPolicy == CommAckPolicy.OnHighRisk && !CommunicationRiskUtilities.IsHighRisk(receipt.OrderVerb))
            {
                return;
            }

            var ackTargetId = receipt.MessageType == CommMessageType.ClarifyResponse && receipt.RelatedMessageId != 0
                ? receipt.RelatedMessageId
                : messageId;

            sendRequests.Add(new CommSendRequest
            {
                Receiver = receipt.Sender,
                MessageType = CommMessageType.Ack,
                RelatedMessageId = ackTargetId,
                TrueIntent = CommunicationIntent.NeutralIntent,
                StatedIntent = CommunicationIntent.NeutralIntent,
                PayloadId = receipt.PayloadId
            });
        }

        private void TryRespondToClarify(in CommReceipt receipt, ref DynamicBuffer<CommSendRequest> sendRequests, Entity entity)
        {
            if (!_outboundLookup.HasBuffer(entity))
            {
                return;
            }

            var outbound = _outboundLookup[entity];
            var targetId = receipt.RelatedMessageId;
            if (targetId == 0)
            {
                return;
            }

            for (int i = 0; i < outbound.Length; i++)
            {
                var entry = outbound[i];
                if (entry.MessageId != targetId)
                {
                    continue;
                }

                sendRequests.Add(new CommSendRequest
                {
                    Receiver = receipt.Sender,
                    MessageType = CommMessageType.ClarifyResponse,
                    RelatedMessageId = targetId,
                    TrueIntent = entry.Intent,
                    StatedIntent = entry.Intent,
                    PayloadId = entry.PayloadId,
                    RedundancyLevel = (byte)math.min(2, entry.RedundancyLevel + 1),
                    OrderVerb = entry.OrderVerb,
                    OrderTarget = entry.OrderTarget,
                    OrderTargetPosition = entry.OrderTargetPosition,
                    OrderSide = entry.OrderSide,
                    OrderPriority = entry.OrderPriority,
                    TimingWindowTicks = entry.TimingWindowTicks,
                    ContextHash = entry.ContextHash
                });
                return;
            }
        }

        private static bool IsDuplicate(in DynamicBuffer<CommRecentMessage> recent, uint messageId)
        {
            if (!recent.IsCreated)
            {
                return false;
            }

            for (int i = 0; i < recent.Length; i++)
            {
                if (recent[i].MessageId == messageId)
                {
                    return true;
                }
            }

            return false;
        }

        private static void RecordRecent(ref DynamicBuffer<CommRecentMessage> recent, uint messageId, uint tick, CommMessageType type, byte maxEntries)
        {
            if (!recent.IsCreated)
            {
                return;
            }

            if (maxEntries == 0)
            {
                return;
            }

            if (maxEntries > 0 && recent.Length >= maxEntries)
            {
                recent.RemoveAt(0);
            }

            recent.Add(new CommRecentMessage
            {
                MessageId = messageId,
                ReceivedTick = tick,
                MessageType = type
            });
        }

        private static uint BuildFallbackMessageId(Entity receiver, in CommReceipt receipt, uint index)
        {
            var seed = math.hash(new uint4((uint)receipt.Sender.Index + 1u, (uint)receiver.Index + 1u, receipt.Timestamp, index + 1u));
            return seed == 0 ? 1u : seed;
        }

        private static bool ClarifyAvailable(ref DynamicBuffer<CommPendingClarify> pending, uint messageId, byte maxPerMessage)
        {
            if (!pending.IsCreated)
            {
                return false;
            }

            for (int i = 0; i < pending.Length; i++)
            {
                var entry = pending[i];
                if (entry.MessageId != messageId)
                {
                    continue;
                }

                return entry.Attempts < maxPerMessage;
            }

            return true;
        }

        private static void ConsumeClarify(ref DynamicBuffer<CommPendingClarify> pending, uint messageId, uint tick)
        {
            if (!pending.IsCreated)
            {
                return;
            }

            for (int i = 0; i < pending.Length; i++)
            {
                var entry = pending[i];
                if (entry.MessageId != messageId)
                {
                    continue;
                }

                entry.Attempts = (byte)math.min(255, entry.Attempts + 1);
                entry.AskedTick = tick;
                pending[i] = entry;
                return;
            }

            pending.Add(new CommPendingClarify
            {
                MessageId = messageId,
                AskedTick = tick,
                Attempts = 1
            });
        }

        private static CommClarifyQuestionMask BuildClarifyMask(in CommReceipt receipt)
        {
            CommClarifyQuestionMask mask = CommClarifyQuestionMask.None;
            var hasTarget = receipt.OrderTarget != Entity.Null;
            var hasPosition = !math.all(receipt.OrderTargetPosition == float3.zero);

            if (receipt.OrderVerb == CommOrderVerb.None)
            {
                mask |= CommClarifyQuestionMask.MissingTarget;
            }

            switch (receipt.OrderVerb)
            {
                case CommOrderVerb.MoveTo:
                case CommOrderVerb.Patrol:
                    if (!hasPosition)
                    {
                        mask |= CommClarifyQuestionMask.MissingTarget;
                    }
                    break;
                case CommOrderVerb.Attack:
                case CommOrderVerb.Defend:
                case CommOrderVerb.FocusFire:
                case CommOrderVerb.Suppress:
                case CommOrderVerb.Screen:
                case CommOrderVerb.Flank:
                case CommOrderVerb.DrawFire:
                    if (!hasTarget && !hasPosition)
                    {
                        mask |= CommClarifyQuestionMask.MissingTarget;
                    }
                    break;
            }

            return mask;
        }

        private static void RemoveOutbound(ref DynamicBuffer<CommOutboundEntry> outbound, uint messageId)
        {
            for (int i = 0; i < outbound.Length; i++)
            {
                if (outbound[i].MessageId == messageId)
                {
                    outbound.RemoveAt(i);
                    return;
                }
            }
        }
    }
}
