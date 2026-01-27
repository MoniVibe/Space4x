using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Spells;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Spells
{
    /// <summary>
    /// Manages extended mastery progression beyond 100%.
    /// Handles milestone unlocks (200%, 300%, 400%) and signature eligibility.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplaySystemGroup))]
    [UpdateAfter(typeof(SpellPracticeSystem))]
    public partial struct ExtendedMasterySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var currentTick = timeState.Tick;

            // Check for milestone unlocks
            new CheckMilestoneUnlocksJob
            {
                CurrentTick = currentTick
            }.ScheduleParallel();
        }

        [BurstCompile]
        public partial struct CheckMilestoneUnlocksJob : IJobEntity
        {
            public uint CurrentTick;

            void Execute(
                Entity entity,
                ref DynamicBuffer<ExtendedSpellMastery> mastery)
            {
                for (int i = 0; i < mastery.Length; i++)
                {
                    var masteryEntry = mastery[i];
                    var previousMilestone = SpellMasteryUtility.GetMilestone(masteryEntry.MasteryProgress - 0.01f);
                    var currentMilestone = SpellMasteryUtility.GetMilestone(masteryEntry.MasteryProgress);

                    // Check if milestone was just reached
                    if (currentMilestone > previousMilestone)
                    {
                        // Unlock signature flag
                        switch (currentMilestone)
                        {
                            case SpellMasteryMilestone.Signature1: // 200%
                                masteryEntry.Signatures |= SpellSignatureFlags.Signature1Unlocked;
                                break;
                            case SpellMasteryMilestone.Signature2: // 300%
                                masteryEntry.Signatures |= SpellSignatureFlags.Signature2Unlocked;
                                break;
                            case SpellMasteryMilestone.Signature3: // 400%
                                masteryEntry.Signatures |= SpellSignatureFlags.Signature3Unlocked;
                                masteryEntry.Signatures |= SpellSignatureFlags.HybridizationUnlocked;
                                break;
                        }

                        masteryEntry.LastUpdateTick = CurrentTick;
                    }

                    mastery[i] = masteryEntry;
                }
            }
        }
    }
}

