using PureDOTS.Runtime.Comms;
using PureDOTS.Runtime.Communication;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Interrupts;
using PureDOTS.Runtime.Perception;
using PureDOTS.Runtime.Performance;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Communication
{
    /// <summary>
    /// Converts high-level CommSendRequest entries into CommsOutboxEntry records so that
    /// semantic communication runs over the scalable Comms transport (signal field + stream).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PerceptionSystemGroup))]
    [UpdateBefore(typeof(PureDOTS.Systems.Comms.CommsOutboxDepositSystem))]
    public partial struct CommunicationToCommsBridgeSystem : ISystem
    {
        private ComponentLookup<CommEndpoint> _endpointLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<CommEndpoint>();
            state.RequireForUpdate<SimulationFeatureFlags>();
            state.RequireForUpdate<CommsMessageStreamTag>();
            _endpointLookup = state.GetComponentLookup<CommEndpoint>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var features = SystemAPI.GetSingleton<SimulationFeatureFlags>();
            if ((features.Flags & SimulationFeatureFlags.LegacyCommunicationDispatchEnabled) != 0)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            _endpointLookup.Update(ref state);

            var commStreamEntity = SystemAPI.GetSingletonEntity<CommsMessageStreamTag>();
            var semanticsBuffer = EnsureSemanticBuffer(ref state, commStreamEntity);

            var budget = SystemAPI.HasSingleton<UniversalPerformanceBudget>()
                ? SystemAPI.GetSingleton<UniversalPerformanceBudget>()
                : UniversalPerformanceBudget.CreateDefaults();
            var remaining = math.max(0, budget.MaxCommsMessagesPerTick);

            foreach (var (endpoint, sendRequests, entity) in SystemAPI
                         .Query<RefRO<CommEndpoint>, DynamicBuffer<CommSendRequest>>()
                         .WithEntityAccess())
            {
                var sendRequestsBuffer = sendRequests;
                if (sendRequests.Length == 0)
                {
                    continue;
                }

                if (remaining <= 0)
                {
                    break;
                }

                var outbox = EnsureOutbox(ref state, entity);

                for (int i = 0; i < sendRequestsBuffer.Length && remaining > 0; i++)
                {
                    var request = sendRequestsBuffer[i];
                    var payloadId32 = new FixedString32Bytes(request.PayloadId);
                    if (request.MessageId == 0)
                    {
                        var interruptType = SelectInterruptType(request.MessageType);
                        request.MessageId = CommsDeterminism.ComputeToken(
                            timeState.Tick,
                            entity,
                            payloadId32,
                            interruptType);
                        sendRequestsBuffer[i] = request;
                    }

                    var entry = BuildOutboxEntry(in request, endpoint.ValueRO, payloadId32);
                    outbox.Add(entry);
                    UpsertSemantic(ref semanticsBuffer, in request, timeState.Tick);
                    remaining--;
                }

                sendRequestsBuffer.Clear();
            }
        }

        private static DynamicBuffer<CommsOutboxEntry> EnsureOutbox(ref SystemState state, Entity entity)
        {
            if (!state.EntityManager.HasBuffer<CommsOutboxEntry>(entity))
            {
                return state.EntityManager.AddBuffer<CommsOutboxEntry>(entity);
            }

            return state.EntityManager.GetBuffer<CommsOutboxEntry>(entity);
        }

        private static DynamicBuffer<CommsMessageSemantic> EnsureSemanticBuffer(ref SystemState state, Entity commStreamEntity)
        {
            if (!state.EntityManager.HasBuffer<CommsMessageSemantic>(commStreamEntity))
            {
                state.EntityManager.AddBuffer<CommsMessageSemantic>(commStreamEntity);
            }

            return state.EntityManager.GetBuffer<CommsMessageSemantic>(commStreamEntity);
        }

        private static CommsOutboxEntry BuildOutboxEntry(
            in CommSendRequest request,
            in CommEndpoint endpoint,
            in FixedString32Bytes payloadId32)
        {
            var transport = request.TransportMask != PerceptionChannel.None
                ? request.TransportMask
                : endpoint.SupportedChannels;
            var flags = CommsMessageFlags.None;
            if (request.AckPolicy == CommAckPolicy.Required)
            {
                flags |= CommsMessageFlags.RequestsAck;
            }

            if (request.DeceptionStrength > 0f || request.TrueIntent != request.StatedIntent)
            {
                flags |= CommsMessageFlags.IsDeceptive;
            }

            if (request.Receiver == Entity.Null)
            {
                flags |= CommsMessageFlags.IsBroadcast;
            }

            return new CommsOutboxEntry
            {
                Token = request.MessageId,
                InterruptType = SelectInterruptType(request.MessageType),
                Priority = SelectPriority(request.OrderPriority),
                PayloadId = payloadId32,
                TransportMaskPreferred = transport,
                Strength01 = 1f,
                Clarity01 = 1f,
                DeceptionStrength01 = math.saturate(request.DeceptionStrength),
                Secrecy01 = 0f,
                TtlTicks = request.TimingWindowTicks,
                IntendedReceiver = request.Receiver,
                Flags = flags,
                FocusCost = 0f,
                MinCohesion01 = 0f,
                RepeatCadenceTicks = 0,
                Attempts = 0,
                MaxAttempts = request.RedundancyLevel,
                NextEmitTick = 0,
                FirstEmitTick = 0
            };
        }

        private static void UpsertSemantic(
            ref DynamicBuffer<CommsMessageSemantic> semantics,
            in CommSendRequest request,
            uint createdTick)
        {
            for (int i = semantics.Length - 1; i >= 0; i--)
            {
                if (semantics[i].Token == request.MessageId)
                {
                    semantics.RemoveAtSwapBack(i);
                }
            }

            semantics.Add(new CommsMessageSemantic
            {
                Token = request.MessageId,
                MessageType = request.MessageType,
                TrueIntent = request.TrueIntent,
                StatedIntent = request.StatedIntent,
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
                ContextHash = request.ContextHash,
                IntendedReceiver = request.Receiver,
                CreatedTick = createdTick,
                RelatedMessageId = request.RelatedMessageId,
                Timestamp = request.Timestamp != 0 ? request.Timestamp : createdTick
            });
        }

        private static InterruptPriority SelectPriority(CommOrderPriority priority)
        {
            return priority switch
            {
                CommOrderPriority.High => InterruptPriority.High,
                CommOrderPriority.Critical => InterruptPriority.Critical,
                CommOrderPriority.Low => InterruptPriority.Low,
                _ => InterruptPriority.Normal
            };
        }

        private static InterruptType SelectInterruptType(CommMessageType messageType)
        {
            return messageType switch
            {
                CommMessageType.Ack => InterruptType.CommsAckReceived,
                _ => InterruptType.CommsMessageReceived
            };
        }
    }
}

