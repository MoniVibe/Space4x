using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Communication;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Perception;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Communication
{
    /// <summary>
    /// Builds CommAttempt entries from explicit CommSendRequest buffers.
    /// Legacy path used only when <see cref="SimulationFeatureFlags.LegacyCommunicationDispatchEnabled"/> is set.
    /// Resolves language ladder, intent, and transport channel selection.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PerceptionSystemGroup))]
    [UpdateAfter(typeof(PureDOTS.Systems.Perception.PerceptionUpdateSystem))]
    [UpdateBefore(typeof(CommunicationDispatchSystem))]
    public partial struct CommunicationAttemptBuildSystem : ISystem
    {
        private ComponentLookup<CommEndpoint> _endpointLookup;
        private BufferLookup<LanguageProficiency> _languageLookup;
        private ComponentLookup<PersistentId> _persistentIdLookup;
        private BufferLookup<CommOutboundEntry> _outboundLookup;
        private ComponentLookup<CommDecisionConfig> _decisionConfigLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<SimulationFeatureFlags>();
            state.RequireForUpdate<CommEndpoint>();

            _endpointLookup = state.GetComponentLookup<CommEndpoint>(true);
            _languageLookup = state.GetBufferLookup<LanguageProficiency>(true);
            _persistentIdLookup = state.GetComponentLookup<PersistentId>(true);
            _outboundLookup = state.GetBufferLookup<CommOutboundEntry>();
            _decisionConfigLookup = state.GetComponentLookup<CommDecisionConfig>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var features = SystemAPI.GetSingleton<SimulationFeatureFlags>();
            if ((features.Flags & SimulationFeatureFlags.CommsEnabled) == 0)
            {
                return;
            }

            if ((features.Flags & SimulationFeatureFlags.LegacyCommunicationDispatchEnabled) == 0)
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

            _endpointLookup.Update(ref state);
            _languageLookup.Update(ref state);
            _persistentIdLookup.Update(ref state);
            _outboundLookup.Update(ref state);
            _decisionConfigLookup.Update(ref state);

            foreach (var (endpoint, requests, attempts, sender) in
                SystemAPI.Query<RefRO<CommEndpoint>, DynamicBuffer<CommSendRequest>, DynamicBuffer<CommAttempt>>()
                    .WithEntityAccess())
            {
                if (requests.Length == 0)
                {
                    continue;
                }

                var senderEndpoint = endpoint.ValueRO;
                var senderLangs = _languageLookup.HasBuffer(sender) ? _languageLookup[sender] : default;
                var senderId = GetStableId(sender, ref _persistentIdLookup);
                var decisionConfig = _decisionConfigLookup.HasComponent(sender)
                    ? _decisionConfigLookup[sender]
                    : CommDecisionConfig.Default;

                for (int i = 0; i < requests.Length; i++)
                {
                    var request = requests[i];
                    var receiver = request.Receiver;
                    if (receiver == Entity.Null || !state.EntityManager.Exists(receiver))
                    {
                        continue;
                    }

                    if (!_endpointLookup.HasComponent(receiver))
                    {
                        continue;
                    }

                    var receiverEndpoint = _endpointLookup[receiver];
                    var receiverLangs = _languageLookup.HasBuffer(receiver) ? _languageLookup[receiver] : default;

                    ResolveMethodAndClarity(senderLangs, receiverLangs, senderEndpoint.BaseClarity, out var method, out var clarity);
                    if (method == CommunicationMethod.FailedCommunication)
                    {
                        continue;
                    }

                    var intent = request.DeceptionStrength > 0f && request.StatedIntent != CommunicationIntent.Incomprehensible
                        ? request.StatedIntent
                        : request.TrueIntent;

                    if (request.MessageType != CommMessageType.Order && request.MessageType != CommMessageType.ClarifyResponse)
                    {
                        if (intent == CommunicationIntent.Greeting)
                        {
                            intent = CommunicationIntent.NeutralIntent;
                        }
                    }

                    if (method == CommunicationMethod.GeneralSigns)
                    {
                        var receiverId = GetStableId(receiver, ref _persistentIdLookup);
                        var seed = BuildSeed(senderId, receiverId, timeState.Tick, (uint)i);
                        var rng = new Unity.Mathematics.Random(seed);
                        if (TryApplyGeneralSignsMisread(ref rng, ref intent, ref clarity))
                        {
                            clarity *= 0.5f;
                        }
                    }

                    if (request.RedundancyLevel > 0)
                    {
                        var redundancyBoost = 1f + 0.15f * math.min(3f, request.RedundancyLevel);
                        clarity = math.saturate(clarity * redundancyBoost);
                    }

                    var transportMask = request.TransportMask != PerceptionChannel.None
                        ? request.TransportMask
                        : (senderEndpoint.SupportedChannels & receiverEndpoint.SupportedChannels);

                    var selectedChannel = SelectTransportChannel(method, transportMask);
                    if (selectedChannel == PerceptionChannel.None)
                    {
                        continue;
                    }

                    var timestamp = request.Timestamp == 0 ? timeState.Tick : request.Timestamp;
                    var messageId = request.MessageId != 0
                        ? request.MessageId
                        : BuildMessageId(senderId, GetStableId(receiver, ref _persistentIdLookup), timestamp, (uint)i);

                    attempts.Add(new CommAttempt
                    {
                        Sender = sender,
                        Receiver = receiver,
                        TransportMask = selectedChannel,
                        Method = method,
                        Intent = intent,
                        MessageType = request.MessageType,
                        MessageId = messageId,
                        RelatedMessageId = request.RelatedMessageId,
                        PayloadId = request.PayloadId,
                        Clarity = math.saturate(clarity),
                        DeceptionStrength = request.DeceptionStrength,
                        Timestamp = timestamp,
                        AckPolicy = request.AckPolicy,
                        RedundancyLevel = request.RedundancyLevel,
                        ClarifyMask = request.ClarifyMask,
                        NackReason = request.NackReason,
                        OrderVerb = request.OrderVerb,
                        OrderTarget = request.OrderTarget,
                        OrderTargetPosition = request.OrderTargetPosition,
                        OrderSide = request.OrderSide,
                        OrderPriority = request.OrderPriority,
                        TimingWindowTicks = request.TimingWindowTicks,
                        ContextHash = request.ContextHash
                    });

                    if (_outboundLookup.HasBuffer(sender) && ShouldTrackOutbound(request, decisionConfig))
                    {
                        var outbound = _outboundLookup[sender];
                        var outboundIndex = FindOutboundIndex(outbound, messageId);
                        if (outboundIndex >= 0)
                        {
                            var existing = outbound[outboundIndex];
                            existing.LastSentTick = timestamp;
                            existing.TimeoutTick = timestamp + math.max(1u, decisionConfig.AckTimeoutTicks);
                            existing.RedundancyLevel = (byte)math.max((int)existing.RedundancyLevel, request.RedundancyLevel);
                            outbound[outboundIndex] = existing;
                        }
                        else
                        {
                            outbound.Add(new CommOutboundEntry
                            {
                                Receiver = receiver,
                                MessageType = request.MessageType,
                                MessageId = messageId,
                                RelatedMessageId = request.RelatedMessageId,
                                Intent = intent,
                                PayloadId = request.PayloadId,
                                AckPolicy = request.AckPolicy,
                                RedundancyLevel = request.RedundancyLevel,
                                RetriesLeft = decisionConfig.MaxRetries,
                                LastSentTick = timestamp,
                                TimeoutTick = timestamp + math.max(1u, decisionConfig.AckTimeoutTicks),
                                ClarifyMask = request.ClarifyMask,
                                NackReason = request.NackReason,
                                OrderVerb = request.OrderVerb,
                                OrderTarget = request.OrderTarget,
                                OrderTargetPosition = request.OrderTargetPosition,
                                OrderSide = request.OrderSide,
                                OrderPriority = request.OrderPriority,
                                TimingWindowTicks = request.TimingWindowTicks,
                                ContextHash = request.ContextHash
                            });
                        }
                    }
                }

                requests.Clear();
            }
        }

        private static void ResolveMethodAndClarity(
            in DynamicBuffer<LanguageProficiency> senderLangs,
            in DynamicBuffer<LanguageProficiency> receiverLangs,
            float baseClarity,
            out CommunicationMethod method,
            out float clarity)
        {
            method = CommunicationMethod.GeneralSigns;
            clarity = math.max(0f, baseClarity) * 0.3f;

            if (senderLangs.IsCreated && receiverLangs.IsCreated)
            {
                if (TryResolveNative(senderLangs, receiverLangs, out var nativeClarity))
                {
                    method = CommunicationMethod.NativeLanguage;
                    clarity = math.max(0f, baseClarity) * nativeClarity;
                    return;
                }

                if (TryResolveKnown(senderLangs, receiverLangs, out var knownClarity))
                {
                    method = CommunicationMethod.KnownLanguage;
                    clarity = math.max(0f, baseClarity) * knownClarity;
                    return;
                }
            }

            if (clarity <= 0f)
            {
                method = CommunicationMethod.FailedCommunication;
            }
        }

        private static bool TryResolveNative(
            in DynamicBuffer<LanguageProficiency> senderLangs,
            in DynamicBuffer<LanguageProficiency> receiverLangs,
            out float clarityFactor)
        {
            clarityFactor = 0f;
            for (int i = 0; i < senderLangs.Length; i++)
            {
                var senderLang = senderLangs[i];
                if (senderLang.IsNative == 0 || senderLang.Level == ProficiencyLevel.None)
                {
                    continue;
                }

                for (int j = 0; j < receiverLangs.Length; j++)
                {
                    var receiverLang = receiverLangs[j];
                    if (receiverLang.Level == ProficiencyLevel.None)
                    {
                        continue;
                    }

                    if (!senderLang.LanguageId.Equals(receiverLang.LanguageId))
                    {
                        continue;
                    }

                    var receiverFactor = GetProficiencyFactor(receiverLang.Level);
                    if (receiverFactor > clarityFactor)
                    {
                        clarityFactor = receiverFactor;
                    }
                }
            }

            return clarityFactor > 0f;
        }

        private static bool TryResolveKnown(
            in DynamicBuffer<LanguageProficiency> senderLangs,
            in DynamicBuffer<LanguageProficiency> receiverLangs,
            out float clarityFactor)
        {
            clarityFactor = 0f;
            for (int i = 0; i < senderLangs.Length; i++)
            {
                var senderLang = senderLangs[i];
                if (senderLang.Level == ProficiencyLevel.None)
                {
                    continue;
                }

                for (int j = 0; j < receiverLangs.Length; j++)
                {
                    var receiverLang = receiverLangs[j];
                    if (receiverLang.Level == ProficiencyLevel.None)
                    {
                        continue;
                    }

                    if (!senderLang.LanguageId.Equals(receiverLang.LanguageId))
                    {
                        continue;
                    }

                    var factor = math.min(GetProficiencyFactor(senderLang.Level), GetProficiencyFactor(receiverLang.Level));
                    if (factor > clarityFactor)
                    {
                        clarityFactor = factor;
                    }
                }
            }

            return clarityFactor > 0f;
        }

        private static float GetProficiencyFactor(ProficiencyLevel level)
        {
            return level switch
            {
                ProficiencyLevel.Rudimentary => 0.25f,
                ProficiencyLevel.Basic => 0.4f,
                ProficiencyLevel.Conversational => 0.6f,
                ProficiencyLevel.Fluent => 0.8f,
                ProficiencyLevel.Native => 1f,
                ProficiencyLevel.Scholarly => 1f,
                _ => 0f
            };
        }

        private static bool TryApplyGeneralSignsMisread(ref Unity.Mathematics.Random rng, ref CommunicationIntent intent, ref float clarity)
        {
            var misreadChance = 0.12f * (1f - math.saturate(clarity));
            if (rng.NextFloat() >= misreadChance)
            {
                return false;
            }

            intent = ResolveMisread(intent, ref rng);
            return true;
        }

        private static CommunicationIntent ResolveMisread(CommunicationIntent intent, ref Unity.Mathematics.Random rng)
        {
            return intent switch
            {
                CommunicationIntent.Greeting => CommunicationIntent.Farewell,
                CommunicationIntent.Farewell => CommunicationIntent.Greeting,
                CommunicationIntent.Gratitude => CommunicationIntent.Apology,
                CommunicationIntent.Apology => CommunicationIntent.Gratitude,
                CommunicationIntent.PeacefulIntent => CommunicationIntent.Submission,
                CommunicationIntent.Submission => CommunicationIntent.Threat,
                CommunicationIntent.Warning => CommunicationIntent.Rumor,
                CommunicationIntent.RequestHelp => CommunicationIntent.Warning,
                CommunicationIntent.DeclineRequest => CommunicationIntent.UnwillingToTrade,
                CommunicationIntent.WillingToTrade => CommunicationIntent.TradeOfferSpecific,
                CommunicationIntent.UnwillingToTrade => CommunicationIntent.DeclineRequest,
                _ => rng.NextBool() ? CommunicationIntent.NeutralIntent : CommunicationIntent.Incomprehensible
            };
        }

        private static PerceptionChannel SelectTransportChannel(CommunicationMethod method, PerceptionChannel mask)
        {
            if (mask == PerceptionChannel.None)
            {
                return PerceptionChannel.None;
            }

            if (method == CommunicationMethod.GeneralSigns)
            {
                return PickFirst4(mask, PerceptionChannel.Vision, PerceptionChannel.Proximity, PerceptionChannel.Hearing, PerceptionChannel.EM);
            }

            return PickFirst4(mask, PerceptionChannel.Hearing, PerceptionChannel.EM, PerceptionChannel.Vision, PerceptionChannel.Paranormal);
        }

        private static PerceptionChannel PickFirst4(PerceptionChannel mask, PerceptionChannel a, PerceptionChannel b, PerceptionChannel c, PerceptionChannel d)
        {
            if ((mask & a) != 0) return a;
            if ((mask & b) != 0) return b;
            if ((mask & c) != 0) return c;
            if ((mask & d) != 0) return d;

            for (int bit = 0; bit < 32; bit++)
            {
                var channel = (PerceptionChannel)(1u << bit);
                if ((mask & channel) != 0)
                {
                    return channel;
                }
            }

            return PerceptionChannel.None;
        }

        private static uint BuildSeed(uint senderId, uint receiverId, uint tick, uint requestIndex)
        {
            var seed = math.hash(new uint4(senderId, receiverId, tick, requestIndex + 1u));
            return seed == 0 ? 1u : seed;
        }

        private static uint BuildMessageId(uint senderId, uint receiverId, uint tick, uint requestIndex)
        {
            var id = math.hash(new uint4(senderId, receiverId, tick, requestIndex + 97u));
            return id == 0 ? 1u : id;
        }

        private static uint GetStableId(Entity entity, ref ComponentLookup<PersistentId> lookup)
        {
            if (lookup.HasComponent(entity))
            {
                return lookup[entity].Value;
            }

            return (uint)entity.Index + 1u;
        }

        private static bool ShouldTrackOutbound(in CommSendRequest request, in CommDecisionConfig config)
        {
            if (request.MessageType != CommMessageType.Order)
            {
                return false;
            }

            if (request.AckPolicy == CommAckPolicy.Required)
            {
                return true;
            }

            if (request.AckPolicy == CommAckPolicy.OnHighRisk)
            {
                return CommunicationRiskUtilities.IsHighRisk(request.OrderVerb);
            }

            return false;
        }

        private static int FindOutboundIndex(in DynamicBuffer<CommOutboundEntry> buffer, uint messageId)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].MessageId == messageId)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
