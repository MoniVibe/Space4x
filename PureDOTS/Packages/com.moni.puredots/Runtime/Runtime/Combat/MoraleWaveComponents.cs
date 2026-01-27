using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Combat
{
    /// <summary>
    /// Morale wave - propagating morale change event.
    /// </summary>
    public struct MoraleWave : IComponentData
    {
        public Entity SourceEntity;         // Entity that broke/routed
        public float Intensity;             // Morale change intensity (-1.0 to 1.0)
        public float Radius;                // Propagation radius
        public uint EmittedTick;            // When wave was emitted
        public float PropagationDelay;      // Delay before propagation starts
    }

    /// <summary>
    /// Morale threshold - categorical morale levels.
    /// </summary>
    public enum MoraleThreshold : byte
    {
        Routed = 0,     // < 20 morale
        Shaken = 1,     // 20-40 morale
        Steady = 2,     // 40-70 morale
        Inspired = 3    // > 70 morale
    }

    /// <summary>
    /// Morale propagation configuration.
    /// </summary>
    public struct MoralePropagationConfig : IComponentData
    {
        public float PropagationRadius;     // Base radius for propagation
        public float PropagationDecay;      // Decay per distance unit (0-1)
        public float PropagationDelay;      // Delay before propagation (seconds)
        public float MinIntensity;          // Minimum intensity to propagate
    }

    /// <summary>
    /// Morale wave target - entity affected by morale wave.
    /// </summary>
    public struct MoraleWaveTarget : IComponentData
    {
        public Entity WaveSource;           // Source of the wave
        public float AppliedIntensity;      // Intensity applied to this entity
        public uint AppliedTick;            // When applied
    }
}



