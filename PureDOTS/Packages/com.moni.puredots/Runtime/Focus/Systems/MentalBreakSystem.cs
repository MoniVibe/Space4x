using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Groups;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Focus
{
    /// <summary>
    /// Handles mental break states from hard focus depletion.
    /// Overrides IndividualCombatIntent based on break state.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(FocusUpdateSystem))]
    public partial struct MentalBreakSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<TimeState>(out var timeState))
                return;

            var job = new ProcessMentalBreaksJob
            {
                CurrentTick = timeState.Tick
            };
            job.ScheduleParallel();
        }

        [BurstCompile]
        partial struct ProcessMentalBreaksJob : IJobEntity
        {
            public uint CurrentTick;

            void Execute(
                ref IndividualCombatIntent intent,
                in MentalState mentalState,
                in FocusState focus)
            {
                // Override intent based on mental break state
                switch (mentalState.State)
                {
                    case MentalBreakState.Stable:
                        // Normal operation - intent resolved by GroupIntentResolutionSystem
                        break;

                    case MentalBreakState.Frazzled:
                        // Random intent flips, bad target choices
                        // Burst-compatible hash: combine focus state fields
                        uint hash = (uint)(CurrentTick + (uint)(focus.Current * 1000f) + (uint)(focus.Max * 100f) + (uint)(focus.Load * 50f));
                        float random = (hash % 1000) / 1000f;
                        if (random < 0.2f) // 20% chance to flip intent
                        {
                            // Randomly change intent
                            int intentValue = (int)(hash % 6);
                            intent.Intent = (IndividualTacticalIntent)intentValue;
                        }
                        break;

                    case MentalBreakState.Panicked:
                        // Flee or surrender regardless of orders
                        intent.Intent = IndividualTacticalIntent.Flee;
                        intent.TargetOverride = Entity.Null;
                        break;

                    case MentalBreakState.Catatonic:
                        // Stand still, drop tasks
                        intent.Intent = IndividualTacticalIntent.CautiousHold;
                        intent.TargetOverride = Entity.Null;
                        break;

                    case MentalBreakState.Berserk:
                        // Ignore group, attack nearest
                        intent.Intent = IndividualTacticalIntent.AggressivePursuit;
                        // TargetOverride would be set to nearest enemy by targeting system
                        break;
                }
            }
        }
    }
}

