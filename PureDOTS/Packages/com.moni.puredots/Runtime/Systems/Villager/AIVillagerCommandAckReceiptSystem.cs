using PureDOTS.Runtime.AI;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Individual;
using PureDOTS.Runtime.Performance;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Villager
{
    /// <summary>
    /// Emits "Received" acknowledgement events for AI commands as they enter the villager domain.
    /// Optional: only agents with AIAckConfig can emit, and only when the command requests receipt acks.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(VillagerSystemGroup))]
    [UpdateAfter(typeof(PureDOTS.Systems.VillagerAIPipelineBridgeSystem))]
    [UpdateBefore(typeof(PureDOTS.Systems.VillagerAISystem))]
    public partial struct AIVillagerCommandAckReceiptSystem : ISystem
    {
        private ComponentLookup<AIAckConfig> _ackConfigLookup;
        private ComponentLookup<AlignmentTriplet> _alignmentLookup;
        private ComponentLookup<FocusBudget> _focusLookup;
        private ComponentLookup<ResourcePools> _poolsLookup;
        private ComponentLookup<IndividualStats> _statsLookup;
        private ComponentLookup<VillagerNeedState> _villagerNeedsLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<AICommandQueueTag>();
            state.RequireForUpdate<AIAckStreamTag>();

            _ackConfigLookup = state.GetComponentLookup<AIAckConfig>(true);
            _alignmentLookup = state.GetComponentLookup<AlignmentTriplet>(true);
            _focusLookup = state.GetComponentLookup<FocusBudget>(true);
            _poolsLookup = state.GetComponentLookup<ResourcePools>(true);
            _statsLookup = state.GetComponentLookup<IndividualStats>(true);
            _villagerNeedsLookup = state.GetComponentLookup<VillagerNeedState>(true);
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

            _ackConfigLookup.Update(ref state);
            _alignmentLookup.Update(ref state);
            _focusLookup.Update(ref state);
            _poolsLookup.Update(ref state);
            _statsLookup.Update(ref state);
            _villagerNeedsLookup.Update(ref state);

            var queueEntity = SystemAPI.GetSingletonEntity<AICommandQueueTag>();
            var commands = state.EntityManager.GetBuffer<AICommand>(queueEntity);
            if (commands.Length == 0)
            {
                return;
            }

            var ackEntity = SystemAPI.GetSingletonEntity<AIAckStreamTag>();
            var ackEvents = state.EntityManager.GetBuffer<AIAckEvent>(ackEntity);

            var ackBudget = int.MaxValue;
            RefRW<UniversalPerformanceCounters> countersRW = default;
            if (SystemAPI.HasSingleton<UniversalPerformanceBudget>() && SystemAPI.HasSingleton<UniversalPerformanceCounters>())
            {
                ackBudget = math.max(0, SystemAPI.GetSingleton<UniversalPerformanceBudget>().MaxAckEventsPerTick);
                countersRW = SystemAPI.GetSingletonRW<UniversalPerformanceCounters>();
            }

            for (int i = 0; i < commands.Length; i++)
            {
                if (ackBudget <= 0)
                {
                    if (countersRW.IsValid)
                    {
                        countersRW.ValueRW.AckEventsDroppedThisTick++;
                        countersRW.ValueRW.TotalOperationsDroppedThisTick++;
                    }
                    break;
                }

                var cmd = commands[i];
                if ((cmd.AckFlags & AIAckRequestFlags.RequestReceipt) == 0 || cmd.AckToken == 0u)
                {
                    continue;
                }

                var agent = cmd.Agent;
                if (!_ackConfigLookup.HasComponent(agent))
                {
                    continue;
                }

                var config = _ackConfigLookup[agent];
                if (config.Enabled == 0)
                {
                    continue;
                }

                var hasAlignment = _alignmentLookup.HasComponent(agent);
                var alignment = hasAlignment ? _alignmentLookup[agent] : default;
                var chaos01 = hasAlignment ? AIAckPolicyUtility.ComputeChaos01(in alignment) : 0.5f;

                var hasFocus = _focusLookup.HasComponent(agent);
                var focus = hasFocus ? _focusLookup[agent] : default;
                var hasPools = _poolsLookup.HasComponent(agent);
                var pools = hasPools ? _poolsLookup[agent] : default;
                var focusRatio01 = AIAckPolicyUtility.ComputeFocusRatio01(hasFocus, in focus, hasPools, in pools);

                var hasNeeds = _villagerNeedsLookup.HasComponent(agent);
                var needs = hasNeeds ? _villagerNeedsLookup[agent] : default;
                var hasStats = _statsLookup.HasComponent(agent);
                var stats = hasStats ? _statsLookup[agent] : default;
                var sleep01 = AIAckPolicyUtility.ComputeSleepPressure01(hasNeeds, in needs, hasPools, in pools, hasStats, in stats);

                if (!AIAckPolicyUtility.ShouldEmitReceiptAcks(in config, focusRatio01, sleep01, chaos01, cmd.AckToken))
                {
                    continue;
                }

                ackBudget--;
                if (countersRW.IsValid)
                {
                    countersRW.ValueRW.AckEventsEmittedThisTick++;
                    countersRW.ValueRW.TotalWarmOperationsThisTick++;
                }

                ackEvents.Add(new AIAckEvent
                {
                    Tick = time.Tick,
                    Token = cmd.AckToken,
                    Agent = agent,
                    TargetEntity = cmd.TargetEntity,
                    ActionIndex = cmd.ActionIndex,
                    Stage = AIAckStage.Received,
                    Reason = AIAckReason.None,
                    Flags = (byte)cmd.AckFlags
                });
            }
        }
    }
}


