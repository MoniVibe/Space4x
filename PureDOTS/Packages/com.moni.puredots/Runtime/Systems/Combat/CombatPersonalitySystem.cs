using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Villagers;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Combat
{
    /// <summary>
    /// System that updates combat AI behavior based on villager personality traits.
    /// Bold/Craven affects stance selection and flee thresholds.
    /// Vengeful/Forgiving affects yield thresholds and non-lethal preferences.
    /// Based on Villager_Behavioral_Personality.md combat behavior modulation.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateBefore(typeof(CombatResolutionSystem))]
    public partial struct CombatPersonalitySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var job = new UpdateCombatPersonalityJob();
            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        public partial struct UpdateCombatPersonalityJob : IJobEntity
        {
            public void Execute(
                ref CombatAI combatAI,
                in VillagerBehavior behavior,
                in VillagerAlignment alignment)
            {
                // Update aggression based on Bold/Craven
                // Bold (+70): Aggression = +35 (reckless)
                // Craven (-70): Aggression = -35 (defensive)
                combatAI.Aggression = (sbyte)math.clamp(behavior.BoldScore / 2, -50, 50);

                // Update flee threshold based on Bold/Craven
                // Bold (+70): FleeThresholdHP = 0 (fights to death)
                // Craven (-70): FleeThresholdHP = 60 (flees early)
                // Neutral (0): FleeThresholdHP = 30 (balanced)
                combatAI.FleeThresholdHP = (byte)math.clamp(30 - (behavior.BoldScore / 2), 0, 60);

                // Update yield threshold based on Vengeful/Forgiving
                // Forgiving (+60): YieldThresholdHP = 40 (yields early)
                // Vengeful (-70): YieldThresholdHP = 0 (never yields)
                // Neutral (0): YieldThresholdHP = 20 (balanced)
                if (behavior.VengefulScore < -20) // Vengeful
                {
                    combatAI.YieldThresholdHP = 0; // Never yields
                }
                else if (behavior.VengefulScore > 40) // Forgiving
                {
                    combatAI.YieldThresholdHP = 40; // Yields early
                }
                else // Neutral
                {
                    combatAI.YieldThresholdHP = 20; // Balanced
                }

                // Update preferred stance based on Bold/Craven
                // Bold: Prefers Aggressive stance
                // Craven: Prefers Defensive stance
                // Neutral: Prefers Balanced stance
                if (behavior.IsBold)
                {
                    combatAI.PreferredStance = (byte)ActiveCombat.CombatStance.Aggressive;
                }
                else if (behavior.IsCraven)
                {
                    combatAI.PreferredStance = (byte)ActiveCombat.CombatStance.Defensive;
                }
                else
                {
                    combatAI.PreferredStance = (byte)ActiveCombat.CombatStance.Balanced;
                }

                // Update non-lethal/execution preferences based on alignment
                // Good alignment: Prefers non-lethal (spare enemies)
                // Evil alignment: Executes prisoners (no mercy)
                combatAI.PrefersNonLethal = alignment.IsGood;
                combatAI.ExecutesPrisoners = alignment.IsEvil;
            }
        }
    }
}

