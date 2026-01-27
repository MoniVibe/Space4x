using PureDOTS.Runtime.Communication;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Interrupts;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Communication
{
    /// <summary>
    /// Bridges comm decisions into interrupts in the same tick.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PerceptionSystemGroup))]
    [UpdateAfter(typeof(CommunicationDecodeDecideSystem))]
    public partial struct CommDecisionToInterruptBridgeSystem : ISystem
    {
        private BufferLookup<CommRecentMessage> _recentLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<CommDecision>();
            _recentLookup = state.GetBufferLookup<CommRecentMessage>(true);
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

            _recentLookup.Update(ref state);

            foreach (var (decisions, entity) in SystemAPI.Query<DynamicBuffer<CommDecision>>().WithEntityAccess())
            {
                if (decisions.Length == 0)
                {
                    continue;
                }

                if (!SystemAPI.HasBuffer<Interrupt>(entity))
                {
                    state.EntityManager.AddBuffer<Interrupt>(entity);
                }

                var interruptBuffer = SystemAPI.GetBuffer<Interrupt>(entity);
                var hasRecent = _recentLookup.HasBuffer(entity);
                var recent = hasRecent ? _recentLookup[entity] : default;

                for (int i = 0; i < decisions.Length; i++)
                {
                    var decision = decisions[i];
                    if (decision.Type != CommDecisionType.Accepted)
                    {
                        continue;
                    }

                    if (hasRecent && !WasReceivedThisTick(recent, decision.MessageId, timeState.Tick))
                    {
                        continue;
                    }

                    InterruptUtils.EmitOrder(
                        ref interruptBuffer,
                        InterruptType.NewOrder,
                        entity,
                        decision.OrderTarget,
                        decision.OrderTargetPosition,
                        timeState.Tick,
                        ResolvePriority(decision.OrderPriority));
                }
            }
        }

        private static bool WasReceivedThisTick(DynamicBuffer<CommRecentMessage> recent, uint messageId, uint tick)
        {
            if (messageId == 0)
            {
                return true;
            }

            for (int i = 0; i < recent.Length; i++)
            {
                var entry = recent[i];
                if (entry.MessageId == messageId)
                {
                    return entry.ReceivedTick == tick;
                }
            }

            return true;
        }

        private static InterruptPriority ResolvePriority(CommOrderPriority priority)
        {
            return priority switch
            {
                CommOrderPriority.High => InterruptPriority.High,
                CommOrderPriority.Critical => InterruptPriority.Critical,
                CommOrderPriority.Normal => InterruptPriority.Normal,
                _ => InterruptPriority.Low
            };
        }
    }
}
