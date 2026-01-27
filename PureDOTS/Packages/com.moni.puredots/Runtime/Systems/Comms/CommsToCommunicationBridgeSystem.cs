using PureDOTS.Runtime.Comms;
using PureDOTS.Runtime.Communication;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Comms
{
    /// <summary>
    /// Converts low-level Comms inbox entries back into CommReceipt records so the
    /// semantic communication layer can process messages delivered via the Comms transport.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PerceptionSystemGroup))]
    [UpdateAfter(typeof(CommsSignalToInterruptSystem))]
    [UpdateAfter(typeof(CommsTargetedMediumDeliverySystem))]
    public partial struct CommsToCommunicationBridgeSystem : ISystem
    {
        private const uint DefaultSemanticTtlTicks = 600;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<CommsMessageStreamTag>();
            state.RequireForUpdate<SimulationFeatureFlags>();
            state.RequireForUpdate<CommReceipt>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var features = SystemAPI.GetSingleton<SimulationFeatureFlags>();
            if ((features.Flags & SimulationFeatureFlags.LegacyCommunicationDispatchEnabled) != 0)
            {
                return;
            }

            var commStreamEntity = SystemAPI.GetSingletonEntity<CommsMessageStreamTag>();
            if (!state.EntityManager.HasBuffer<CommsMessageSemantic>(commStreamEntity))
            {
                return;
            }

            var semantics = state.EntityManager.GetBuffer<CommsMessageSemantic>(commStreamEntity);
            CleanupExpiredSemantics(ref semantics, timeState.Tick);

            foreach (var (inbox, receipts) in SystemAPI
                         .Query<DynamicBuffer<CommsInboxEntry>, DynamicBuffer<CommReceipt>>())
            {
                if (inbox.Length == 0)
                {
                    continue;
                }

                for (int i = inbox.Length - 1; i >= 0; i--)
                {
                    var entry = inbox[i];
                    var semanticIndex = FindSemanticIndex(semantics, entry.Token);
                    CommsMessageSemantic semantic = default;
                    var hasSemantic = semanticIndex >= 0;
                    if (hasSemantic)
                    {
                        semantic = semantics[semanticIndex];
                    }

                    receipts.Add(BuildReceipt(entry, hasSemantic ? (CommMessageType?)semantic.MessageType : null, hasSemantic ? semantic : default));

                    if (hasSemantic && semantic.IntendedReceiver != Entity.Null)
                    {
                        semantics.RemoveAtSwapBack(semanticIndex);
                    }

                    inbox.RemoveAtSwapBack(i);
                }
            }
        }

        private static void CleanupExpiredSemantics(ref DynamicBuffer<CommsMessageSemantic> semantics, uint currentTick)
        {
            for (int i = semantics.Length - 1; i >= 0; i--)
            {
                var semantic = semantics[i];
                var ttl = semantic.TimingWindowTicks > 0 ? semantic.TimingWindowTicks : DefaultSemanticTtlTicks;
                if (ttl > 0 && currentTick - semantic.CreatedTick > ttl)
                {
                    semantics.RemoveAtSwapBack(i);
                }
            }
        }

        private static int FindSemanticIndex(DynamicBuffer<CommsMessageSemantic> semantics, uint token)
        {
            for (int i = 0; i < semantics.Length; i++)
            {
                if (semantics[i].Token == token)
                {
                    return i;
                }
            }

            return -1;
        }

        private static CommReceipt BuildReceipt(
            in CommsInboxEntry inboxEntry,
            CommMessageType? messageType,
            in CommsMessageSemantic semantic)
        {
            FixedString64Bytes payload = inboxEntry.PayloadId;

            return new CommReceipt
            {
                Sender = inboxEntry.Sender,
                Channel = inboxEntry.TransportUsed,
                Method = CommunicationMethod.GeneralSigns,
                Intent = messageType.HasValue ? semantic.StatedIntent : CommunicationIntent.Incomprehensible,
                MessageType = messageType ?? CommMessageType.Order,
                MessageId = inboxEntry.Token,
                RelatedMessageId = semantic.RelatedMessageId,
                PayloadId = payload,
                Integrity = inboxEntry.Integrity01,
                WasDeceptive = inboxEntry.WasDeceptionDetected,
                Timestamp = semantic.Timestamp,
                AckPolicy = semantic.AckPolicy,
                RedundancyLevel = semantic.RedundancyLevel,
                ClarifyMask = semantic.ClarifyMask,
                NackReason = semantic.NackReason,
                OrderVerb = semantic.OrderVerb,
                OrderTarget = semantic.OrderTarget,
                OrderTargetPosition = semantic.OrderTargetPosition,
                OrderSide = semantic.OrderSide,
                OrderPriority = semantic.OrderPriority,
                TimingWindowTicks = semantic.TimingWindowTicks,
                ContextHash = semantic.ContextHash
            };
        }
    }
}

