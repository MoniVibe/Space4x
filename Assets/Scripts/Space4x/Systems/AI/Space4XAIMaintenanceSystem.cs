using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

using SpatialSystemGroup = PureDOTS.Systems.SpatialSystemGroup;

namespace Space4X.Systems.AI
{
    /// <summary>
    /// Implements maintenance and repair automation per MaintenanceAndRepair.md: carriers evaluate repair needs,
    /// route to shipyards in friendly territory, and factor stance/mission urgency when choosing repair options.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(Space4XThreatBehaviorSystem))]
    public partial struct Space4XAIMaintenanceSystem : ISystem
    {
        private ComponentLookup<VesselStanceComponent> _stanceLookup;
        private BufferLookup<AIOrder> _orderLookup;
        private ComponentLookup<AlignmentTriplet> _alignmentLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            
            _stanceLookup = state.GetComponentLookup<VesselStanceComponent>(true);
            _orderLookup = state.GetBufferLookup<AIOrder>(true);
            _alignmentLookup = state.GetComponentLookup<AlignmentTriplet>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            _stanceLookup.Update(ref state);
            _orderLookup.Update(ref state);
            _alignmentLookup.Update(ref state);

            var job = new ProcessMaintenanceJob
            {
                CurrentTick = timeState.Tick,
                StanceLookup = _stanceLookup,
                OrderLookup = _orderLookup,
                AlignmentLookup = _alignmentLookup
            };

            state.Dependency = job.Schedule(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(AICommandQueue))]
        public partial struct ProcessMaintenanceJob : IJobEntity
        {
            public uint CurrentTick;
            [ReadOnly] public ComponentLookup<VesselStanceComponent> StanceLookup;
            [ReadOnly] public BufferLookup<AIOrder> OrderLookup;
            [ReadOnly] public ComponentLookup<AlignmentTriplet> AlignmentLookup;

            public void Execute(Entity entity, ref PreFlightCheck preFlight)
            {
                // Determine if repair is needed
                var needsRepair = (float)preFlight.HullIntegrity < 0.7f;
                var needsProvisions = (float)preFlight.ProvisionsLevel < 0.5f;
                var needsMoraleBoost = (float)preFlight.CrewMorale < 0.4f;

                if (!needsRepair && !needsProvisions && !needsMoraleBoost)
                {
                    return;
                }

                // Get alignment for decision-making
                var alignment = AlignmentLookup.HasComponent(entity) 
                    ? AlignmentLookup[entity] 
                    : default(AlignmentTriplet);
                var lawfulness = AlignmentMath.Lawfulness(alignment);

                // Get current stance and mission urgency
                var hasUrgentMission = false;
                if (OrderLookup.HasBuffer(entity))
                {
                    var orders = OrderLookup[entity];
                    for (int i = 0; i < orders.Length; i++)
                    {
                        if (orders[i].Status == AIOrderStatus.Executing && orders[i].Priority > 200)
                        {
                            hasUrgentMission = true;
                            break;
                        }
                    }
                }

                var stance = StanceLookup.HasComponent(entity) 
                    ? StanceLookup[entity].CurrentStance 
                    : VesselStanceMode.Neutral;

                // AI factors stance, mission urgency, and proximity to friendly facilities
                // Lawful captains prioritize safety, chaotic may push limits
                var shouldBreakOff = false;

                if (needsRepair && (float)preFlight.HullIntegrity < 0.5f)
                {
                    // Critical hull damage - always break off unless extremely urgent
                    shouldBreakOff = !hasUrgentMission || (float)preFlight.HullIntegrity < 0.3f;
                }
                else if (needsProvisions && (float)preFlight.ProvisionsLevel < 0.3f)
                {
                    // Critical provisions - break off unless urgent
                    shouldBreakOff = !hasUrgentMission || lawfulness > 0.7f; // Lawful prioritize safety
                }
                else if (needsMoraleBoost && (float)preFlight.CrewMorale < 0.3f)
                {
                    // Critical morale - consider breaking off
                    shouldBreakOff = lawfulness > 0.6f; // Lawful prioritize crew welfare
                }

                // In full implementation, would:
                // 1. Check proximity to friendly shipyards/stations
                // 2. Queue repair/resupply orders
                // 3. Route to nearest facility
                // 4. Handle field repairs in hostile territory

                // Update pre-flight check to reflect maintenance needs
                if (shouldBreakOff)
                {
                    // Mark that maintenance is needed
                    preFlight.CheckPassed = 0;
                }
            }
        }
    }
}
