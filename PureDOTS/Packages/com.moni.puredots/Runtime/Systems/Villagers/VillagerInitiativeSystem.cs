using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Villagers;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Villagers
{
    /// <summary>
    /// System that computes villager initiative from base (village band) + personality modifiers.
    /// Initiative determines WHEN villagers act autonomously (life-changing decisions).
    /// Based on Villager_Behavioral_Personality.md design.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(VillagerSystemGroup))]
    [UpdateAfter(typeof(VillagerGrudgeDecaySystem))]
    public partial struct VillagerInitiativeSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused) return;

            var job = new ComputeInitiativeJob
            {
                CurrentTick = timeState.Tick
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        public partial struct ComputeInitiativeJob : IJobEntity
        {
            public uint CurrentTick;

            public void Execute(
                ref VillagerInitiativeState initiativeState,
                ref VillagerBehavior behavior,
                in VillagerAlignment alignment)
            {
                // Base initiative from village band (placeholder: default to 0.5 for now)
                // TODO: Query village band initiative when village membership system is wired
                float baseInitiative = 0.5f;

                // Apply bold/craven modifier
                // Bold (+60): +0.12 to initiative
                // Craven (-60): -0.10 to initiative
                float boldModifier = behavior.BoldScore * 0.002f; // Scale: -0.2 to +0.2
                baseInitiative += boldModifier;

                // Apply grudge boost (if active grudges exist)
                // Active Grudge Present: InitiativeBoost = Intensity × 0.002
                float grudgeBoost = 0f;
                // Note: Grudge intensity is checked in VillagerGrudgeDecaySystem
                // For now, we'll compute boost from active grudge count
                // TODO: Sum actual grudge intensities when buffer is accessible
                if (behavior.ActiveGrudgeCount > 0)
                {
                    // Estimate boost: each active grudge adds ~0.05 initiative
                    grudgeBoost = behavior.ActiveGrudgeCount * 0.05f;
                }

                // Apply alignment multipliers (from Generalized_Alignment_Framework.md)
                // Lawful personalities: dampen initiative swings (more stable)
                // Chaotic personalities: amplify initiative swings (volatile)
                float alignmentModifier = 1f;
                if (alignment.IsLawful)
                {
                    alignmentModifier = 0.9f; // Slight dampening for lawful
                }
                else if (alignment.IsChaotic)
                {
                    alignmentModifier = 1.1f; // Amplification for chaotic
                }

                // Compute final initiative
                float computedInitiative = (baseInitiative + grudgeBoost) * alignmentModifier;
                computedInitiative = math.clamp(computedInitiative, 0f, 1f);

                // Update state
                initiativeState.CurrentInitiative = computedInitiative;
                behavior.InitiativeModifier = computedInitiative - baseInitiative;

                // Compute next action tick based on initiative frequency
                // High Initiative (0.8+): Every ~2-5 days
                // Medium Initiative (0.4-0.6): Every ~10-20 days
                // Low Initiative (0.0-0.3): Every ~30-60 days
                // Using ticks: 1 tick = 1 second, 1 day = 86400 ticks
                if (CurrentTick >= initiativeState.NextActionTick)
                {
                    float daysUntilNextAction;
                    if (computedInitiative >= 0.8f)
                    {
                        daysUntilNextAction = 2f + (1f - computedInitiative) * 3f; // 2-5 days
                    }
                    else if (computedInitiative >= 0.4f)
                    {
                        daysUntilNextAction = 10f + (0.6f - computedInitiative) * 50f; // 10-20 days
                    }
                    else
                    {
                        daysUntilNextAction = 30f + (0.4f - computedInitiative) * 75f; // 30-60 days
                    }

                    uint ticksUntilNext = (uint)(daysUntilNextAction * 86400f);
                    initiativeState.NextActionTick = CurrentTick + ticksUntilNext;
                }
            }
        }
    }

    /// <summary>
    /// System that decays grudge intensity over time based on VengefulScore.
    /// Vengeful villagers hold grudges longer; forgiving villagers let them fade quickly.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(VillagerSystemGroup))]
    public partial struct VillagerGrudgeDecaySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused) return;

            var job = new DecayGrudgesJob
            {
                CurrentTick = timeState.Tick,
                DeltaTime = timeState.FixedDeltaTime
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        public partial struct DecayGrudgesJob : IJobEntity
        {
            public uint CurrentTick;
            public float DeltaTime;

            public void Execute(
                ref DynamicBuffer<VillagerGrudge> grudges,
                ref VillagerBehavior behavior,
                in VillagerAlignment alignment)
            {
                if (grudges.Length == 0)
                {
                    behavior.ActiveGrudgeCount = 0;
                    return;
                }

                // Calculate decay rate based on VengefulScore
                // Vengeful (-70): DecayRate = 0.01 × (100 + VengefulScore) = 0.3 per day
                // Forgiving (+60): DecayRate = 2.0 per day (rapid fade)
                float decayRate;
                if (behavior.VengefulScore < -20) // Vengeful
                {
                    decayRate = 0.01f * (100f + behavior.VengefulScore); // 0.3-0.8 per day
                }
                else if (behavior.VengefulScore > 40) // Forgiving
                {
                    decayRate = 2.0f; // Rapid fade
                }
                else // Neutral
                {
                    decayRate = 0.5f; // Moderate decay
                }

                // Convert per-day rate to per-second
                float decayPerSecond = decayRate / 86400f;

                // Decay all grudges and remove expired ones
                int activeCount = 0;
                for (int i = grudges.Length - 1; i >= 0; i--)
                {
                    var grudge = grudges[i];
                    grudge.IntensityScore -= decayPerSecond * DeltaTime;
                    grudge.IntensityScore = math.max(0f, grudge.IntensityScore);

                    if (grudge.IntensityScore > 0.1f) // Threshold for "active"
                    {
                        grudges[i] = grudge;
                        activeCount++;
                    }
                    else
                    {
                        grudges.RemoveAt(i);
                    }
                }

                behavior.ActiveGrudgeCount = (byte)activeCount;
            }
        }
    }
}

