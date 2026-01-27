using PureDOTS.Runtime.Components;

namespace PureDOTS.Runtime.Time
{
    /// <summary>
    /// Helper methods for creating TimeControlCommand instances.
    /// Provides a stable API for games to create time control commands without
    /// depending on the internal structure of TimeControlCommand.
    /// </summary>
    public static class TimeControlHelpers
    {
        /// <summary>
        /// Creates a command to step forward by the specified number of ticks.
        /// </summary>
        public static TimeControlCommand MakeStepTicks(int ticks)
        {
            return new TimeControlCommand
            {
                Type = TimeControlCommandType.StepTicks,
                UintParam = (uint)ticks
            };
        }

        /// <summary>
        /// Creates a command to set the simulation speed multiplier.
        /// </summary>
        public static TimeControlCommand MakeSetSpeed(float speedMultiplier)
        {
            return new TimeControlCommand
            {
                Type = TimeControlCommandType.SetSpeed,
                FloatParam = speedMultiplier
            };
        }

        /// <summary>
        /// Creates a command to toggle pause state.
        /// </summary>
        public static TimeControlCommand MakeTogglePause()
        {
            return new TimeControlCommand
            {
                Type = TimeControlCommandType.Pause
            };
        }

        /// <summary>
        /// Creates a command to start rewinding.
        /// </summary>
        public static TimeControlCommand MakeStartRewind()
        {
            return new TimeControlCommand
            {
                Type = TimeControlCommandType.StartRewind
            };
        }

        /// <summary>
        /// Creates a command to stop rewinding.
        /// </summary>
        public static TimeControlCommand MakeStopRewind()
        {
            return new TimeControlCommand
            {
                Type = TimeControlCommandType.StopRewind
            };
        }
    }
}

