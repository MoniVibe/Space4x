using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Construction;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Construction
{
    /// <summary>
    /// Final decision on which intents to realize as construction sites.
    /// Sorts by urgency, checks budget and constraints, marks intents as Approved.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ConstructionIntentInfluenceSystem))]
    public partial struct ConstructionDecisionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
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

            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

            // Process groups with BuildCoordinator and ConstructionIntent buffers
            foreach (var (coordinator, entity) in SystemAPI.Query<
                RefRO<BuildCoordinator>>().WithEntityAccess())
            {
                if (coordinator.ValueRO.AutoBuildEnabled == 0)
                    continue;

                if (!SystemAPI.HasBuffer<ConstructionIntent>(entity))
                    continue;

                var intents = SystemAPI.GetBuffer<ConstructionIntent>(entity);
                var coordinatorValue = coordinator.ValueRO;

                // Check if we're at max active sites
                if (coordinatorValue.ActiveSiteCount >= coordinatorValue.MaxActiveSites)
                    continue;

                // Sort intents by urgency (descending)
                var sortedIntents = new NativeList<IntentWithIndex>(intents.Length, Allocator.Temp);
                for (int i = 0; i < intents.Length; i++)
                {
                    var intent = intents[i];
                    if (intent.Status == 0 && intent.PatternId >= 0) // Planned and has pattern
                    {
                        sortedIntents.Add(new IntentWithIndex { Intent = intent, Index = i });
                    }
                }

                sortedIntents.Sort(new IntentUrgencyComparer());

                // Approve top intents that pass checks
                int approvedCount = 0;
                int maxToApprove = coordinatorValue.MaxActiveSites - coordinatorValue.ActiveSiteCount;

                for (int i = 0; i < sortedIntents.Length && approvedCount < maxToApprove; i++)
                {
                    var item = sortedIntents[i];
                    var intent = item.Intent;

                    // Check budget (stub - would check resource availability)
                    if (coordinatorValue.BuildBudget < 1f) // Minimum budget check
                        continue;

                    // Check if intent is blocked
                    if (intent.Status == 3) // Blocked
                        continue;

                    // Approve intent
                    intent.Status = 1; // Approved
                    intents[item.Index] = intent;
                    approvedCount++;
                }

                sortedIntents.Dispose();
            }
        }

        private struct IntentWithIndex
        {
            public ConstructionIntent Intent;
            public int Index;
        }

        private struct IntentUrgencyComparer : System.Collections.Generic.IComparer<IntentWithIndex>
        {
            public int Compare(IntentWithIndex x, IntentWithIndex y)
            {
                return y.Intent.Urgency.CompareTo(x.Intent.Urgency); // Descending
            }
        }
    }
}





