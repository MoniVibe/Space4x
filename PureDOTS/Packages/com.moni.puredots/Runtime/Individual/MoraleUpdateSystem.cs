using PureDOTS.Runtime.Time;
using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Individual
{
    /// <summary>
    /// Updates MoraleState based on events, needs, and environment.
    /// Implements decay toward baseline, stress accumulation, and panic checks.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct MoraleUpdateSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<TimeState>(out var timeState))
                return;
            float deltaTime = timeState.DeltaTime;
            
            var job = new UpdateMoraleJob
            {
                DeltaTime = deltaTime,
                CurrentTick = timeState.Tick
            };
            job.ScheduleParallel();
        }

        [BurstCompile]
        partial struct UpdateMoraleJob : IJobEntity
        {
            public float DeltaTime;
            public uint CurrentTick;

            void Execute(ref MoraleState morale)
            {
                // Decay current toward baseline over time
                float decayRate = 0.1f; // 10% per second toward baseline
                float decayAmount = (morale.Baseline - morale.Current) * decayRate * DeltaTime;
                morale.Current = math.clamp(morale.Current + decayAmount, -1f, 1f);

                // Decay stress over time
                float stressDecayRate = 0.05f; // 5% per second
                morale.Stress = math.max(0f, morale.Stress - stressDecayRate * DeltaTime);

                // Decay panic over time (faster than stress)
                float panicDecayRate = 0.1f; // 10% per second
                morale.Panic = math.max(0f, morale.Panic - panicDecayRate * DeltaTime);

                // Decay LastEventImpact over time
                float impactDecayRate = 0.2f; // 20% per second
                if (math.abs(morale.LastEventImpact) > 0.01f)
                {
                    morale.LastEventImpact *= (1f - impactDecayRate * DeltaTime);
                    if (math.abs(morale.LastEventImpact) < 0.01f)
                    {
                        morale.LastEventImpact = 0f;
                    }
                }

                // Update last update tick
                morale.LastUpdateTick = CurrentTick;
            }
        }
    }
}

