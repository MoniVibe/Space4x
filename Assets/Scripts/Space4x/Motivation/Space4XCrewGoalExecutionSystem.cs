using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Motivation;
using Unity.Burst;
using Unity.Entities;

namespace Space4X.Motivation
{
    /// <summary>
    /// Stub system that reads MotivationIntent for crews/fleets and translates into concrete actions.
    /// Example: Creates fleet orders when intent is "colonize system" (SpecId=4001).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XCrewGoalExecutionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<MotivationConfigState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();

            // Skip if paused or rewinding
            if (timeState.IsPaused || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            // TODO: Implement goal execution logic
            // Example: Read MotivationIntent for fleet/empire entities
            // If ActiveSpecId == 4001 (colonize system), create fleet orders
            // Decode SpecId via MotivationCatalog if needed
            // Translate intent into fleet orders, colonization tasks, or behavior modifiers
        }
    }
}












