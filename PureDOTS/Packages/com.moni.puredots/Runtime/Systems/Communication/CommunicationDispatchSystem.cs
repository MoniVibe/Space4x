using PureDOTS.Runtime.Communication;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Perception;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Communication
{
    /// <summary>
    /// Legacy path: dispatches queued comm attempts directly (guarded by LegacyCommunicationDispatchEnabled).
    /// Apply medium + endpoint gating for message delivery.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PerceptionSystemGroup))]
    [UpdateAfter(typeof(CommunicationEndpointBootstrapSystem))]
    public partial struct CommunicationDispatchSystem : ISystem
    {
        private ComponentLookup<CommEndpoint> _endpointLookup;
        private ComponentLookup<CommLinkQuality> _linkQualityLookup;
        private ComponentLookup<MediumContext> _mediumLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<SenseCapability> _senseLookup;
        private BufferLookup<SenseOrganState> _organLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<SimulationFeatureFlags>();
            state.RequireForUpdate<CommEndpoint>();

            _endpointLookup = state.GetComponentLookup<CommEndpoint>(true);
            _linkQualityLookup = state.GetComponentLookup<CommLinkQuality>(true);
            _mediumLookup = state.GetComponentLookup<MediumContext>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _senseLookup = state.GetComponentLookup<SenseCapability>(true);
            _organLookup = state.GetBufferLookup<SenseOrganState>(true);
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
            _linkQualityLookup.Update(ref state);
            _mediumLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _senseLookup.Update(ref state);
            _organLookup.Update(ref state);

            var jammers = new NativeList<JammerSample>(Allocator.Temp);
            foreach (var (jammer, transform) in SystemAPI.Query<RefRO<CommJammer>, RefRO<LocalTransform>>())
            {
                var jammerData = jammer.ValueRO;
                if (jammerData.IsActive == 0 || jammerData.Radius <= 0f || jammerData.Strength <= 0f)
                {
                    continue;
                }

                jammers.Add(new JammerSample
                {
                    Position = transform.ValueRO.Position,
                    Radius = jammerData.Radius,
                    Strength = math.saturate(jammerData.Strength)
                });
            }

            foreach (var (endpoint, attempts, sender) in SystemAPI.Query<RefRO<CommEndpoint>, DynamicBuffer<CommAttempt>>()
                .WithEntityAccess())
            {
                if (attempts.Length == 0)
                {
                    continue;
                }

                var senderMedium = ResolveMedium(sender);
                var senderChannels = endpoint.ValueRO.SupportedChannels;

                for (int i = 0; i < attempts.Length; i++)
                {
                    var attempt = attempts[i];
                    var receiver = attempt.Receiver;
                    if (receiver == Entity.Null || !state.EntityManager.Exists(receiver))
                    {
                        continue;
                    }

                    if (!_endpointLookup.HasComponent(receiver))
                    {
                        continue;
                    }

                    if (!_transformLookup.HasComponent(sender) || !_transformLookup.HasComponent(receiver))
                    {
                        continue;
                    }

                    var receiverEndpoint = _endpointLookup[receiver];
                    var receiverMedium = ResolveMedium(receiver);
                    var senderPos = _transformLookup[sender].Position;
                    var receiverPos = _transformLookup[receiver].Position;
                    var distance = math.distance(senderPos, receiverPos);

                    var mask = attempt.TransportMask == PerceptionChannel.None
                        ? senderChannels
                        : attempt.TransportMask;

                    mask &= senderChannels;
                    mask &= receiverEndpoint.SupportedChannels;
                    mask = MediumUtilities.FilterChannels(senderMedium, mask);
                    mask = MediumUtilities.FilterChannels(receiverMedium, mask);
                    mask = FilterChannelsByRange(mask, sender, receiver, distance);

                    if (mask == PerceptionChannel.None)
                    {
                        continue;
                    }

                    var channel = SelectPrimaryChannel(mask);
                    var channelRange = ResolveChannelRange(channel, sender, receiver);
                    if (channelRange <= 0f)
                    {
                        continue;
                    }

                    var clarity = attempt.Clarity > 0f ? attempt.Clarity : endpoint.ValueRO.BaseClarity;
                    var rangeFactor = math.saturate(1f - distance / channelRange);
                    clarity = math.saturate(clarity * rangeFactor);

                    var linkQuality = 1f;
                    var baseInterference = 0f;
                    if (_linkQualityLookup.HasComponent(receiver))
                    {
                        var link = _linkQualityLookup[receiver];
                        linkQuality = math.saturate(link.IntegrityMultiplier);
                        baseInterference = math.max(0f, link.Interference);
                    }

                    var jamInterference = ComputeJammerInterference(receiverPos, jammers);
                    linkQuality = math.saturate(linkQuality - baseInterference - jamInterference);
                    clarity = math.saturate(clarity * linkQuality);
                    if (clarity <= 0f)
                    {
                        continue;
                    }
                    var integrity = math.saturate(clarity - attempt.DeceptionStrength - receiverEndpoint.NoiseFloor);
                    var wasDeceptive = (byte)((attempt.DeceptionStrength > 0f && integrity < clarity) ? 1 : 0);

                    if (!state.EntityManager.HasBuffer<CommReceipt>(receiver))
                    {
                        continue;
                    }

                    var receiptBuffer = state.EntityManager.GetBuffer<CommReceipt>(receiver);
                    receiptBuffer.Add(new CommReceipt
                    {
                        Sender = sender,
                        Channel = channel,
                        Method = attempt.Method,
                        Intent = attempt.Intent,
                        MessageType = attempt.MessageType,
                        MessageId = attempt.MessageId,
                        RelatedMessageId = attempt.RelatedMessageId,
                        PayloadId = attempt.PayloadId,
                        Integrity = integrity,
                        WasDeceptive = wasDeceptive,
                        Timestamp = attempt.Timestamp == 0 ? timeState.Tick : attempt.Timestamp,
                        AckPolicy = attempt.AckPolicy,
                        RedundancyLevel = attempt.RedundancyLevel,
                        ClarifyMask = attempt.ClarifyMask,
                        NackReason = attempt.NackReason,
                        OrderVerb = attempt.OrderVerb,
                        OrderTarget = attempt.OrderTarget,
                        OrderTargetPosition = attempt.OrderTargetPosition,
                        OrderSide = attempt.OrderSide,
                        OrderPriority = attempt.OrderPriority,
                        TimingWindowTicks = attempt.TimingWindowTicks,
                        ContextHash = attempt.ContextHash
                    });
                }

                attempts.Clear();
            }

            jammers.Dispose();
        }

        private MediumType ResolveMedium(Entity entity)
        {
            return _mediumLookup.HasComponent(entity)
                ? _mediumLookup[entity].Type
                : MediumType.Gas;
        }

        private PerceptionChannel FilterChannelsByRange(PerceptionChannel mask, Entity sender, Entity receiver, float distance)
        {
            if (mask == PerceptionChannel.None)
            {
                return PerceptionChannel.None;
            }

            PerceptionChannel filtered = PerceptionChannel.None;
            for (int bit = 0; bit < 32; bit++)
            {
                var channel = (PerceptionChannel)(1u << bit);
                if ((mask & channel) == 0)
                {
                    continue;
                }

                var range = ResolveChannelRange(channel, sender, receiver);
                if (range <= 0f || distance > range)
                {
                    continue;
                }

                filtered |= channel;
            }

            return filtered;
        }

        private float ResolveChannelRange(PerceptionChannel channel, Entity sender, Entity receiver)
        {
            float range = 0f;

            if (_senseLookup.HasComponent(sender))
            {
                var sense = _senseLookup[sender];
                range = sense.Range;
                if (_organLookup.HasBuffer(sender))
                {
                    PerceptionOrganUtilities.GetChannelModifiers(
                        channel,
                        _organLookup[sender],
                        out var rangeMult,
                        out _,
                        out _);
                    range *= rangeMult;
                }
            }

            if (_senseLookup.HasComponent(receiver))
            {
                var sense = _senseLookup[receiver];
                var receiverRange = sense.Range;
                if (_organLookup.HasBuffer(receiver))
                {
                    PerceptionOrganUtilities.GetChannelModifiers(
                        channel,
                        _organLookup[receiver],
                        out var rangeMult,
                        out _,
                        out _);
                    receiverRange *= rangeMult;
                }

                range = range > 0f ? math.min(range, receiverRange) : receiverRange;
            }

            if (range <= 0f)
            {
                range = GetDefaultChannelRange(channel);
            }

            return range;
        }

        private static float GetDefaultChannelRange(PerceptionChannel channel)
        {
            if ((channel & PerceptionChannel.Hearing) != 0)
            {
                return 25f;
            }

            if ((channel & PerceptionChannel.Vision) != 0)
            {
                return 20f;
            }

            if ((channel & PerceptionChannel.EM) != 0)
            {
                return 500f;
            }

            if ((channel & PerceptionChannel.Paranormal) != 0)
            {
                return 80f;
            }

            if ((channel & PerceptionChannel.Exotic) != 0)
            {
                return 200f;
            }

            return 30f;
        }

        private static PerceptionChannel SelectPrimaryChannel(PerceptionChannel mask)
        {
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

        private static float ComputeJammerInterference(float3 position, NativeList<JammerSample> jammers)
        {
            if (jammers.Length == 0)
            {
                return 0f;
            }

            var total = 0f;
            for (int i = 0; i < jammers.Length; i++)
            {
                var jammer = jammers[i];
                var distance = math.distance(position, jammer.Position);
                if (distance > jammer.Radius)
                {
                    continue;
                }

                var falloff = 1f - (distance / jammer.Radius);
                total += jammer.Strength * falloff;
            }

            return math.saturate(total);
        }

        private struct JammerSample
        {
            public float3 Position;
            public float Radius;
            public float Strength;
        }
    }
}
