using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Spells;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Spells
{
    /// <summary>
    /// Handles spell practice attempts with success chance based on mastery.
    /// Updates mastery progress based on success/failure.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplaySystemGroup))]
    [UpdateAfter(typeof(SpellCastingSystem))]
    public partial struct SpellPracticeSystem : ISystem
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

            // Process practice attempts from failed casts
            new ProcessPracticeAttemptsJob
            {
                CurrentTick = currentTick
            }.ScheduleParallel();
        }

        [BurstCompile]
        public partial struct ProcessPracticeAttemptsJob : IJobEntity
        {
            public uint CurrentTick;

            void Execute(
                Entity entity,
                ref DynamicBuffer<ExtendedSpellMastery> mastery,
                in DynamicBuffer<SpellCastEvent> castEvents)
            {
                // Process cast events to update practice stats
                for (int i = 0; i < castEvents.Length; i++)
                {
                    var castEvent = castEvents[i];

                    // Find mastery entry
                    int masteryIndex = -1;
                    for (int j = 0; j < mastery.Length; j++)
                    {
                        if (mastery[j].SpellId.Equals(castEvent.SpellId))
                        {
                            masteryIndex = j;
                            break;
                        }
                    }

                    if (masteryIndex < 0)
                    {
                        continue; // Spell not being learned
                    }

                    var masteryEntry = mastery[masteryIndex];

                    // Update practice stats
                    masteryEntry.PracticeAttempts++;

                    if (castEvent.Result == SpellCastResult.Success)
                    {
                        masteryEntry.SuccessfulCasts++;

                        // Grant XP for successful cast
                        // More XP at lower mastery, less at higher mastery
                        float baseXp = 0.02f; // 2% base
                        float masteryFactor = 1f - (masteryEntry.MasteryProgress / 4f) * 0.5f; // 0.5-1.0 multiplier
                        float xpGain = baseXp * masteryFactor;

                        masteryEntry.MasteryProgress += xpGain;
                    }
                    else if (castEvent.Result == SpellCastResult.Fizzled)
                    {
                        masteryEntry.FailedCasts++;

                        // Grant small XP for learning from failure
                        float failureXp = 0.005f; // 0.5% base
                        masteryEntry.MasteryProgress += failureXp;
                    }

                    // Clamp to 4.0 (400%)
                    masteryEntry.MasteryProgress = math.min(masteryEntry.MasteryProgress, 4.0f);
                    masteryEntry.LastUpdateTick = CurrentTick;

                    mastery[masteryIndex] = masteryEntry;
                }
            }
        }
    }
}

