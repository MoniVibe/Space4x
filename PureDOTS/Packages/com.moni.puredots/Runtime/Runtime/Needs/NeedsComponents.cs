using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Needs
{
    /// <summary>
    /// Types of needs an entity can have.
    /// </summary>
    public enum NeedType : byte
    {
        None = 0,
        
        // Biological (1-19)
        Hunger = 1,         // Food requirement
        Thirst = 2,         // Water requirement
        Fatigue = 3,        // Rest requirement
        Health = 4,         // Physical wellbeing
        Hygiene = 5,        // Cleanliness
        Warmth = 6,         // Temperature comfort
        
        // Psychological (10-29)
        Social = 10,        // Interaction need
        Entertainment = 11, // Leisure need
        Safety = 12,        // Security feeling
        Purpose = 13,       // Meaningful work
        Privacy = 14,       // Alone time
        Comfort = 15,       // Physical comfort
        
        // Spiritual (20-29)
        Faith = 20,         // Religious fulfillment
        Enlightenment = 21, // Knowledge seeking
        Beauty = 22,        // Aesthetic appreciation
        
        // Operational/System (30-39)
        Fuel = 30,
        Power = 31,
        Maintenance = 32,
        Supplies = 33,
        Ammunition = 34,
        Coolant = 35
    }

    /// <summary>
    /// Urgency level for a need.
    /// </summary>
    public enum NeedUrgency : byte
    {
        Satisfied = 0,      // 80-100%
        Normal = 1,         // 50-79%
        Concerned = 2,      // 25-49%
        Urgent = 3,         // 10-24%
        Critical = 4        // 0-9%
    }

    /// <summary>
    /// Simplified needs for most entities (composite needs).
    /// </summary>
    public struct EntityNeeds : IComponentData
    {
        // Core needs (0-1000 scale)
        public float Health;
        public float MaxHealth;
        public float Energy;           // Combines hunger/fatigue
        public float MaxEnergy;
        public float Morale;           // Combines social/purpose
        public float MaxMorale;
        
        // Urgency flags
        public NeedUrgency HealthUrgency;
        public NeedUrgency EnergyUrgency;
        public NeedUrgency MoraleUrgency;
        
        // Decay rates (per second)
        public float EnergyDecayRate;
        public float MoraleDecayRate;
        
        // Regen rates (per second when replenishing)
        public float HealthRegenRate;
        public float EnergyRegenRate;
        public float MoraleRegenRate;
        
        // Timestamps
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Individual need entry for detailed simulation.
    /// </summary>
    [InternalBufferCapacity(6)]
    public struct NeedEntry : IBufferElementData
    {
        public NeedType Type;
        public float Current;          // 0-100
        public float Max;              // Maximum (usually 100)
        public float DecayRate;        // Per second
        public float RegenRate;        // Per second when replenishing
        public NeedUrgency Urgency;
        public uint LastUpdateTick;
        public uint LastSatisfiedTick; // When need was last fully met
    }

    /// <summary>
    /// Configuration for needs system.
    /// </summary>
    public struct NeedsConfig : IComponentData
    {
        public float SatisfiedThreshold;    // 80% - above this is Satisfied
        public float NormalThreshold;       // 50% - above this is Normal
        public float ConcernedThreshold;    // 25% - above this is Concerned
        public float UrgentThreshold;       // 10% - above this is Urgent
        // Below UrgentThreshold = Critical
        
        public float WorkingDecayMult;      // 2.5x decay when working
        public float IdleDecayMult;         // 0.5x decay when idle
        public float SleepRegenMult;        // 5x regen when sleeping
        public float EatingRegenMult;       // 3x regen when eating
        
        public float CriticalPerformancePenalty; // 0.5 = 50% penalty at critical
        public float UrgentPerformancePenalty;   // 0.25 = 25% penalty at urgent
    }

    /// <summary>
    /// Activity state affecting need decay/regen rates.
    /// </summary>
    public enum ActivityState : byte
    {
        Idle = 0,
        Working = 1,
        Resting = 2,
        Sleeping = 3,
        Eating = 4,
        Socializing = 5,
        Exercising = 6,
        Traveling = 7,
        Combat = 8
    }

    /// <summary>
    /// Current activity affecting needs.
    /// </summary>
    public struct NeedsActivityState : IComponentData
    {
        public ActivityState Current;
        public uint StateStartTick;
        public float StateDuration;    // How long in this state
    }

    /// <summary>
    /// Event when a need reaches critical level.
    /// </summary>
    [InternalBufferCapacity(2)]
    public struct NeedCriticalEvent : IBufferElementData
    {
        public NeedType NeedType;
        public float CurrentValue;
        public uint Tick;
    }

    /// <summary>
    /// Request to satisfy a need (e.g., eating, resting).
    /// </summary>
    public struct SatisfyNeedRequest : IComponentData
    {
        public NeedType NeedType;
        public float Amount;
        public Entity SourceEntity;    // What provided the satisfaction (food, bed, etc.)
    }
}

