using Unity.Entities;

namespace PureDOTS.Runtime.Components
{
    public struct TimeSettingsConfig : IComponentData
    {
        public float FixedDeltaTime;
        public float MaxDeltaTime;
        public float DefaultSpeedMultiplier;
        public bool PauseOnStart;
    }

    public static class TimeSettingsDefaults
    {
        public const float FixedDeltaTime = 1f / 60f;
        public const float MaxDeltaTime = 1f / 10f;
        public const float DefaultSpeed = 1f;
        public const float DefaultSpeedMultiplier = DefaultSpeed; // Alias for DefaultSpeed for backward compatibility
        public const bool PauseOnStart = false;

        public static TickTimeState CreateTickTimeDefault()
        {
            return new TickTimeState
            {
                FixedDeltaTime = FixedDeltaTime,
                CurrentSpeedMultiplier = DefaultSpeed,
                Tick = 0,
                TargetTick = 0,
                IsPaused = PauseOnStart,
                IsPlaying = !PauseOnStart
            };
        }
    }
}
