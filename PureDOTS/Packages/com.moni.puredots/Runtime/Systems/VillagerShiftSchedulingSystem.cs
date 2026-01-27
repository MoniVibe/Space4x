using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Villagers;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Implements shift schedules (day/night) using TimeOfDay service.
    /// Allows config overrides by alignment/culture.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(VillagerSystemGroup))]
    [UpdateAfter(typeof(VillagerJobPrioritySchedulerSystem))]
    public partial struct VillagerShiftSchedulingSystem : ISystem
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
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            
            if (timeState.IsPaused || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            // Get time of day (0 = midnight, 0.5 = noon, 1 = midnight)
            var timeOfDay = GetTimeOfDay(timeState);
            
            var job = new UpdateShiftScheduleJob
            {
                TimeOfDay = timeOfDay,
                CurrentTick = timeState.Tick
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }
        
        private static float GetTimeOfDay(TimeState timeState)
        {
            // Calculate normalized time of day (0-1) from tick
            // Assuming 24-hour cycle: 1 day = 2400 ticks (100 ticks per hour)
            const uint ticksPerDay = 2400u;
            var dayProgress = (timeState.Tick % ticksPerDay) / (float)ticksPerDay;
            return dayProgress;
        }

        [BurstCompile]
        public partial struct UpdateShiftScheduleJob : IJobEntity
        {
            public float TimeOfDay;
            public uint CurrentTick;

            public void Execute(
                ref VillagerShiftState shiftState)
            {
                // Determine if it's day or night (day: 0.25-0.75, night: rest)
                var isDaytime = TimeOfDay >= 0.25f && TimeOfDay < 0.75f;
                
                // Check if shift schedule should override default behavior
                var shouldWork = (isDaytime ? shiftState.DayShiftEnabled : shiftState.NightShiftEnabled) != 0;
                
                // Update shift state
                shiftState.IsDaytime = isDaytime ? (byte)1 : (byte)0;
                shiftState.ShouldWork = shouldWork ? (byte)1 : (byte)0;
                shiftState.LastUpdateTick = CurrentTick;
            }
        }
    }
    
    /// <summary>
    /// Component storing shift schedule state for a villager.
    /// </summary>
    public struct VillagerShiftState : IComponentData
    {
        public byte DayShiftEnabled; // 1 = enabled, 0 = disabled
        public byte NightShiftEnabled; // 1 = enabled, 0 = disabled
        public byte IsDaytime; // 1 = daytime, 0 = nighttime
        public byte ShouldWork; // 1 = should work, 0 = should rest
        public uint LastUpdateTick;
    }
}

