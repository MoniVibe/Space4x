using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Motivation;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Motivation
{
    /// <summary>
    /// Picks which goal an entity is actively trying to pursue, based on Initiative & Importance.
    /// Does NOT decide how to execute it; games read MotivationIntent to drive behavior.
    /// Runs in SimulationSystemGroup to update active intents each frame.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct MotivationIntentSelectionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<MotivationConfigState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            // Skip if paused or rewinding
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var currentTick = timeState.Tick;

            // Get scoring config (use default if singleton doesn't exist)
            var scoringConfig = MotivationScoringConfig.Default;
            if (SystemAPI.TryGetSingleton<MotivationScoringConfig>(out var scoringSingleton))
            {
                scoringConfig = scoringSingleton;
            }

            // Process all entities with motivation drive
            foreach (var (drive, intent, entity) in SystemAPI.Query<
                RefRW<MotivationDrive>,
                RefRW<MotivationIntent>>().WithEntityAccess())
            {
                // Throttle how often an entity can switch intent
                if (currentTick == drive.ValueRO.LastInitiativeTick)
                    continue;

                byte initiative = drive.ValueRO.InitiativeCurrent;
                if (initiative == 0)
                    continue;

                var slots = SystemAPI.GetBuffer<MotivationSlot>(entity);

                // Find highest-scoring available slot
                byte bestIndex = 255;
                float bestScore = -1f;

                for (int i = 0; i < slots.Length; i++)
                {
                    var slot = slots[i];
                    if (slot.Status != MotivationStatus.Available && slot.Status != MotivationStatus.InProgress)
                        continue;

                    // Skip if not locked and has no importance
                    if ((slot.LockFlags & MotivationLockFlags.LockedByPlayer) == 0 && slot.Importance == 0)
                        continue;

                    // Calculate score using configurable weights
                    float score = CalculateScore(
                        slot.Importance,
                        initiative,
                        drive.ValueRO.LoyaltyCurrent,
                        in scoringConfig);

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestIndex = (byte)i;
                    }
                }

                // Update intent if we found a valid goal
                if (bestIndex != 255 && bestScore > 0f)
                {
                    var intentValue = intent.ValueRO;
                    var chosen = slots[bestIndex];
                    intentValue.ActiveSlotIndex = bestIndex;
                    intentValue.ActiveLayer = chosen.Layer;
                    intentValue.ActiveSpecId = chosen.SpecId;
                    intent.ValueRW = intentValue;

                    // Mark as in-progress if not already
                    if (chosen.Status != MotivationStatus.InProgress)
                    {
                        chosen.Status = MotivationStatus.InProgress;
                        slots[bestIndex] = chosen;
                    }

                    // Update last initiative check tick
                    var driveValue = drive.ValueRO;
                    driveValue.LastInitiativeTick = currentTick;
                    drive.ValueRW = driveValue;
                }
            }
        }

        /// <summary>
        /// Calculates intent selection score using configurable weights.
        /// Public for testing purposes.
        /// </summary>
        [BurstCompile]
        public static float CalculateScore(
            byte importance,
            byte initiative,
            byte loyalty,
            in MotivationScoringConfig config)
        {
            return importance * config.ImportanceWeight +
                   initiative * config.InitiativeWeight +
                   loyalty * config.LoyaltyWeight;
        }
    }
}

