using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Components
{
    public struct TimeControlConfig : IComponentData
    {
        public float DefaultSpeed;
        public float FastForwardSpeed;
        public float SlowMotionSpeed;
        public float DefaultRewindTicksPerSecond;
        public float FastRewindTicksPerSecond;
        public float ScrubSpeedMultiplier;
        public bool EnableKeyboardShortcuts;
        public bool ShowDebugUI;
        public bool LogStateChanges;
    }

    public struct TimeControlSingletonTag : IComponentData
    {
    }

    public struct TimeControlCommand : IBufferElementData
    {
        public enum CommandType : byte
        {
            None = 0,
            Pause = 1,
            Resume = 2,
            SetSpeed = 3,
            StartRewind = 4,
            StopRewind = 5,
            ScrubTo = 6
        }

        public CommandType Type;
        public float FloatParam;
        public uint UintParam;
    }
}
