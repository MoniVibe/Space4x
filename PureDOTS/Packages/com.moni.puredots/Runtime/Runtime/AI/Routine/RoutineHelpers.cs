using Unity.Mathematics;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Runtime.AI.Routine
{
    /// <summary>
    /// Static helpers for routine calculations.
    /// </summary>
    [BurstCompile]
    public static class RoutineHelpers
    {
        /// <summary>
        /// Gets the day phase for a given hour.
        /// </summary>
        public static DayPhase GetPhaseForHour(float hour, in RoutineConfig config)
        {
            // Normalize hour to 0-24 range
            hour = hour % 24f;
            if (hour < 0) hour += 24f;

            // Check phases in order (handles wrap-around at midnight)
            if (hour >= config.MidnightHour && hour < config.DawnHour)
                return DayPhase.Midnight;
            if (hour >= config.DawnHour && hour < config.MorningHour)
                return DayPhase.Dawn;
            if (hour >= config.MorningHour && hour < config.NoonHour)
                return DayPhase.Morning;
            if (hour >= config.NoonHour && hour < config.AfternoonHour)
                return DayPhase.Noon;
            if (hour >= config.AfternoonHour && hour < config.DuskHour)
                return DayPhase.Afternoon;
            if (hour >= config.DuskHour && hour < config.EveningHour)
                return DayPhase.Dusk;
            if (hour >= config.EveningHour && hour < config.NightHour)
                return DayPhase.Evening;
            if (hour >= config.NightHour || hour < config.MidnightHour)
                return DayPhase.Night;

            return DayPhase.Morning; // Fallback
        }

        /// <summary>
        /// Gets the default routine config with standard day phases.
        /// </summary>
        public static RoutineConfig GetDefaultConfig()
        {
            return new RoutineConfig
            {
                DawnHour = 5f,
                MorningHour = 7f,
                NoonHour = 11f,
                AfternoonHour = 13f,
                DuskHour = 17f,
                EveningHour = 19f,
                NightHour = 22f,
                MidnightHour = 2f,
                DayLengthSeconds = 1200f // 20 minutes per game day
            };
        }

        /// <summary>
        /// Converts real seconds to game hours.
        /// </summary>
        public static float SecondsToGameHours(float seconds, float dayLengthSeconds)
        {
            return (seconds / dayLengthSeconds) * 24f;
        }

        /// <summary>
        /// Converts game hours to real seconds.
        /// </summary>
        public static float GameHoursToSeconds(float hours, float dayLengthSeconds)
        {
            return (hours / 24f) * dayLengthSeconds;
        }

        /// <summary>
        /// Gets the start hour of a phase.
        /// </summary>
        public static float GetPhaseStartHour(DayPhase phase, in RoutineConfig config)
        {
            return phase switch
            {
                DayPhase.Dawn => config.DawnHour,
                DayPhase.Morning => config.MorningHour,
                DayPhase.Noon => config.NoonHour,
                DayPhase.Afternoon => config.AfternoonHour,
                DayPhase.Dusk => config.DuskHour,
                DayPhase.Evening => config.EveningHour,
                DayPhase.Night => config.NightHour,
                DayPhase.Midnight => config.MidnightHour,
                _ => 0f
            };
        }

        /// <summary>
        /// Gets the duration of a phase in hours.
        /// </summary>
        public static float GetPhaseDurationHours(DayPhase phase, in RoutineConfig config)
        {
            float start = GetPhaseStartHour(phase, config);
            float end = GetPhaseStartHour(GetNextPhase(phase), config);
            
            // Handle wrap-around
            if (end < start) end += 24f;
            
            return end - start;
        }

        /// <summary>
        /// Gets the next phase in sequence.
        /// </summary>
        public static DayPhase GetNextPhase(DayPhase current)
        {
            return (DayPhase)(((byte)current + 1) % 8);
        }

        /// <summary>
        /// Gets the previous phase in sequence.
        /// </summary>
        public static DayPhase GetPreviousPhase(DayPhase current)
        {
            return (DayPhase)(((byte)current + 7) % 8);
        }

        /// <summary>
        /// Checks if a phase is during daytime (dawn to dusk).
        /// </summary>
        public static bool IsDaytime(DayPhase phase)
        {
            return phase >= DayPhase.Dawn && phase <= DayPhase.Dusk;
        }

        /// <summary>
        /// Checks if a phase is during nighttime (evening to midnight).
        /// </summary>
        public static bool IsNighttime(DayPhase phase)
        {
            return phase >= DayPhase.Evening || phase == DayPhase.Midnight;
        }

        /// <summary>
        /// Gets the scheduled activity for a phase from the schedule buffer.
        /// </summary>
        public static RoutineActivity GetScheduledActivity(
            in DynamicBuffer<RoutineSchedule> schedule,
            DayPhase phase)
        {
            for (int i = 0; i < schedule.Length; i++)
            {
                if (schedule[i].Phase == phase)
                    return schedule[i].Activity;
            }
            return RoutineActivity.None;
        }

        /// <summary>
        /// Gets the priority of a scheduled activity.
        /// </summary>
        public static byte GetScheduledPriority(
            in DynamicBuffer<RoutineSchedule> schedule,
            DayPhase phase)
        {
            for (int i = 0; i < schedule.Length; i++)
            {
                if (schedule[i].Phase == phase)
                    return schedule[i].Priority;
            }
            return 0;
        }

        /// <summary>
        /// Checks if an interrupt can override current activity.
        /// </summary>
        public static bool CanInterrupt(
            in EntityRoutine routine,
            in DynamicBuffer<RoutineSchedule> schedule,
            byte interruptPriority)
        {
            // Already interrupted by higher priority
            if (routine.IsInterrupted && routine.InterruptPriority >= interruptPriority)
                return false;

            // Check scheduled priority
            byte scheduledPriority = GetScheduledPriority(schedule, routine.CurrentPhase);
            return interruptPriority > scheduledPriority;
        }

        /// <summary>
        /// Gets a default schedule for a worker.
        /// </summary>
        public static void GetDefaultWorkerSchedule(ref DynamicBuffer<RoutineSchedule> schedule)
        {
            schedule.Clear();
            schedule.Add(new RoutineSchedule { Phase = DayPhase.Dawn, Activity = RoutineActivity.Wake, Priority = 1 });
            schedule.Add(new RoutineSchedule { Phase = DayPhase.Morning, Activity = RoutineActivity.Work, Priority = 3 });
            schedule.Add(new RoutineSchedule { Phase = DayPhase.Noon, Activity = RoutineActivity.Eat, Priority = 2 });
            schedule.Add(new RoutineSchedule { Phase = DayPhase.Afternoon, Activity = RoutineActivity.Work, Priority = 3 });
            schedule.Add(new RoutineSchedule { Phase = DayPhase.Dusk, Activity = RoutineActivity.Rest, Priority = 1 });
            schedule.Add(new RoutineSchedule { Phase = DayPhase.Evening, Activity = RoutineActivity.Leisure, Priority = 1 });
            schedule.Add(new RoutineSchedule { Phase = DayPhase.Night, Activity = RoutineActivity.Sleep, Priority = 4 });
            schedule.Add(new RoutineSchedule { Phase = DayPhase.Midnight, Activity = RoutineActivity.Sleep, Priority = 5 });
        }

        /// <summary>
        /// Gets a default schedule for a guard.
        /// </summary>
        public static void GetDefaultGuardSchedule(ref DynamicBuffer<RoutineSchedule> schedule)
        {
            schedule.Clear();
            schedule.Add(new RoutineSchedule { Phase = DayPhase.Dawn, Activity = RoutineActivity.Patrol, Priority = 3 });
            schedule.Add(new RoutineSchedule { Phase = DayPhase.Morning, Activity = RoutineActivity.Guard, Priority = 4 });
            schedule.Add(new RoutineSchedule { Phase = DayPhase.Noon, Activity = RoutineActivity.Eat, Priority = 2 });
            schedule.Add(new RoutineSchedule { Phase = DayPhase.Afternoon, Activity = RoutineActivity.Guard, Priority = 4 });
            schedule.Add(new RoutineSchedule { Phase = DayPhase.Dusk, Activity = RoutineActivity.Patrol, Priority = 3 });
            schedule.Add(new RoutineSchedule { Phase = DayPhase.Evening, Activity = RoutineActivity.Rest, Priority = 2 });
            schedule.Add(new RoutineSchedule { Phase = DayPhase.Night, Activity = RoutineActivity.Sleep, Priority = 3 });
            schedule.Add(new RoutineSchedule { Phase = DayPhase.Midnight, Activity = RoutineActivity.Sleep, Priority = 4 });
        }

        /// <summary>
        /// Gets light level for a phase (0-1).
        /// </summary>
        public static float GetLightLevel(DayPhase phase)
        {
            return phase switch
            {
                DayPhase.Dawn => 0.3f,
                DayPhase.Morning => 0.8f,
                DayPhase.Noon => 1.0f,
                DayPhase.Afternoon => 0.9f,
                DayPhase.Dusk => 0.4f,
                DayPhase.Evening => 0.2f,
                DayPhase.Night => 0.05f,
                DayPhase.Midnight => 0.02f,
                _ => 0.5f
            };
        }

        /// <summary>
        /// Gets activity efficiency modifier for time of day.
        /// </summary>
        public static float GetActivityEfficiency(RoutineActivity activity, DayPhase phase)
        {
            // Some activities are more efficient at certain times
            return activity switch
            {
                RoutineActivity.Work => IsDaytime(phase) ? 1.0f : 0.7f,
                RoutineActivity.Sleep => IsNighttime(phase) ? 1.0f : 0.5f,
                RoutineActivity.Hunt => phase == DayPhase.Dawn || phase == DayPhase.Dusk ? 1.2f : 1.0f,
                RoutineActivity.Gather => IsDaytime(phase) ? 1.0f : 0.5f,
                RoutineActivity.Guard => IsNighttime(phase) ? 1.1f : 1.0f, // More alert at night
                _ => 1.0f
            };
        }
    }
}

