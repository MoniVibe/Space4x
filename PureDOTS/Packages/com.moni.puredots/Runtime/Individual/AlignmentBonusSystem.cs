using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Individual
{
    /// <summary>
    /// Recomputes derived bonuses from alignment components periodically.
    /// Updates MightMagicAlignment bonuses based on axis and strength.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct AlignmentBonusCalculationSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var job = new CalculateBonusesJob();
            job.ScheduleParallel();
        }

        [BurstCompile]
        partial struct CalculateBonusesJob : IJobEntity
        {
            void Execute(ref MightMagicAlignment alignment)
            {
                // Calculate might bonus: positive when axis < 0 (might-aligned)
                // Strength determines magnitude
                if (alignment.Axis < 0f)
                {
                    alignment.MightBonus = math.abs(alignment.Axis) * alignment.Strength * 0.2f; // Max 20% bonus
                    alignment.MagicBonus = 0f;
                    alignment.OppositePenalty = math.abs(alignment.Axis) * alignment.Strength * 0.1f; // Max 10% penalty
                }
                // Calculate magic bonus: positive when axis > 0 (magic-aligned)
                else if (alignment.Axis > 0f)
                {
                    alignment.MagicBonus = alignment.Axis * alignment.Strength * 0.2f; // Max 20% bonus
                    alignment.MightBonus = 0f;
                    alignment.OppositePenalty = alignment.Axis * alignment.Strength * 0.1f; // Max 10% penalty
                }
                // Neutral: no bonuses or penalties
                else
                {
                    alignment.MightBonus = 0f;
                    alignment.MagicBonus = 0f;
                    alignment.OppositePenalty = 0f;
                }
            }
        }
    }
}

