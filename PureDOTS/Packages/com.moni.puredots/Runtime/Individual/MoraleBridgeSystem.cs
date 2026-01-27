using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Individual
{
    /// <summary>
    /// Bridge system to convert between EntityMorale (0-1000 range) and MoraleState (-1..+1 range).
    /// Reads EntityMorale and writes to MoraleState for entities that have both.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct MoraleBridgeSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var job = new BridgeJob();
            job.ScheduleParallel();
        }

        [BurstCompile]
        partial struct BridgeJob : IJobEntity
        {
            void Execute(ref MoraleState moraleState, in PureDOTS.Runtime.Morale.EntityMorale entityMorale)
            {
                // Convert EntityMorale (0-1000) to MoraleState (-1..+1)
                // Map 0 -> -1, 500 -> 0, 1000 -> +1
                float normalized = (entityMorale.CurrentMorale / 1000f) * 2f - 1f;
                
                moraleState.Current = math.clamp(normalized, -1f, 1f);
                
                // Map baseline from band (approximate)
                // Despair (0-199) -> -1 to -0.6
                // Unhappy (200-399) -> -0.6 to -0.2
                // Stable (400-599) -> -0.2 to 0.2
                // Cheerful (600-799) -> 0.2 to 0.6
                // Elated (800-1000) -> 0.6 to 1.0
                float baselineNormalized = (entityMorale.CurrentMorale / 1000f) * 2f - 1f;
                moraleState.Baseline = math.clamp(baselineNormalized, -1f, 1f);
                
                // Map stress/panic from breakdown/burnout risk
                moraleState.Stress = entityMorale.BreakdownRisk / 100f;
                moraleState.Panic = entityMorale.BurnoutRisk / 100f;
                
                moraleState.LastUpdateTick = entityMorale.LastUpdateTick;
            }
        }
    }
}

