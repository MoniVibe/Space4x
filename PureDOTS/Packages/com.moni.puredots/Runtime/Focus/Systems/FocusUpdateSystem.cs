using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Individual;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Focus
{
    /// <summary>
    /// Updates FocusState each tick: calculates Load from FocusTask buffer, regenerates Current,
    /// applies soft threshold penalties, triggers mental breaks when below hard threshold.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct FocusUpdateSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<TimeState>(out var timeState))
                return;
            
            float deltaTime = timeState.DeltaTime;

            var job = new UpdateFocusJob
            {
                DeltaTime = deltaTime,
                CurrentTick = timeState.Tick
            };
            job.ScheduleParallel();
        }

        [BurstCompile]
        partial struct UpdateFocusJob : IJobEntity
        {
            public float DeltaTime;
            public uint CurrentTick;

            void Execute(
                ref FocusState focus,
                DynamicBuffer<FocusTask> tasks,
                ref MentalState mentalState)
            {
                // Calculate Load from FocusTask buffer
                float totalLoad = 0f;
                for (int i = 0; i < tasks.Length; i++)
                {
                    totalLoad += tasks[i].Cost;
                }
                focus.Load = totalLoad;

                // Regenerate: Current += RegenRate - Load * LoadFactor
                float loadFactor = 1.0f; // Base load factor
                float netChange = (focus.RegenRate - focus.Load * loadFactor) * DeltaTime;
                focus.Current = math.clamp(focus.Current + netChange, 0f, focus.Max);

                // Apply soft threshold penalties (reaction time, attack speed, morale drain)
                if (focus.Current < focus.SoftThreshold)
                {
                    // Penalties applied by other systems reading FocusState
                    // This system just tracks the state
                }

                // Trigger mental breaks when below hard threshold
                if (focus.Current < focus.HardThreshold)
                {
                    // Transition to mental break state based on how far below threshold
                    float deficit = focus.HardThreshold - focus.Current;
                    float breakChance = math.clamp(deficit / focus.HardThreshold, 0f, 1f);

                    // Random check (using deterministic hash for consistency)
                    // Burst-compatible hash: combine focus state fields
                    uint hash = (uint)(CurrentTick + (uint)(focus.Current * 1000f) + (uint)(focus.Max * 100f) + (uint)(focus.Load * 50f));
                    float random = (hash % 1000) / 1000f;

                    if (random < breakChance * 0.1f) // 10% max chance per tick
                    {
                        // Determine break type based on personality (would need PersonalityAxes)
                        // For now, default to Panicked
                        if (mentalState.State == MentalBreakState.Stable)
                        {
                            mentalState.State = MentalBreakState.Panicked;
                            mentalState.LastStateChangeTick = CurrentTick;
                        }
                    }
                }
                else if (focus.Current > focus.SoftThreshold && mentalState.State != MentalBreakState.Stable)
                {
                    // Recovery: gradually return to Stable
                    if (CurrentTick - mentalState.LastStateChangeTick > 100) // 100 ticks recovery delay
                    {
                        mentalState.State = MentalBreakState.Stable;
                        mentalState.LastStateChangeTick = CurrentTick;
                    }
                }
            }
        }
    }
}

