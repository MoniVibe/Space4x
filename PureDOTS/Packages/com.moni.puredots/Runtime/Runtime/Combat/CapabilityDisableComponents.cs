using Unity.Entities;

namespace PureDOTS.Runtime.Combat
{
    /// <summary>
    /// Module capability - maps module to capability.
    /// </summary>
    public struct ModuleCapability : IComponentData
    {
        public Entity ModuleEntity;
        public CapabilityType Capability;
    }

    /// <summary>
    /// Capability types.
    /// </summary>
    public enum CapabilityType : byte
    {
        Movement = 0,
        Firing = 1,
        Shields = 2,
        Sensors = 3,
        Communications = 4,
        LifeSupport = 5
    }

    /// <summary>
    /// Capability state - tracks if capability is enabled/disabled.
    /// </summary>
    public struct CapabilityState : IComponentData
    {
        public CapabilityFlags EnabledCapabilities;
    }

    /// <summary>
    /// Capability flags - bitmask of enabled capabilities.
    /// </summary>
    [System.Flags]
    public enum CapabilityFlags : byte
    {
        None = 0,
        Movement = 1 << 0,
        Firing = 1 << 1,
        Shields = 1 << 2,
        Sensors = 1 << 3,
        Communications = 1 << 4,
        LifeSupport = 1 << 5
    }

    /// <summary>
    /// Capability effectiveness - tracks partial capability reduction.
    /// </summary>
    public struct CapabilityEffectiveness : IComponentData
    {
        public float MovementEffectiveness; // 0-1
        public float FiringEffectiveness; // 0-1
        public float ShieldEffectiveness; // 0-1
        public float SensorEffectiveness; // 0-1
        public float CommunicationEffectiveness; // 0-1
        public float LifeSupportEffectiveness; // 0-1
    }
}

