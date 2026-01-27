using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Perception
{
    /// <summary>
    /// Sensory signal emitter - entity that emits signals (smell, sound, EM).
    /// </summary>
    public struct SensorySignalEmitter : IComponentData
    {
        public PerceptionChannel Channels;
        public float SmellStrength;
        public float SoundStrength;
        public float EMStrength;
        public float EmissionRadius;
        public byte IsActive;
    }

    /// <summary>
    /// Signal field cell - stores signal strengths in a spatial grid cell.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct SignalFieldCell : IBufferElementData
    {
        public float Smell;
        public float Sound;
        public float EM;
        public uint LastUpdatedTick;
    }

    /// <summary>
    /// Signal field configuration - decay rates, emission scale, and sampling parameters.
    /// </summary>
    public struct SignalFieldConfig : IComponentData
    {
        public float SmellDecayPerSecond;
        public float SoundDecayPerSecond;
        public float EMDecayPerSecond;
        public float EmissionScale;
        public float MaxStrength;
        public int MaxSamplingRadiusCells;
        public float SamplingFalloffExponent;
        public float Tier0SamplingRadiusMultiplier;
        public float Tier1SamplingRadiusMultiplier;
        public float Tier2SamplingRadiusMultiplier;
        public float Tier3SamplingRadiusMultiplier;

        public static SignalFieldConfig Default => new SignalFieldConfig
        {
            SmellDecayPerSecond = 0.25f,
            SoundDecayPerSecond = 0.5f,
            EMDecayPerSecond = 0.75f,
            EmissionScale = 1f,
            MaxStrength = 1f,
            MaxSamplingRadiusCells = 3,
            SamplingFalloffExponent = 1.5f,
            Tier0SamplingRadiusMultiplier = 1f,
            Tier1SamplingRadiusMultiplier = 0.75f,
            Tier2SamplingRadiusMultiplier = 0.5f,
            Tier3SamplingRadiusMultiplier = 0.25f
        };
    }

    /// <summary>
    /// Signal field state tracking for grid updates.
    /// </summary>
    public struct SignalFieldState : IComponentData
    {
        public uint LastUpdateTick;
        public uint Version;
    }

    /// <summary>
    /// Cached perception state for medium-based signals.
    /// </summary>
    public struct SignalPerceptionState : IComponentData
    {
        public float SmellLevel;
        public float SmellConfidence;
        public float SoundLevel;
        public float SoundConfidence;
        public float EMLevel;
        public float EMConfidence;
        public uint LastUpdateTick;
        public uint LastSmellInterruptTick;
        public uint LastSoundInterruptTick;
        public uint LastEMInterruptTick;
    }

    /// <summary>
    /// Thresholds for emitting perception-based interrupts.
    /// </summary>
    public struct SignalPerceptionThresholds : IComponentData
    {
        public float SmellThreshold;
        public float SoundThreshold;
        public float EMThreshold;
        public uint CooldownTicks;

        public static SignalPerceptionThresholds Default => new SignalPerceptionThresholds
        {
            SmellThreshold = 0.25f,
            SoundThreshold = 0.25f,
            EMThreshold = 0.25f,
            CooldownTicks = 10
        };
    }

    /// <summary>
    /// Perception channel flags enum.
    /// </summary>
    [System.Flags]
    public enum PerceptionChannel : uint
    {
        None = 0,
        Vision = 1 << 0,
        Hearing = 1 << 1,
        Smell = 1 << 2,
        EM = 1 << 3,
        Gravitic = 1 << 4,
        Exotic = 1 << 5,
        Paranormal = 1 << 6,
        Proximity = 1 << 7
    }
}
