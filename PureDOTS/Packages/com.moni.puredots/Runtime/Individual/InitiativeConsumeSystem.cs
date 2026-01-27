using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Individual
{
    /// <summary>
    /// Consumes initiative when entities act. Runs before AI/combat systems.
    /// Only Ready entities (Current >= ActionCost) can enter action loops.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    // TODO: Fix UpdateBefore reference when AI system namespace is clarified
    // [UpdateBefore(typeof(PureDOTS.Runtime.AI.Systems.AIUtilityEvaluationSystem))]
    public partial struct InitiativeConsumeSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var job = new ConsumeJob();
            job.ScheduleParallel();
        }

        [BurstCompile]
        partial struct ConsumeJob : IJobEntity
        {
            void Execute(ref InitiativeState initiative)
            {
                // Only consume if Ready (entity acted this frame)
                if (initiative.Ready)
                {
                    initiative.Current = math.max(0f, initiative.Current - initiative.ActionCost);
                    initiative.Ready = false; // Reset ready flag after consuming
                }
            }
        }
    }
}

