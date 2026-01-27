using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Motivation;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Motivation
{
    /// <summary>
    /// Listens for goal-completed markers and awards dynasty/legacy points.
    /// Games are responsible for deciding when a goal is completed and
    /// adding a GoalCompleted marker pointing at the slot index.
    /// Runs in SimulationSystemGroup to process completion events.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct MotivationRewardSystem : ISystem
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

            // Skip if paused or rewinding
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            // Process all entities with completed goals
            foreach (var (legacy, completed, entity) in SystemAPI.Query<
                RefRW<LegacyPoints>,
                DynamicBuffer<GoalCompleted>>().WithEntityAccess())
            {
                if (completed.Length == 0)
                    continue;

                var slots = SystemAPI.GetBuffer<MotivationSlot>(entity);
                var legacyValue = legacy.ValueRO;
                int addedPoints = 0;

                for (int i = 0; i < completed.Length; i++)
                {
                    var evt = completed[i];
                    if (evt.SlotIndex >= slots.Length)
                        continue;

                    var slot = slots[evt.SlotIndex];
                    if (slot.Status == MotivationStatus.Satisfied)
                        continue; // Already processed

                    // Mark goal as satisfied
                    slot.Status = MotivationStatus.Satisfied;
                    slot.Progress = 255;
                    slots[evt.SlotIndex] = slot;

                    // Reward model:
                    // - Base: importance / 10 (so 0–25)
                    // - Bonus if locked by player/aggregate
                    int baseReward = slot.Importance / 10;
                    int bonus = 0;
                    if ((slot.LockFlags & MotivationLockFlags.LockedByPlayer) != 0)
                        bonus += 10;
                    if ((slot.LockFlags & MotivationLockFlags.LockedByAggregate) != 0)
                        bonus += 10;

                    int reward = baseReward + bonus;
                    if (reward < 1)
                        reward = 1;

                    addedPoints += reward;

                    // TODO (per-game): optionally spawn tribute event for god/empire here
                }

                if (addedPoints > 0)
                {
                    legacyValue.TotalEarned += addedPoints;
                    legacyValue.Unspent += addedPoints;
                    legacy.ValueRW = legacyValue;
                }

                // Clear completed buffer after processing
                completed.Clear();
            }
        }
    }
}

