using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Individual
{
    /// <summary>
    /// Charges initiative over time. Adds GainPerTick each fixed tick, sets Ready flag when Current >= ActionCost.
    /// Calculates GainPerTick from Finesse + Agility + Will, modified by Morale/Boldness.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct InitiativeChargeSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<TimeState>(out var timeState))
                return;
            
            var job = new ChargeJob
            {
                CurrentTick = timeState.Tick
            };
            job.ScheduleParallel();
        }

        [BurstCompile]
        partial struct ChargeJob : IJobEntity
        {
            public uint CurrentTick;

            void Execute(
                ref InitiativeState initiative,
                in IndividualStats stats,
                in MoraleState morale,
                in PersonalityAxes personality)
            {
                // Calculate GainPerTick from stats: Finesse + Agility + Will
                // Normalize to [0..1] range (assuming stats are 0-10, max sum = 30)
                float statSum = stats.Finesse + stats.Agility + stats.Will;
                float baseGain = math.clamp(statSum / 30f, 0f, 1f) * 0.1f; // Max 0.1 per tick

                // Modify by morale: high morale increases gain
                float moraleModifier = 1f + morale.Current * 0.5f; // +50% at max morale

                // Modify by boldness: bold individuals gain initiative faster
                float boldnessModifier = 1f + personality.Boldness * 0.3f; // +30% at max boldness

                initiative.GainPerTick = baseGain * moraleModifier * boldnessModifier;

                // Charge initiative
                initiative.Current = math.min(1f, initiative.Current + initiative.GainPerTick);

                // Set Ready flag when Current >= ActionCost
                initiative.Ready = initiative.Current >= initiative.ActionCost;

                initiative.LastUpdateTick = CurrentTick;
            }
        }
    }
}

