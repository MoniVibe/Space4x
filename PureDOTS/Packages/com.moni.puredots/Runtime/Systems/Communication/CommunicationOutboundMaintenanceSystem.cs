using PureDOTS.Runtime.Communication;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Communication
{
    /// <summary>
    /// Legacy path: handles retries/timeouts for outbound messages that require acknowledgements.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PerceptionSystemGroup))]
    [UpdateAfter(typeof(CommunicationDecodeDecideSystem))]
    public partial struct CommunicationOutboundMaintenanceSystem : ISystem
    {
        private ComponentLookup<CommDecisionConfig> _configLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<SimulationFeatureFlags>();
            state.RequireForUpdate<CommEndpoint>();

            _configLookup = state.GetComponentLookup<CommDecisionConfig>(true);
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

            _configLookup.Update(ref state);

            foreach (var (outboundBuffer, sendRequestsBuffer, entity) in
                SystemAPI.Query<DynamicBuffer<CommOutboundEntry>, DynamicBuffer<CommSendRequest>>()
                    .WithEntityAccess())
            {
                var outbound = outboundBuffer;
                var sendRequests = sendRequestsBuffer;
                if (outbound.Length == 0)
                {
                    continue;
                }

                var config = _configLookup.HasComponent(entity)
                    ? _configLookup[entity]
                    : CommDecisionConfig.Default;

                for (int i = outbound.Length - 1; i >= 0; i--)
                {
                    var entry = outbound[i];
                    if (entry.Receiver == Entity.Null || !state.EntityManager.Exists(entry.Receiver))
                    {
                        outbound.RemoveAt(i);
                        continue;
                    }

                    if (entry.TimeoutTick == 0 || entry.TimeoutTick > timeState.Tick)
                    {
                        continue;
                    }

                    if (entry.RetriesLeft == 0)
                    {
                        outbound.RemoveAt(i);
                        continue;
                    }

                    if (entry.LastSentTick == timeState.Tick)
                    {
                        continue;
                    }

                    var nextRedundancy = (byte)math.min(2, entry.RedundancyLevel + 1);

                    sendRequests.Add(new CommSendRequest
                    {
                        Receiver = entry.Receiver,
                        MessageType = entry.MessageType,
                        MessageId = entry.MessageId,
                        RelatedMessageId = entry.RelatedMessageId,
                        TrueIntent = entry.Intent,
                        StatedIntent = entry.Intent,
                        PayloadId = entry.PayloadId,
                        RedundancyLevel = nextRedundancy,
                        ClarifyMask = entry.ClarifyMask,
                        NackReason = entry.NackReason,
                        AckPolicy = entry.AckPolicy,
                        OrderVerb = entry.OrderVerb,
                        OrderTarget = entry.OrderTarget,
                        OrderTargetPosition = entry.OrderTargetPosition,
                        OrderSide = entry.OrderSide,
                        OrderPriority = entry.OrderPriority,
                        TimingWindowTicks = entry.TimingWindowTicks,
                        ContextHash = entry.ContextHash
                    });

                    entry.RedundancyLevel = nextRedundancy;
                    entry.RetriesLeft--;
                    entry.LastSentTick = timeState.Tick;
                    entry.TimeoutTick = timeState.Tick + math.max(1u, config.AckTimeoutTicks);
                    outbound[i] = entry;
                }
            }
        }
    }
}
