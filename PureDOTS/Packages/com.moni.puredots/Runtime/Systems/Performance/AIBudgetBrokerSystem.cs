using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Performance;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Performance
{
    /// <summary>
    /// Resets and exposes per-tick AI/perception credits (LOS rays, decision updates, path requests).
    /// Backed by UniversalPerformanceBudget and tracked via UniversalPerformanceCounters.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(WarmPathSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(UniversalPerformanceBudgetSystem))]
    public partial struct AIBudgetBrokerSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<UniversalPerformanceBudget>();
            state.RequireForUpdate<UniversalPerformanceCounters>();

            if (!SystemAPI.HasSingleton<AIBudgetBrokerState>())
            {
                var entity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(entity, default(AIBudgetBrokerState));
            }
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

            var budget = SystemAPI.GetSingleton<UniversalPerformanceBudget>();
            var broker = SystemAPI.GetSingletonRW<AIBudgetBrokerState>();

            broker.ValueRW.Tick = time.Tick;
            broker.ValueRW.RemainingLosRays = budget.MaxLosRaysPerTick;
            broker.ValueRW.RemainingDecisionUpdates = budget.MaxTacticalDecisionsPerTick;
            broker.ValueRW.RemainingPathRequests = budget.MaxStrategicRoutePlansPerTick;

            broker.ValueRW.DeferredLosRays = 0;
            broker.ValueRW.DeferredDecisions = 0;
            broker.ValueRW.DeferredPathRequests = 0;
        }
    }
}


