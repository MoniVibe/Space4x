using PureDOTS.Runtime.Aggregate;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Construction;
using PureDOTS.Runtime.Motivation;
using PureDOTS.Systems.Aggregate;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Construction
{
    /// <summary>
    /// Updates BuildPreferenceProfile based on group motivations and alignment.
    /// Reads active MotivationSlots and AggregateStats to influence building preferences.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(AmbientConditionsUpdateSystem))]
    public partial struct BuildPreferenceUpdateSystem : ISystem
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
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var currentTick = timeState.Tick;
            var updateFrequency = 1000u; // Update every ~11 seconds at 90 TPS

            // Process groups with BuildPreferenceProfile and MotivationDrive
            foreach (var (preferences, entity) in SystemAPI.Query<RefRW<BuildPreferenceProfile>>().WithEntityAccess())
            {
                var prefValue = preferences.ValueRO;

                // Only update periodically
                if (prefValue.LastUpdateTick > 0 &&
                    (currentTick - prefValue.LastUpdateTick) < updateFrequency)
                {
                    continue;
                }

                // Start with default weights
                var newPrefs = new BuildPreferenceProfile
                {
                    HousingWeight = 1f,
                    StorageWeight = 1f,
                    WorshipWeight = 1f,
                    DefenseWeight = 1f,
                    FoodWeight = 1f,
                    ProductionWeight = 1f,
                    InfrastructureWeight = 1f,
                    AestheticWeight = 1f,
                    LastUpdateTick = currentTick
                };

                // Apply alignment-based preferences (if AggregateStats exists)
                if (SystemAPI.HasComponent<AggregateStats>(entity))
                {
                    var stats = SystemAPI.GetComponent<AggregateStats>(entity);
                    ApplyAlignmentPreferences(ref newPrefs, in stats);
                }

                // Apply motivation-based preferences (if MotivationSlots exist)
                if (SystemAPI.HasBuffer<MotivationSlot>(entity))
                {
                    var slots = SystemAPI.GetBuffer<MotivationSlot>(entity);
                    ApplyMotivationPreferences(ref newPrefs, in slots);
                }

                preferences.ValueRW = newPrefs;
            }
        }

        [BurstCompile]
        private static void ApplyAlignmentPreferences(ref BuildPreferenceProfile prefs, in AggregateStats stats)
        {
            // Pure/Good → boost Housing/Worship/Aesthetic
            if (stats.AvgEvilGood > 50f)
            {
                prefs.HousingWeight *= 1.2f;
                prefs.WorshipWeight *= 1.3f;
                prefs.AestheticWeight *= 1.2f;
            }

            // Corrupt/Evil → boost Defense/Production/Special
            if (stats.AvgCorruptPure < -50f)
            {
                prefs.DefenseWeight *= 1.3f;
                prefs.ProductionWeight *= 1.2f;
            }

            // Lawful → boost Infrastructure
            if (stats.AvgChaoticLawful > 50f)
            {
                prefs.InfrastructureWeight *= 1.2f;
            }

            // Chaotic → boost Aesthetic
            if (stats.AvgChaoticLawful < -50f)
            {
                prefs.AestheticWeight *= 1.2f;
            }
        }

        [BurstCompile]
        private static void ApplyMotivationPreferences(ref BuildPreferenceProfile prefs, in DynamicBuffer<MotivationSlot> slots)
        {
            // TODO: Read MotivationSpec from catalog to determine which categories to boost
            // For now, stub - game-specific systems can extend this
            // Example logic:
            // - If active ambition is "Become fortress" → boost Defense weight
            // - If active ambition is "Build grand temple" → boost Worship weight
            // - If active ambition is "Become trade hub" → boost Production/Infrastructure weights

            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot.Status == MotivationStatus.InProgress)
                {
                    // Stub: Would read MotivationSpec.SpecId from catalog and check tags
                    // For now, just boost based on common patterns
                }
            }
        }
    }
}
























